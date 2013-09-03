using System;
using Eluant.ObjectBinding;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace Eluant
{
    public class LuaTransparentClrObject : LuaClrObjectValue, IEquatable<LuaTransparentClrObject>, IBindingContext
    {
        private static readonly IBindingSecurityPolicy defaultSecurityPolicy = new BasicBindingSecurityPolicy(MemberSecurityPolicy.Deny);

        public IBindingSecurityPolicy BindingSecurityPolicy { get; private set; }
        public ILuaBinder Binder { get; private set; }

        private TransparentClrObjectProxy proxy;

        public LuaTransparentClrObject(object obj) : this(obj, null, null) { }

        public LuaTransparentClrObject(object obj, ILuaBinder binder, IBindingSecurityPolicy bindingSecurityPolicy) : base(obj)
        {
            Binder = binder ?? BasicLuaBinder.Instance;
            BindingSecurityPolicy = bindingSecurityPolicy ?? defaultSecurityPolicy;

            proxy = new TransparentClrObjectProxy(this);
        }

        internal override void Push(LuaRuntime runtime)
        {
            runtime.PushCustomClrObject(this);
        }

        public override bool Equals(LuaValue other)
        {
            return Equals(other as LuaTransparentClrObject);
        }

        public bool Equals(LuaTransparentClrObject obj)
        {
            return obj != null && obj.ClrObject == ClrObject;
        }

        internal override object BackingCustomObject
        {
            get { return proxy; }
        }

        private class TransparentClrObjectProxy : ILuaTableBinding, ILuaEqualityBinding
        {
            private LuaTransparentClrObject clrObject;

            public TransparentClrObjectProxy(LuaTransparentClrObject obj)
            {
                clrObject = obj;
            }

            private static LuaTransparentClrObject GetObjectValue(LuaValue v)
            {
                var r = v as LuaClrObjectReference;
                if (r != null) {
                    return r.ClrObjectValue as LuaTransparentClrObject;
                }

                return null;
            }

            #region ILuaEqualityBinding implementation

            public LuaValue Equals(LuaRuntime runtime, LuaValue left, LuaValue right)
            {
                var leftObj = GetObjectValue(left);
                var rightObj = GetObjectValue(right);

                if (object.ReferenceEquals(leftObj, rightObj)) {
                    return true;
                }

                if (leftObj == null || rightObj == null) {
                    return false;
                }

                return leftObj.ClrObject == rightObj.ClrObject &&
                    leftObj.Binder == rightObj.Binder &&
                        leftObj.BindingSecurityPolicy == rightObj.BindingSecurityPolicy;
            }

            #endregion

            private static string KeyToString(LuaValue key)
            {
                var str = key as LuaString;
                if (str != null) {
                    return str.Value;
                }

                var num = key as LuaNumber;
                if (num != null) {
                    return num.Value.ToString();
                }

                return null;
            }

            private List<MemberInfo> GetMembers(LuaValue keyValue)
            {
                var key = KeyToString(keyValue);

                if (key != null) {
                    return clrObject.Binder.GetMembersByName(clrObject.ClrObject, key)
                        .Where(i => clrObject.BindingSecurityPolicy.GetMemberSecurityPolicy(i) == MemberSecurityPolicy.Permit)
                            .ToList();
                }

                return new List<MemberInfo>();
            }

            #region ILuaTableBinding implementation

            public LuaValue this[LuaRuntime runtime, LuaValue keyValue]
            {
                get {
                    var members = GetMembers(keyValue);

                    if (members.Count == 1) {
                        var method = members[0] as MethodInfo;
                        if (method != null) {
                            return runtime.CreateFunctionFromMethodWrapper(new LuaRuntime.MethodWrapper(clrObject.ClrObject, method));
                        }

                        var property = members[0] as PropertyInfo;
                        if (property != null) {
                            var getter = property.GetGetMethod();
                            if (getter == null) {
                                throw new LuaException("Property is write-only.");
                            }
                            if (getter.GetParameters().Length != 0) {
                                throw new LuaException("Cannot get an indexer.");
                            }

                            return clrObject.Binder.ObjectToLuaValue(property.GetValue(clrObject.ClrObject, null), clrObject, runtime);
                        }

                        var field = members[0] as FieldInfo;
                        if (field != null) {
                            return clrObject.Binder.ObjectToLuaValue(field.GetValue(clrObject.ClrObject), clrObject, runtime);
                        }
                    }

                    return LuaNil.Instance;
                }
                set {
                    var members = GetMembers(keyValue);

                    if (members.Count == 1) {
                        var property = members[0] as PropertyInfo;
                        if (property != null) {
                            var setter = property.GetSetMethod();
                            if (setter == null) {
                                throw new LuaException("Property is read-only.");
                            }
                            if (setter.GetParameters().Length != 1) {
                                throw new LuaException("Cannot set an indexer.");
                            }

                            object v;
                            try {
                                v = value.ToClrType(property.PropertyType);
                            } catch {
                                throw new LuaException("Value is incompatible with this property.");
                            }

                            property.SetValue(clrObject.ClrObject, v, null);
                            return;
                        }

                        var field = members[0] as FieldInfo;
                        if (field != null) {
                            object v;
                            try {
                                v = value.ToClrType(field.FieldType);
                            } catch {
                                throw new LuaException("Value is incompatible with this property.");
                            }

                            field.SetValue(clrObject.ClrObject, v);
                            return;
                        }
                    }

                    throw new LuaException("Property/field not found: " + keyValue.ToString());
                }
            }

            #endregion
        }
    }
}


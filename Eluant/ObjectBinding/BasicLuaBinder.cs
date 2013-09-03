using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Collections.ObjectModel;

namespace Eluant.ObjectBinding
{
    using MemberNameMap = Dictionary<string, List<MemberInfo>>;

    public class BasicLuaBinder : ILuaBinder
    {
        private static readonly BasicLuaBinder instance = new BasicLuaBinder();

        public static BasicLuaBinder Instance
        {
            get { return instance; }
        }

        private static Dictionary<Type, MemberNameMap> memberNameCache = new Dictionary<Type, MemberNameMap>();

        private static readonly MemberInfo[] noMembers = new MemberInfo[0];

        public BasicLuaBinder() { }

        private static MemberNameMap GetMembersByName(Type type)
        {
            var membersByName = new MemberNameMap();

            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance)) {
                var method = member as MethodInfo;
                if (method != null && method.IsGenericMethodDefinition) {
                    continue;
                }

                foreach (var memberNameAttr in member.GetCustomAttributes(typeof(LuaMemberAttribute), true).Cast<LuaMemberAttribute>()) {
                    var memberName = memberNameAttr.LuaKey ?? member.Name;

                    List<MemberInfo> members;
                    if (!membersByName.TryGetValue(memberName, out members)) {
                        members = new List<MemberInfo>();
                        membersByName[memberName] = members;
                    }

                    members.Add(member);
                }
            }

            return membersByName;
        }

        #region ILuaBinder implementation

        public virtual ICollection<MemberInfo> GetMembersByName(object targetObject, string memberName)
        {
            if (targetObject == null) { throw new ArgumentNullException("targetObject"); }
            if (memberName == null) { throw new ArgumentNullException("memberName"); }

            var type = targetObject.GetType();

            MemberNameMap memberNameMap;

            lock (memberNameCache) {
                if (!memberNameCache.TryGetValue(type, out memberNameMap)) {
                    memberNameMap = GetMembersByName(type);
                    memberNameCache[type] = memberNameMap;
                }
            }

            List<MemberInfo> members;
            if (memberNameMap.TryGetValue(memberName, out members)) {
                return new ReadOnlyCollection<MemberInfo>(members);
            }

            return noMembers;
        }

        public virtual MethodInfo ResolveOverload(ICollection<MemberInfo> possibleOverloads, LuaVararg arguments)
        {
            throw new NotImplementedException("Overload resolution is not yet supported.");
        }

        public virtual LuaValue ObjectToLuaValue(object obj, IBindingContext bindingContext, LuaRuntime runtime)
        {
            return runtime.AsLuaValue(obj) ??
                new LuaTransparentClrObject(obj, bindingContext.Binder, bindingContext.BindingSecurityPolicy);
        }

        #endregion
    }
}


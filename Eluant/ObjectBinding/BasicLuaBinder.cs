//
// BasicLuaBinder.cs
//
// Author:
//       Chris Howie <me@chrishowie.com>
//
// Copyright (c) 2013 Chris Howie
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

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


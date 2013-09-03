using System;
using System.Collections.Generic;
using System.Reflection;

namespace Eluant.ObjectBinding
{
    public interface ILuaBinder
    {
        ICollection<MemberInfo> GetMembersByName(object targetObject, string memberName);

        MethodInfo ResolveOverload(ICollection<MemberInfo> possibleOverloads, LuaVararg arguments);

        LuaValue ObjectToLuaValue(object obj, IBindingContext bindingContext, LuaRuntime runtime);
    }
}


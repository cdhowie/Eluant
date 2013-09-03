using System;
using System.Reflection;

namespace Eluant.ObjectBinding
{
    public interface IBindingSecurityPolicy
    {
        MemberSecurityPolicy GetMemberSecurityPolicy(MemberInfo member);
    }
}


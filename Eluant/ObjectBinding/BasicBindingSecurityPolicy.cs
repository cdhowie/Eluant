using System;
using System.Reflection;

namespace Eluant.ObjectBinding
{
    public class BasicBindingSecurityPolicy : IBindingSecurityPolicy
    {
        public MemberSecurityPolicy DefaultPolicy { get; private set; }

        public BasicBindingSecurityPolicy(MemberSecurityPolicy policy)
        {
            DefaultPolicy = policy;
        }

        #region IBindingSecurityPolicy implementation

        public virtual MemberSecurityPolicy GetMemberSecurityPolicy(MemberInfo member)
        {
            if (member.GetCustomAttributes(typeof(LuaMemberAttribute), true).Length != 0) {
                return MemberSecurityPolicy.Permit;
            }

            return DefaultPolicy;
        }

        #endregion
    }
}


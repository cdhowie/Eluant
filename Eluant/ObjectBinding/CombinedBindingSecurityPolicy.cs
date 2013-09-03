using System;
using System.Reflection;

namespace Eluant.ObjectBinding
{
    public class CombinedBindingSecurityPolicy : IBindingSecurityPolicy
    {
        public IBindingSecurityPolicy FirstPolicy { get; private set; }
        public IBindingSecurityPolicy SecondPolicy { get; private set; }

        public CombinedBindingSecurityPolicy(IBindingSecurityPolicy first, IBindingSecurityPolicy second)
        {
            if (first == null) { throw new ArgumentNullException("first"); }
            if (second == null) { throw new ArgumentNullException("second"); }

            FirstPolicy = first;
            SecondPolicy = second;
        }

        #region IBindingSecurityPolicy implementation

        public MemberSecurityPolicy GetMemberSecurityPolicy(MemberInfo member)
        {
            var first = FirstPolicy.GetMemberSecurityPolicy(member);

            if (first == MemberSecurityPolicy.Unspecified) {
                return SecondPolicy.GetMemberSecurityPolicy(member);
            }

            return first;
        }

        #endregion
    }
}


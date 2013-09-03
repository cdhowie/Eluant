using System;

namespace Eluant.ObjectBinding
{
    [Flags]
    public enum MemberSecurityPolicy
    {
        Deny,
        Permit,

        Unspecified,
    }
}


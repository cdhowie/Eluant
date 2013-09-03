using System;

namespace Eluant.ObjectBinding
{
    public interface IBindingContext
    {
        IBindingSecurityPolicy BindingSecurityPolicy { get; }
        ILuaBinder Binder { get; }
    }
}


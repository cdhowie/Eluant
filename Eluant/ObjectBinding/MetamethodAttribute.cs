using System;

namespace Eluant.ObjectBinding
{
    // This attribute is internal because it should not be added to any interfaces except those declared by Eluant.
    [AttributeUsage(AttributeTargets.Interface, Inherited=false, AllowMultiple=true)]
    internal class MetamethodAttribute : Attribute
    {
        public string MethodName { get; private set; }

        public MetamethodAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }
}


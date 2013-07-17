using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Eluant
{
    internal static class Scripts
    {
        private static string GetResource(string file)
        {
            using (var s = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Eluant." + file), Encoding.UTF8)) {
                return s.ReadToEnd();
            }
        }

        private static string bindingSupport;

        public static string BindingSupport
        {
            get {
                if (bindingSupport == null) {
                    bindingSupport = GetResource("BindingSupport.lua");
                }

                return bindingSupport;
            }
        }
    }
}


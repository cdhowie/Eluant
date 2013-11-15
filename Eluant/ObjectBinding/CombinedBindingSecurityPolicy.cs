//
// CombinedBindingSecurityPolicy.cs
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


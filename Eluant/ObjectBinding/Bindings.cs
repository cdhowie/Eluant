//
// Bindings.cs
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

namespace Eluant.ObjectBinding
{
    [Metamethod("__gc")]
    public interface ILuaFinalizedBinding
    {
        void Finalized(LuaRuntime runtime);
    }

    [Metamethod("__index")]
    [Metamethod("__newindex")]
    public interface ILuaTableBinding
    {
        LuaValue this[LuaRuntime runtime, LuaValue key] { get; set; }
    }

    [Metamethod("__add")]
    public interface ILuaAdditionBinding
    {
        LuaValue Add(LuaRuntime runtime, LuaValue left, LuaValue right);
    }

    [Metamethod("__sub")]
    public interface ILuaSubtractionBinding
    {
        LuaValue Subtract(LuaRuntime runtime, LuaValue left, LuaValue right);
    }

    [Metamethod("__mul")]
    public interface ILuaMultiplicationBinding
    {
        LuaValue Multiply(LuaRuntime runtime, LuaValue left, LuaValue right);
    }

    [Metamethod("__div")]
    public interface ILuaDivisionBinding
    {
        LuaValue Divide(LuaRuntime runtime, LuaValue left, LuaValue right);
    }

    [Metamethod("__mod")]
    public interface ILuaModuloBinding
    {
        LuaValue Modulo(LuaRuntime runtime, LuaValue left, LuaValue right);
    }

    [Metamethod("__pow")]
    public interface ILuaExponentiationBinding
    {
        LuaValue Power(LuaRuntime runtime, LuaValue left, LuaValue right);
    }

    [Metamethod("__unm")]
    public interface ILuaUnaryMinusBinding
    {
        LuaValue Minus(LuaRuntime runtime);
    }

    [Metamethod("__concat")]
    public interface ILuaConcatenationBinding
    {
        LuaValue Concatenate(LuaRuntime runtime, LuaValue left, LuaValue right);
    }

    [Metamethod("__len")]
    public interface ILuaLengthBinding
    {
        LuaValue GetLength(LuaRuntime runtime);
    }

    [Metamethod("__eq")]
    public interface ILuaEqualityBinding
    {
        LuaValue Equals(LuaRuntime runtime, LuaValue left, LuaValue right);
    }

    [Metamethod("__lt")]
    public interface ILuaLessThanBinding
    {
        LuaValue LessThan(LuaRuntime runtime, LuaValue left, LuaValue right);
    }

    [Metamethod("__le")]
    public interface ILuaLessThanOrEqualToBinding
    {
        LuaValue LessThanOrEqualTo(LuaRuntime runtime, LuaValue left, LuaValue right);
    }

    [Metamethod("__call")]
    public interface ILuaCallBinding
    {
        LuaVararg Call(LuaRuntime runtime, LuaVararg arguments);
    }

    public interface ILuaMathBinding :
        ILuaAdditionBinding,
        ILuaSubtractionBinding,
        ILuaMultiplicationBinding,
        ILuaDivisionBinding,
        ILuaModuloBinding,
        ILuaExponentiationBinding,
        ILuaUnaryMinusBinding,
        ILuaEqualityBinding,
        ILuaLessThanBinding,
        ILuaLessThanOrEqualToBinding { }
}

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

//
// LuaRuntime.cs
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
using System.Collections.Generic;
using System.Threading;
using System.Reflection;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Eluant.ObjectBinding;
using System.Linq;

namespace Eluant
{
    public class LuaRuntime : IDisposable
    {
        // These are our only two callbacks.  They need to be static methods for iOS, where the runtime cannot create C
        // function pointers from instance methods.
        private static readonly LuaApi.lua_CFunction clrObjectGcCallbackWrapper;
        private static readonly LuaApi.lua_CFunction methodWrapperCallCallbackWrapper;
        private static readonly LuaApi.lua_CFunction cFunctionCallback;

        static LuaRuntime()
        {
            clrObjectGcCallbackWrapper = ClrObjectGcCallbackWrapper;
            methodWrapperCallCallbackWrapper = MethodWrapperCallCallbackWrapper;
            cFunctionCallback = CFunctionCallback;
        }

        [UnmanagedFunctionPointer(LuaApi.LUA_CALLING_CONVENTION)]
        protected internal delegate IntPtr LuaAllocator(IntPtr ud, IntPtr ptr, IntPtr osize, IntPtr nsize);

        protected internal IntPtr LuaState { get; private set; }

        // A self-referential weak handle used by the static callback methods to locate the LuaRuntime instance.
        private GCHandle selfHandle;

        protected GCHandle SelfHandle
        {
            get { return selfHandle; }
        }

        private ObjectReferenceManager<LuaClrObjectValue> objectReferenceManager = new ObjectReferenceManager<LuaClrObjectValue>();

        // Separate field for the corner case where customAllocator was collected first.
        private bool hasCustomAllocator = false;
        private LuaAllocator customAllocator;

        private const string MAIN_THREAD_KEY = "eluant_main_thread";
        private const string REFERENCES_KEY = "eluant_references";

        private const string WEAKREFERENCE_METATABLE = "eluant_weakreference";
        private const string OPAQUECLROBJECT_METATABLE = "eluant_opaqueclrobject";

        private Dictionary<string, LuaFunction> metamethodCallbacks = new Dictionary<string, LuaFunction>();

        private LuaFunction createManagedCallWrapper;

        private ConcurrentQueue<int> releasedReferences = new ConcurrentQueue<int>();

        public LuaRuntime()
        {
            try {
                selfHandle = GCHandle.Alloc(this, GCHandleType.WeakTrackResurrection);

                IntPtr customState;
                customAllocator = CreateAllocatorDelegate(out customState);

                if (customAllocator != null) {
                    hasCustomAllocator = true;
                    //LuaState = LuaApi.luaL_newstate();
                    LuaState = LuaApi.lua_newstate(customAllocator, customState);
                } else {
                    hasCustomAllocator = false;
                    LuaState = LuaApi.luaL_newstate();
                }

                Globals = new LuaGlobalsTable(this);

                Initialize();
            } catch {
                Dispose();
                throw;
            }
        }

        // This is to support accounting (see MemoryConstrainedLuaRuntime).  Returning a delegate is wonky, but there
        // are good reasons for doing it this way.
        //
        // 1. Virtual method means we have to either implement a base allocator ourselves (instead of letting
        //    luaL_newstate() use its default) or we need to use reflection to detect if the method was overridden.
        //
        // 2. Abstract method means that LuaRuntime is abstract, and subclasses MUST implement their own allocator.
        //
        // So instead we have a method that returns a delegate, and a null return value means use the default allocator.
        protected virtual LuaAllocator CreateAllocatorDelegate(out IntPtr customState)
        {
            customState = IntPtr.Zero;
            return null;
        }

        protected virtual void PreInitialize() { }

        protected virtual void PostInitialize() { }

        internal static IntPtr GetMainThread(IntPtr state)
        {
            LuaApi.lua_getfield(state, LuaApi.LUA_REGISTRYINDEX, MAIN_THREAD_KEY);
            var mainThread = LuaApi.lua_touserdata(state, -1);
            LuaApi.lua_pop(state, 1);

            return mainThread;
        }

        private LuaFunction CreateCallbackWrapper(LuaApi.lua_CFunction callback)
        {
            var top = LuaApi.lua_gettop(LuaState);

            try {
                Push(createManagedCallWrapper);
                PushCFunction(callback);

                if (LuaApi.lua_pcall(LuaState, 1, 1, 0) != 0) {
                    throw new InvalidOperationException("Unable to create delegate wrapper.");
                }

                return (LuaFunction)Wrap(-1);
            } finally {
                LuaApi.lua_settop(LuaState, top);
            }
        }

        protected void PushSelf()
        {
            var ud = LuaApi.lua_newuserdata(LuaState, new UIntPtr(unchecked((ulong)IntPtr.Size)));
            Marshal.WriteIntPtr(ud, (IntPtr)selfHandle);
        }

        protected static LuaRuntime GetSelf(IntPtr state, int index)
        {
            var ud = LuaApi.lua_touserdata(state, index);
            var handle = (GCHandle)Marshal.ReadIntPtr(ud);

            return handle.Target as LuaRuntime;
        }

        private void Initialize()
        {
            PreInitialize();

            LuaApi.lua_newtable(LuaState);
            LuaApi.lua_setfield(LuaState, LuaApi.LUA_REGISTRYINDEX, REFERENCES_KEY);

            LuaApi.luaL_openlibs(LuaState);

            LuaApi.lua_pushlightuserdata(LuaState, LuaState);
            LuaApi.lua_setfield(LuaState, LuaApi.LUA_REGISTRYINDEX, MAIN_THREAD_KEY);

            LuaApi.luaL_newmetatable(LuaState, OPAQUECLROBJECT_METATABLE);

            LuaApi.lua_pushstring(LuaState, "__gc");
            PushSelf();
            LuaApi.lua_pushcclosure(LuaState, clrObjectGcCallbackWrapper, 1);
            LuaApi.lua_settable(LuaState, -3);

            LuaApi.lua_pushstring(LuaState, "__metatable");
            LuaApi.lua_pushboolean(LuaState, 0);
            LuaApi.lua_settable(LuaState, -3);

            LuaApi.lua_pushstring(LuaState, "is_clr_object");
            LuaApi.lua_pushboolean(LuaState, 1);
            LuaApi.lua_settable(LuaState, -3);

            LuaApi.lua_pop(LuaState, 1);

            LuaApi.luaL_newmetatable(LuaState, WEAKREFERENCE_METATABLE);

            LuaApi.lua_pushstring(LuaState, "__mode");
            LuaApi.lua_pushstring(LuaState, "v");
            LuaApi.lua_settable(LuaState, -3);

            LuaApi.lua_pop(LuaState, 1);

            DoString(Scripts.BindingSupport).Dispose();

            createManagedCallWrapper = (LuaFunction)Globals["eluant_create_managed_call_wrapper"];

            Globals["eluant_create_managed_call_wrapper"] = null;

            metamethodCallbacks["__newindex"] = CreateCallbackWrapper(NewindexCallback);
            metamethodCallbacks["__index"] = CreateCallbackWrapper(IndexCallback);

            metamethodCallbacks["__add"] = CreateCallbackWrapper(state => BinaryOperatorCallback<ILuaAdditionBinding>(state, (i, a, b) => i.Add(this, a, b)));
            metamethodCallbacks["__sub"] = CreateCallbackWrapper(state => BinaryOperatorCallback<ILuaSubtractionBinding>(state, (i, a, b) => i.Subtract(this, a, b)));
            metamethodCallbacks["__mul"] = CreateCallbackWrapper(state => BinaryOperatorCallback<ILuaMultiplicationBinding>(state, (i, a, b) => i.Multiply(this, a, b)));
            metamethodCallbacks["__div"] = CreateCallbackWrapper(state => BinaryOperatorCallback<ILuaDivisionBinding>(state, (i, a, b) => i.Divide(this, a, b)));
            metamethodCallbacks["__mod"] = CreateCallbackWrapper(state => BinaryOperatorCallback<ILuaModuloBinding>(state, (i, a, b) => i.Modulo(this, a, b)));
            metamethodCallbacks["__pow"] = CreateCallbackWrapper(state => BinaryOperatorCallback<ILuaExponentiationBinding>(state, (i, a, b) => i.Power(this, a, b)));
            metamethodCallbacks["__unm"] = CreateCallbackWrapper(state => UnaryOperatorCallback<ILuaUnaryMinusBinding>(state, i => i.Minus(this)));
            metamethodCallbacks["__concat"] = CreateCallbackWrapper(state => BinaryOperatorCallback<ILuaConcatenationBinding>(state, (i, a, b) => i.Concatenate(this, a, b)));
            metamethodCallbacks["__len"] = CreateCallbackWrapper(state => UnaryOperatorCallback<ILuaLengthBinding>(state, i => i.GetLength(this)));
            metamethodCallbacks["__eq"] = CreateCallbackWrapper(state => BinaryOperatorCallback<ILuaEqualityBinding>(state, (i, a, b) => i.Equals(this, a, b)));
            metamethodCallbacks["__lt"] = CreateCallbackWrapper(state => BinaryOperatorCallback<ILuaLessThanBinding>(state, (i, a, b) => i.LessThan(this, a, b)));
            metamethodCallbacks["__le"] = CreateCallbackWrapper(state => BinaryOperatorCallback<ILuaLessThanOrEqualToBinding>(state, (i, a, b) => i.LessThanOrEqualTo(this, a, b)));

            PostInitialize();
        }

        ~LuaRuntime()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void OnEnterLua() { }

        protected virtual void OnEnterClr() { }

        protected virtual void Dispose(bool disposing)
        {
            GC.SuppressFinalize(this);

            if (LuaState != IntPtr.Zero) {
                if (hasCustomAllocator && Environment.HasShutdownStarted) {
                    // This is the perfect storm.  The CLR is shutting down, and we created the Lua state with a custom
                    // allocator.  The allocator delegate may have already been finalized, which (at least on Mono)
                    // would mean that the unmanaged->managed trampoline has been collected.  Any action we take now
                    // (including lua_close()) would call this potentially missing trampoline.  If the trampoline is
                    // missing then this causes a segfault or access violation, taking the runtime down hard.
                    //
                    // The only sane thing to do here is skip lua_close() and let the OS clean up the Lua allocation.
                    //
                    // This means that Lua objects won't be collected, so hopefully no finalizations there were of a
                    // critical nature (or things that the OS won't do when the runtime process quits, anyway).
                    //
                    // Consumers should make sure that they dispose Lua runtimes before the CLR begins shutting down to
                    // avoid this scenario.
                    LuaState = IntPtr.Zero;
                } else {
                    LuaApi.lua_close(LuaState);
                    LuaState = IntPtr.Zero;
                }
            }

            if (selfHandle.IsAllocated) {
                selfHandle.Free();
            }
        }

        protected void CheckDisposed()
        {
            if (LuaState == IntPtr.Zero) {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public LuaGlobalsTable Globals { get; private set; }

        internal void Push(LuaValue value)
        {
            if (value == null) {
                // Special case for null.
                LuaApi.lua_pushnil(LuaState);
            } else {
                value.Push(this);
            }
        }

        private void RewriteReferenceTable()
        {
            LuaApi.lua_getfield(LuaState, LuaApi.LUA_REGISTRYINDEX, REFERENCES_KEY);
            LuaApi.lua_newtable(LuaState);

            LuaApi.lua_pushnil(LuaState);
            while (LuaApi.lua_next(LuaState, -3) != 0) {
                // Stack: reftable newtable key value
                // Goal:  reftable newtable key key value

                // reftable newtable key value key
                LuaApi.lua_pushvalue(LuaState, -2);
                // reftable newtable key key value
                LuaApi.lua_insert(LuaState, LuaApi.abs_index(LuaState, -2));

                // reftable newtable key
                LuaApi.lua_settable(LuaState, -4);

                // All set for next iteration.
            }

            // Swap out old table for the new table.
            LuaApi.lua_setfield(LuaState, LuaApi.LUA_REGISTRYINDEX, REFERENCES_KEY);

            // Pop the old table.
            LuaApi.lua_pop(LuaState, 1);
        }

        private int referenceSeq = 0;
        private int lastReference = 0;

        private int GetNextReference()
        {
            if (++referenceSeq == 100) {
                // Every hundred references taken, reset lastReference so that we try to reuse slots in the reference
                // table.  Otherwise the table is going to grow very large.
                referenceSeq = 0;
                lastReference = 0;
            }

            var reference = lastReference;
            do {
                // Increment the reference number with wraparound.
                if (reference == int.MaxValue) {
                    reference = 1;
                } else {
                    ++reference;
                }

                // Get the type of Lua value at this index.
                LuaApi.lua_rawgeti(LuaState, -1, reference);
                var type = LuaApi.lua_type(LuaState, -1);
                LuaApi.lua_pop(LuaState, 1);

                // If the entry at that slot was nil, it's a valid open slot.
                if (type == LuaApi.LuaType.Nil) {
                    lastReference = reference;

                    return reference;
                }

                // Stop looping if we traversed the entire reference-space (unlikely...).
            } while (reference != lastReference);

            throw new InvalidOperationException("Too many references.");
        }

        // The following methods are how we implement CLR references to Lua objects.  Similar to luaL_ref(), we do so
        // in a table, but rather than using the registry we use a table stored in the registry.  This gives us a bit
        // more flexibility while also avoiding clashing with other stuff that may use numeric keys into the registry.
        //
        // The idea is that we put a Lua object at a numeric index that is not in use.  Then the Lua GC will not collect
        // the object, and that numeric index becomes the reference ID used by the CLR object wrapper.  To push the
        // reference we just look up the ID in the reference table.  To destroy the reference, we set that table slot to
        // nil, which releases that Lua reference.
        private int CreateReference(int index)
        {
            index = LuaApi.abs_index(LuaState, index);

            // Need the table first, and NextReference depends on it being pushed first.
            LuaApi.lua_getfield(LuaState, LuaApi.LUA_REGISTRYINDEX, REFERENCES_KEY);
            var reference = GetNextReference();

            LuaApi.lua_pushvalue(LuaState, index);
            LuaApi.lua_rawseti(LuaState, -2, reference);

            LuaApi.lua_pop(LuaState, 1);

            return reference;
        }

        private int destroySeq = 0;

        private void DestroyReference(int reference)
        {
            LuaApi.lua_getfield(LuaState, LuaApi.LUA_REGISTRYINDEX, REFERENCES_KEY);
            LuaApi.lua_pushnil(LuaState);
            LuaApi.lua_rawseti(LuaState, -2, reference);

            LuaApi.lua_pop(LuaState, 1);

            // Every 1000 destroys, rewrite the reference table to try to reduce its memory footprint.
            if (++destroySeq == 1000) {
                destroySeq = 0;
                RewriteReferenceTable();
            }
        }

        internal void PushReference(int reference)
        {
            LuaApi.lua_getfield(LuaState, LuaApi.LUA_REGISTRYINDEX, REFERENCES_KEY);
            LuaApi.lua_rawgeti(LuaState, -1, reference);
            LuaApi.lua_remove(LuaState, LuaApi.abs_index(LuaState, -2));
        }

        internal LuaValue Wrap(int index)
        {
            var type = LuaApi.lua_type(LuaState, index);

            switch (type) {
                case LuaApi.LuaType.Nil:
                    return LuaNil.Instance;

                case LuaApi.LuaType.Boolean:
                    return (LuaBoolean)(LuaApi.lua_toboolean(LuaState, index) != 0);

                case LuaApi.LuaType.Number:
                    return (LuaNumber)LuaApi.lua_tonumber(LuaState, index);

                case LuaApi.LuaType.String:
                    return (LuaString)LuaApi.lua_tostring(LuaState, index);

                case LuaApi.LuaType.Table:
                    return new LuaTable(this, CreateReference(index));

                case LuaApi.LuaType.Function:
                    return new LuaFunction(this, CreateReference(index));

                case LuaApi.LuaType.LightUserdata:
                    return new LuaLightUserdata(this, CreateReference(index));

                case LuaApi.LuaType.Userdata:
                    if (IsClrObject(index)) {
                        return new LuaClrObjectReference(this, CreateReference(index));
                    }

                    return new LuaUserdata(this, CreateReference(index));

                case LuaApi.LuaType.Thread:
                    return new LuaThread(this, CreateReference(index));
            }

            throw new InvalidOperationException("Don't know how to wrap Lua type " + type.ToString());
        }

        private bool HasMetatable(int index, string tableName)
        {
            var top = LuaApi.lua_gettop(LuaState);

            try {
                if (LuaApi.lua_getmetatable(LuaState, index) == 0) {
                    return false;
                }

                LuaApi.luaL_getmetatable(LuaState, tableName);
                return LuaApi.lua_rawequal(LuaState, -1, -2) != 0;
            } finally {
                LuaApi.lua_settop(LuaState, top);
            }
        }

        internal void DisposeReference(int reference, bool isExplicit)
        {
            // If the Lua state is gone then this is a successful no-op.
            if (LuaState == IntPtr.Zero) { return; }

            if (isExplicit) {
                // If Dispose() was called then assume that there is no contention for the Lua runtime.
                DestroyReference(reference);

                // This is probably a good time to destroy any other pending disposed references.
                ProcessReleasedReferences();
            } else if (Environment.HasShutdownStarted) {
                // If the CLR is terminating then do nothing; see LuaRuntime.Dispose(bool) for an explanation.
            } else {
                // Otherwise we have to arrange to have the reference released at a later time, since we can't be sure
                // that the runtime isn't in use on another thread.
                releasedReferences.Enqueue(reference);
            }
        }

        private void ProcessReleasedReferences()
        {
            int reference;
            while (releasedReferences.TryDequeue(out reference)) {
                DestroyReference(reference);
            }
        }

        private void LoadString(string str)
        {
            if (LuaApi.luaL_loadstring(LuaState, str) != 0) {
                var error = LuaApi.lua_tostring(LuaState, -1);
                LuaApi.lua_pop(LuaState, 1);

                throw new LuaException(error);
            }
        }

        public LuaVararg DoString(string str)
        {
            if (str == null) { throw new ArgumentNullException("str"); }

            CheckDisposed();

            LoadString(str);

            // Compiled code is on the stack, now call it.
            return Call(new LuaValue[0]);
        }

        public LuaFunction CompileString(string str)
        {
            if (str == null) { throw new ArgumentNullException("str"); }

            CheckDisposed();

            LoadString(str);

            var fn = Wrap(-1);

            LuaApi.lua_pop(LuaState, 1);

            return (LuaFunction)fn;
        }

        internal LuaVararg Call(LuaFunction fn, IList<LuaValue> args)
        {
            if (fn == null) { throw new ArgumentNullException("fn"); }
            if (args == null) { throw new ArgumentNullException("args"); }

            CheckDisposed();

            Push(fn);

            return Call(args);
        }

        // Calls a function that has already been pushed.  We need this functionality to support DoString().
        // Call CheckDisposed() before calling this method!
        private LuaVararg Call(IList<LuaValue> args)
        {
            if (args == null) { throw new ArgumentNullException("args"); }

            // Top should point to the frame BELOW the function, which should have already been pushed.
            var top = LuaApi.lua_gettop(LuaState) - 1;

            bool needEnterClr = false;

            LuaValue[] results = null;

            try {
                // Ensure room for function + args.
                if (LuaApi.lua_checkstack(LuaState, 1 + args.Count) == 0) {
                    throw new LuaException("Cannot grow stack for call arguments.");
                }

                // Whenever we cross a Lua/CLR boundary, release any references that were cleaned up by the CLR's
                // garbage collector.
                ProcessReleasedReferences();

                foreach (var arg in args) {
                    Push(arg);
                }

                needEnterClr = true;
                OnEnterLua();
                if (LuaApi.lua_pcall(LuaState, args.Count, LuaApi.LUA_MULTRET, 0) != 0) {
                    needEnterClr = false;
                    OnEnterClr();

                    // Finally block will take care of popping the error message.
                    throw new LuaException(LuaApi.lua_tostring(LuaState, -1));
                }
                needEnterClr = false;
                OnEnterClr();

                // Results are in the stack, last result on the top.
                var newTop = LuaApi.lua_gettop(LuaState);
                var nresults = newTop - top;
                results = new LuaValue[nresults];

                if (nresults > 0) {
                    // We may need one additional stack slot to wrap a reference.
                    if (LuaApi.lua_checkstack(LuaState, 1) == 0) {
                        throw new LuaException("Cannot grow stack for call results.");
                    }

                    for (int i = 0; i < nresults; ++i) {
                        results[i] = Wrap(top + 1 + i);
                    }
                }

                // Clean up any references again.
                ProcessReleasedReferences();

                var ret = new LuaVararg(results, true);

                // Clear out results so the finally block doesn't dispose of the references we are returning.
                results = null;

                return ret;
            } finally {
                if (needEnterClr) { OnEnterClr(); }

                // Takes care of resetting the stack after processing results or retrieving the error message.
                LuaApi.lua_settop(LuaState, top);

                // results will be non-null if an exception was thrown before we could return the LuaVararg.  Clean up
                // any references, since the caller will never get the chance to dispose them.
                if (results != null) {
                    foreach (var r in results) {
                        if (r != null) {
                            r.Dispose();
                        }
                    }
                }
            }
        }

        private void PushNewReferenceValue(int reference)
        {
            var userData = LuaApi.lua_newuserdata(LuaState, (UIntPtr)Marshal.SizeOf(typeof(IntPtr)));
            Marshal.WriteIntPtr(userData, new IntPtr(reference));
        }

        private bool IsClrObject(int index)
        {
            if (LuaApi.lua_getmetatable(LuaState, index) == 0) {
                return false;
            }

            LuaApi.lua_pushstring(LuaState, "is_clr_object");
            LuaApi.lua_gettable(LuaState, -2);

            var is_clr_object = LuaApi.lua_toboolean(LuaState, -1) != 0;

            LuaApi.lua_pop(LuaState, 2);

            return is_clr_object;
        }

        private int? TryGetReference(int index)
        {
            // Make sure this Lua value represents a CLR object.  There are security implications if things go wrong
            // here, so we check to make absolutely sure that the Lua value represents one of our CLR object types.
            if (LuaApi.lua_type(LuaState, index) == LuaApi.LuaType.Userdata) {
                if (IsClrObject(index)) {
                    var userData = LuaApi.lua_touserdata(LuaState, index);
                    var handlePtr = Marshal.ReadIntPtr(userData);

                    return handlePtr.ToInt32();
                }
            }

            return null;
        }

        internal T GetClrObject<T>(int index)
            where T : LuaClrObjectValue
        {
            var obj = TryGetClrObject<LuaClrObjectValue>(index);

            if (obj == null) {
                throw new InvalidOperationException("Attempt to obtain CLR object from a Lua object that does not represent a CLR object.");
            }

            var typedObj = obj as T;
            if (typedObj == null) {
                throw new InvalidOperationException(string.Format("CLR object of type {0} was found, but CLR object of incompatible type {1} was expected.",
                                                                  obj.GetType().FullName,
                                                                  typeof(T).FullName));
            }

            return typedObj;
        }

        internal T TryGetClrObject<T>(int index)
            where T : LuaClrObjectValue
        {
            var reference = TryGetReference(index);

            if (!reference.HasValue) {
                return null;
            }

            return objectReferenceManager.GetReference(reference.Value) as T;
        }

        // This provides a general-purpose mechanism to push C functions into Lua without needing an instance method
        // (for iOS support).
        internal void PushCFunction(LuaApi.lua_CFunction fn)
        {
            PushSelf();
            PushOpaqueClrObject(new LuaOpaqueClrObject(fn));
            LuaApi.lua_pushcclosure(LuaState, cFunctionCallback, 2);
        }
        
#if (__IOS__ || MONOTOUCH)
        [MonoTouch.MonoPInvokeCallback(typeof(LuaApi.lua_CFunction))]
#endif
        private static int CFunctionCallback(IntPtr state)
        {
            LuaApi.lua_CFunction fn;
            try {
                var runtime = GetSelf(state, LuaApi.lua_upvalueindex(1));

                fn = (LuaApi.lua_CFunction)runtime.TryGetClrObject<LuaOpaqueClrObject>(LuaApi.lua_upvalueindex(2)).ClrObject;
            } catch {
                LuaApi.lua_pushboolean(state, 0);
                LuaApi.lua_pushstring(state, "Unexpected error processing callback.");

                return 2;
            }

            return fn(state);
        }

        internal void PushOpaqueClrObject(LuaOpaqueClrObject obj)
        {
            // We don't check for null, intentionally.
            PushNewReferenceValue(objectReferenceManager.CreateReference(obj));
            LuaApi.luaL_getmetatable(LuaState, OPAQUECLROBJECT_METATABLE);
            LuaApi.lua_setmetatable(LuaState, -2);
        }

        internal void PushCustomClrObject(LuaClrObjectValue obj)
        {
            if (obj == null || obj.ClrObject == null) {
                LuaApi.lua_pushnil(LuaState);
                return;
            }

            var reference = objectReferenceManager.CreateReference(obj);

            try {
                PushNewReferenceValue(reference);

                // We will build up a unique metatable for this object based on the bindings it has implemented.
                LuaApi.lua_newtable(LuaState);

                // Set flag so that TryGetReference knows that this is a CLR object.
                LuaApi.lua_pushstring(LuaState, "is_clr_object");
                LuaApi.lua_pushboolean(LuaState, 1);
                LuaApi.lua_settable(LuaState, -3);

                // Protect the metatable.
                LuaApi.lua_pushstring(LuaState, "__metatable");
                LuaApi.lua_pushboolean(LuaState, 0);
                LuaApi.lua_settable(LuaState, -3);

                // __gc is required to clean up the reference.  The callback will determine if it implements the
                // interface.
                LuaApi.lua_pushstring(LuaState, "__gc");
                PushSelf();
                LuaApi.lua_pushcclosure(LuaState, clrObjectGcCallbackWrapper, 1);
                LuaApi.lua_settable(LuaState, -3);

                // For all others, we use MetamethodAttribute on the interface to make this code less repetitive.
                var metamethods = obj.BackingCustomObject.GetType().GetInterfaces()
                    .SelectMany(iface => iface.GetCustomAttributes(typeof(MetamethodAttribute), false).Cast<MetamethodAttribute>());

                foreach (var metamethod in metamethods) {
                    LuaApi.lua_pushstring(LuaState, metamethod.MethodName);
                    Push(metamethodCallbacks[metamethod.MethodName]);
                    LuaApi.lua_settable(LuaState, -3);
                }

                LuaApi.lua_setmetatable(LuaState, -2);
            } catch {
                objectReferenceManager.DestroyReference(reference);
                throw;
            }
        }

        private int NewindexCallback(IntPtr state)
        {
            return LuaToClrBoundary(state, toDispose => {
                // Arguments: Userdata (CLR object), key (property), value
                var obj = GetClrObject<LuaClrObjectValue>(1).BackingCustomObject as ILuaTableBinding;

                if (obj == null) {
                    throw new LuaException("CLR object does not support indexing.");
                }

                var key = Wrap(2);
                toDispose.Add(key);

                var value = Wrap(3);
                toDispose.Add(value);

                obj[this, key] = value;

                return 0;
            });
        }

        private int IndexCallback(IntPtr state)
        {
            return LuaToClrBoundary(state, toDispose => {
                // Arguments: Userdata (CLR object), key (property)
                var obj = GetClrObject<LuaClrObjectValue>(1).BackingCustomObject as ILuaTableBinding;

                if (obj == null) {
                    throw new LuaException("CLR object does not support indexing.");
                }

                var key = Wrap(2);
                toDispose.Add(key);

                var value = obj[this, key];
                toDispose.Add(value);

                Push(value);

                return 1;
            });
        }

        private int CallCallback(IntPtr state)
        {
            return LuaToClrBoundary(state, toDispose => {
                var obj = GetClrObject<LuaClrObjectValue>(1).BackingCustomObject as ILuaCallBinding;

                if (obj == null) {
                    throw new LuaException("CLR object is not callable.");
                }

                var nargs = LuaApi.lua_gettop(LuaState) - 1;
                var args = new LuaValue[nargs];

                for (int i = 0; i < nargs; ++i) {
                    args[i] = Wrap(i + 2);
                    toDispose.Add(args[i]);
                }

                var vararg = new LuaVararg(args, true);

                var results = obj.Call(this, vararg);
                toDispose.Add(results);

                if (LuaApi.lua_checkstack(LuaState, 1 + results.Count) == 0) {
                    throw new LuaException("Cannot grow stack for results.");
                }

                foreach (var v in results) {
                    Push(v);
                }

                return results.Count;
            });
        }

        private int UnaryOperatorCallback<T>(IntPtr state, Func<T, LuaValue> oper)
            where T : class
        {
            return LuaToClrBoundary(state, toDispose => {
                var binding = GetClrObject<LuaClrObjectValue>(1).BackingCustomObject as T;

                if (binding == null) {
                    throw new LuaException("Unary operator not found for CLR object.");
                }

                var result = oper(binding);
                toDispose.Add(result);

                Push(result);
                return 1;
            });
        }

        private int BinaryOperatorCallback<T>(IntPtr state, Func<T, LuaValue, LuaValue, LuaValue> oper)
            where T : class
        {
            return LuaToClrBoundary(state, toDispose => {
                // For binary operators, the right argument could be a CLR object while the left argument might not, and
                // only one is guaranteed to support the given interface.  So we need to do some tests.
                LuaClrObjectValue obj;
                T binding = null;

                if ((obj = TryGetClrObject<LuaClrObjectValue>(1)) != null) {
                    binding = obj.BackingCustomObject as T;
                }

                if (binding == null && (obj = TryGetClrObject<LuaClrObjectValue>(2)) != null) {
                    binding = obj.BackingCustomObject as T;
                }

                if (binding == null) {
                    throw new LuaException("Binary operator not found for CLR object.");
                }

                var left = Wrap(1);
                toDispose.Add(left);

                var right = Wrap(2);
                toDispose.Add(right);

                var result = oper(binding, left, right);
                toDispose.Add(result);

                Push(result);
                return 1;
            });
        }

        public LuaClrObjectReference CreateClrObjectReference(LuaClrObjectValue obj)
        {
            if (obj == null) { throw new ArgumentNullException("obj"); }

            Push(obj);

            var wrap = Wrap(-1);

            LuaApi.lua_pop(LuaState, 1);

            return (LuaClrObjectReference)wrap;
        }

#if (__IOS__ || MONOTOUCH)
        [MonoTouch.MonoPInvokeCallback(typeof(LuaApi.lua_CFunction))]
#endif
        private static int ClrObjectGcCallbackWrapper(IntPtr state)
        {
            var runtime = GetSelf(state, LuaApi.lua_upvalueindex(1));

            // If it's null then the runtime has already been finalized.  In that case, all objects are already eligible
            // for collection anyway and we can just do nothing.
            if (runtime == null) { return 0; }

            return runtime.ClrObjectGcCallback(state);
        }

        private int ClrObjectGcCallback(IntPtr state)
        {
            // Don't CheckDisposed() here... we were called from Lua, so lua_close() could not have been called yet.

            var reference = TryGetReference(1);
            if (!reference.HasValue) {
                // Not good, but what can we do?
                return 0;
            }

            var obj = objectReferenceManager.GetReference(reference.Value);
            
            objectReferenceManager.DestroyReference(reference.Value);

            if (obj != null) {
                var finalizedBinding = obj.BackingCustomObject as ILuaFinalizedBinding;

                if (finalizedBinding != null) {
                    try { finalizedBinding.Finalized(this); }
                    catch { }
                }
            }

            return 0;
        }

        public LuaTable CreateTable()
        {
            CheckDisposed();

            LuaApi.lua_newtable(LuaState);
            var wrap = Wrap(-1);
            LuaApi.lua_pop(LuaState, 1);

            return (LuaTable)wrap;
        }

        // Useful when building an array; saves two p/invoke calls per element (table push and pop).
        public LuaTable CreateTable(IEnumerable<LuaValue> values)
        {
            if (values == null) { throw new ArgumentNullException("values"); }

            CheckDisposed();

            LuaApi.lua_newtable(LuaState);

            int i = 1;
            foreach (var v in values) {
                Push(v);
                LuaApi.lua_rawseti(LuaState, -2, i);
                ++i;
            }

            var wrap = Wrap(-1);
            LuaApi.lua_pop(LuaState, 1);

            return (LuaTable)wrap;
        }

        private int? CheckOnMainThread(IntPtr state)
        {
            if (state != GetMainThread(state)) {
                LuaApi.lua_pushboolean(state, 0);
                LuaApi.lua_pushstring(state, "Cannot enter the CLR from inside of a Lua coroutine.");
                return 2;
            }

            return null;
        }

#if (__IOS__ || MONOTOUCH)
        [MonoTouch.MonoPInvokeCallback(typeof(LuaApi.lua_CFunction))]
#endif
        private static int MethodWrapperCallCallbackWrapper(IntPtr state)
        {
            var runtime = GetSelf(state, LuaApi.lua_upvalueindex(1));

            if (runtime == null) {
                // The runtime doesn't exist, so Lua code shouldn't even be running now, Just return nothing at all.
                // This will be seen as an error by the bindings (assuming the bindings even still exist in memory), but
                // without any error message.

                return 0;
            }

            return runtime.MethodWrapperCallCallback(state);
        }

        private int MethodWrapperCallCallback(IntPtr state)
        {
            // We need to do this check as early as possible to avoid using the wrong state pointer.
            {
                var ret = CheckOnMainThread(state);
                if (ret.HasValue) { return ret.Value; }
            }

            OnEnterClr();
            try {
                var wrapper = (MethodWrapper)(GetClrObject<LuaClrObjectValue>(LuaApi.lua_upvalueindex(2)).ClrObject);

                return MakeManagedCall(state, wrapper);
            } finally {
                OnEnterLua();
            }
        }

        public LuaFunction CreateFunctionFromDelegate(Delegate d)
        {
            if (d == null) { throw new ArgumentNullException("d"); }

            return CreateFunctionFromMethodWrapper(new MethodWrapper(d));
        }

        internal LuaFunction CreateFunctionFromMethodWrapper(MethodWrapper wrapper)
        {
            if (wrapper == null) { throw new ArgumentNullException("wrapper"); }

            CheckDisposed();

            var top = LuaApi.lua_gettop(LuaState);

            try {
                Push(createManagedCallWrapper);

                PushSelf();
                Push(new LuaOpaqueClrObject(wrapper));
                LuaApi.lua_pushcclosure(LuaState, methodWrapperCallCallbackWrapper, 2);

                if (LuaApi.lua_pcall(LuaState, 1, 1, 0) != 0) {
                    throw new InvalidOperationException("Unable to create wrapper function.");
                }

                return (LuaFunction)Wrap(-1);
            } finally {
                LuaApi.lua_settop(LuaState, top);
            }
        }

        // Helper for handling the transition period when Lua calls into the CLR.
        //
        // Delegate should return the number of arguments it pushed.
        private delegate int LuaToClrBoundaryCallback(IList<IDisposable> toDispose);

        private int LuaToClrBoundary(IntPtr state, LuaToClrBoundaryCallback callback)
        {
            // We need to do this check as early as possible to avoid using the wrong state pointer.
            {
                var ret = CheckOnMainThread(state);
                if (ret.HasValue) { return ret.Value; }
            }

            var toDispose = new List<IDisposable>();

            var oldTop = LuaApi.lua_gettop(LuaState);

            OnEnterClr();
            try {
                // Pre-push the success flag.
                LuaApi.lua_pushboolean(LuaState, 1);

                return callback(toDispose) + 1;
            } catch (LuaException ex) {
                // If something bad happens, we can't be sure how much space is left on the stack.  Lua guarantees 20
                // free slots from the top, so restore the top back to the initial value to make sure we have enough
                // space to report the error.
                //
                // The same thing goes for the other exception handler.
                LuaApi.lua_settop(state, oldTop);

                LuaApi.lua_pushboolean(LuaState, 0);
                LuaApi.lua_pushstring(LuaState, ex.Message);
                return 2;
            } catch (Exception ex) {
                LuaApi.lua_settop(state, oldTop);

                LuaApi.lua_pushboolean(state, 0);
                LuaApi.lua_pushstring(state, "Uncaught CLR exception at Lua->CLR boundary: " + ex.ToString());
                return 2;
            } finally {
                try {
                    foreach (var i in toDispose) {
                        if (i != null) {
                            i.Dispose();
                        }
                    }
                } finally {
                    // If something bad happens while disposing stuff that's okay... but we CAN'T skip this, or Lua code
                    // running under a MemoryConstrainedLuaRuntime would be able to allocate more memory than the limit.
                    OnEnterLua();
                }
            }
        }

        private int MakeManagedCall(IntPtr state, MethodWrapper wrapper)
        {
            var toDispose = new List<IDisposable>();

            try {
                // As with Call(), we are crossing a Lua<->CLR boundary, so release any references that have been 
                // queued to be released.
                ProcessReleasedReferences();

                var nargs = LuaApi.lua_gettop(state);

                // By Lua convention, extra arguments are ignored.  For omitted/nil arguments, we will first see if the
                // managed argument declaration specifies a default value.  Otherwise, for reference/nullable arguments,
                // we will pass null (by Lua convention).  Otherwise, we will raise an error.
                //
                // For numeric types will try to be smart and convert the argument, if possible.
                var parms = wrapper.Method.GetParameters();
                object[] args;

                LuaValue wrapped;

                if (parms.Length == 1 && parms[0].ParameterType == typeof(LuaVararg)) {
                    // Special case: wrap all arguments into a vararg.
                    //
                    // We still use toDispose instead of disposing the vararg later, because any exception thrown from
                    // Wrap() could leak some objects.  It's safer to add the wrapped objects to toDisposed as we
                    // create them to prevent this possibility.
                    var varargs = new LuaValue[nargs];

                    for (int i = 0; i < nargs; ++i) {
                        varargs[i] = wrapped = Wrap(i + 1);
                        toDispose.Add(wrapped);
                    }

                    // "Retain ownership" is true here because we don't want references copied.  Since we don't call
                    // Dispose() on the vararg, they won't be disposed anyway.  This is what we want.  (The finally
                    // block will take care of that.)
                    args = new object[] { new LuaVararg(varargs, true) };
                } else {
                    args = new object[parms.Length];

                    for (int i = 0; i < parms.Length; ++i) {
                        var ptype = parms[i].ParameterType;

                        var luaType = i >= nargs ? LuaApi.LuaType.None : LuaApi.lua_type(state, i + 1);

                        switch (luaType) {
                            case LuaApi.LuaType.None:
                            case LuaApi.LuaType.Nil:
                                // Omitted/nil argument.
                                if (parms[i].IsOptional) {
                                    args[i] = parms[i].DefaultValue;
                                } else if (!ptype.IsValueType || (ptype.IsGenericType && ptype.GetGenericTypeDefinition() == typeof(Nullable<>))) {
                                    args[i] = null;
                                } else {
                                    throw new LuaException(string.Format("Argument {0} is not optional.", i + 1));
                                }
                                break;

                            case LuaApi.LuaType.Boolean:
                                // Bool means bool.
                                if (!ptype.IsAssignableFrom(typeof(bool))) {
                                    throw new LuaException(string.Format("Argument {0}: Cannot be bool.", i + 1));
                                }

                                args[i] = LuaApi.lua_toboolean(state, i + 1) != 0;
                                break;

                            case LuaApi.LuaType.Function:
                                if (!ptype.IsAssignableFrom(typeof(LuaFunction))) {
                                    throw new LuaException(string.Format("Argument {0}: Cannot be a function.", i + 1));
                                }

                                args[i] = wrapped = Wrap(i + 1);
                                toDispose.Add(wrapped);
                                break;

                            case LuaApi.LuaType.LightUserdata:
                                if (ptype.IsAssignableFrom(typeof(LuaLightUserdata))) {
                                    args[i] = wrapped = Wrap(i + 1);
                                    toDispose.Add(wrapped);
                                } else {
                                    throw new LuaException(string.Format("Argument {0}: Cannot be light userdata.", i + 1));
                                }
                                break;

                            case LuaApi.LuaType.Number:
                                try {
                                    args[i] = Convert.ChangeType(LuaApi.lua_tonumber(state, i + 1), ptype);
                                } catch {
                                    throw new LuaException(string.Format("Argument {0}: Cannot be a number.", i + 1));
                                }
                                break;

                            case LuaApi.LuaType.String:
                                if (!ptype.IsAssignableFrom(typeof(string))) {
                                    throw new LuaException(string.Format("Argument {0}: Cannot be a string.", i + 1));
                                }

                                args[i] = LuaApi.lua_tostring(state, i + 1);
                                break;

                            case LuaApi.LuaType.Table:
                                if (!ptype.IsAssignableFrom(typeof(LuaTable))) {
                                    throw new LuaException(string.Format("Argument {0}: Cannot be a table.", i + 1));
                                }

                                args[i] = wrapped = Wrap(i + 1);
                                toDispose.Add(wrapped);
                                break;

                            case LuaApi.LuaType.Thread:
                                if (!ptype.IsAssignableFrom(typeof(LuaThread))) {
                                    throw new LuaException(string.Format("Argument {0}: Cannot be a thread.", i + 1));
                                }

                                args[i] = wrapped = Wrap(i + 1);
                                toDispose.Add(wrapped);
                                break;

                            case LuaApi.LuaType.Userdata:
                                // With CLR objects, we have ambiguity.  We could test if the parameter type is
                                // compatible with LuaUserdata first, and if so wrap the Lua object.  But, perhaps the
                                // opaque object IS a LuaUserdata instance?  There's really no way to be smart in that
                                // situation.  Therefore, we will just unwrap any CLR object and pray that was the
                                // right thing to do.  (Especially since it's kind of silly to hand Lua code userdata
                                // wrapped in userdata.  Further, we are trying to map to CLR types; if code wants Eluant
                                // objects then it should take a LuaVararg instead.)
                                LuaClrObjectValue clrObject;
                                if ((clrObject = TryGetClrObject<LuaClrObjectValue>(i + 1)) != null) {
                                    args[i] = clrObject.ClrObject;
                                } else if (ptype.IsAssignableFrom(typeof(LuaUserdata))) {
                                    args[i] = wrapped = Wrap(i + 1);
                                    toDispose.Add(wrapped);
                                } else {
                                    throw new LuaException(string.Format("Argument {0}: Cannot be userdata.", i + 1));
                                }

                                break;

                            default:
                                throw new LuaException(string.Format("Argument {0}: Cannot proxy Lua type {1}.", i + 1, luaType));
                        }
                    }
                }

                object ret;
                try {
                    ret = wrapper.Invoke(args);
                } catch (MemberAccessException) {
                    throw new LuaException("Invalid argument(s).");
                } catch (TargetInvocationException ex) {
                    if (ex.InnerException is LuaException) {
                        throw ex.InnerException;
                    }
                    throw;
                }

                // Process any released references again.
                ProcessReleasedReferences();

                // If the method was declared to return void we can just stop now.
                if (wrapper.Method.ReturnType == typeof(void)) {
                    LuaApi.lua_pushboolean(state, 1);
                    return 1;
                }

                // If a vararg is returned, unpack the results.
                var retVararg = ret as LuaVararg;
                if (retVararg != null) {
                    // We do need to dispose the vararg.  If the calling code wants to retain references then it can
                    // pass takeOwnership:false to the LuaVararg constructor.  If we didn't dispose of it then the
                    // called method would have no way to dispose of references that it didn't need anymore.
                    toDispose.Add(retVararg);

                    LuaApi.lua_pushboolean(state, 1);

                    if (LuaApi.lua_checkstack(LuaState, 1 + retVararg.Count) == 0) {
                        throw new LuaException("Cannot grow stack for results.");
                    }

                    foreach (var a in retVararg) {
                        Push(a);
                    }

                    return retVararg.Count + 1;
                }

                var retValue = AsLuaValue(ret);
                if (retValue == null) {
                    throw new LuaException(string.Format("Unable to convert object of type {0} to Lua value.",
                                                         ret.GetType().FullName));
                }

                // Similar to the vararg case, we always dispose the returned value object.
                //
                // 1. If we created it ourselves, we need to dispose of it anyway.
                //
                // 2. If the callee created an object with the sole purpose of being returned (tables are probably a
                //    common case of that) they would have no way to dispose of the CLR reference to the object.  So we
                //    do that here.  (If they didn't want the reference disposed they could return value.CopyHandle().)
                toDispose.Add(retValue);

                LuaApi.lua_pushboolean(state, 1);
                Push(retValue);

                return 2;
            } catch (LuaException ex) {
                LuaApi.lua_pushboolean(state, 0);
                LuaApi.lua_pushstring(state, ex.Message);
                return 2;
            } catch (Exception ex) {
                LuaApi.lua_pushboolean(state, 0);
                LuaApi.lua_pushstring(state, "Uncaught CLR exception at Lua->CLR boundary: " + ex.ToString());
                return 2;
            } finally {
                // Dispose whatever we need to.  It's okay to dispose result objects, as that will only release the CLR
                // reference to them; they will still be alive on the Lua stack.
                foreach (var o in toDispose) {
                    if (o != null) {
                        o.Dispose();
                    }
                }
            }
        }

        public LuaValue AsLuaValue(object obj)
        {
            CheckDisposed();

            if (obj == null) {
                return LuaNil.Instance;
            }

            var luaValue = obj as LuaValue;
            if (luaValue != null) {
                return luaValue;
            }

            if (obj is bool) {
                return (LuaBoolean)(bool)obj;
            }

            var delegateObject = obj as Delegate;
            if (delegateObject != null) {
                return CreateFunctionFromDelegate(delegateObject);
            }

            var str = obj as string;
            if (str != null) {
                return (LuaString)str;
            }

            try {
                return (LuaNumber)(double)Convert.ChangeType(obj, typeof(double));
            } catch { }

            return null;
        }

        public LuaWeakReference<T> CreateWeakReference<T>(T reference)
            where T : LuaReference
        {
            CheckDisposed();

            if (reference == null) { throw new ArgumentNullException("reference"); }

            reference.CheckDisposed();
            reference.AssertRuntimeIs(this);

            LuaApi.lua_newtable(LuaState);

            LuaApi.luaL_getmetatable(LuaState, WEAKREFERENCE_METATABLE);
            LuaApi.lua_setmetatable(LuaState, -2);

            Push(reference);
            LuaApi.lua_rawseti(LuaState, -2, 1);

            var refTable = (LuaTable)Wrap(-1);
            LuaApi.lua_pop(LuaState, 1);

            return new LuaWeakReference<T>(refTable);
        }

        internal void PushWeakReference<T>(LuaWeakReference<T> reference)
            where T : LuaReference
        {
            CheckDisposed();

            Push(reference.WeakTable);
            LuaApi.lua_rawgeti(LuaState, -1, 1);
        }

        internal T GetWeakReference<T>(LuaWeakReference<T> reference)
            where T : LuaReference
        {
            PushWeakReference(reference);

            var wrapped = Wrap(-1);

            LuaApi.lua_pop(LuaState, 2);

            if (wrapped == LuaNil.Instance) {
                return null;
            }

            return (T)wrapped;
        }

        private class ObjectReferenceManager<T> where T : class
        {
            private Dictionary<int, T> references = new Dictionary<int, T>();
            private int nextReference = 1;

            public ObjectReferenceManager() { }

            public T GetReference(int reference)
            {
                if (reference == 0) {
                    return null;
                }

                T obj;
                if (!references.TryGetValue(reference, out obj)) {
                    throw new InvalidOperationException("No such reference: " + reference);
                }

                return obj;
            }

            public void DestroyReference(int reference)
            {
                references.Remove(reference);

                nextReference = Math.Min(nextReference, reference);
            }

            public int CreateReference(T obj)
            {
                if (obj == null) {
                    return 0;
                }

                var start = nextReference;
                while (references.ContainsKey(nextReference)) {
                    if (nextReference == int.MaxValue) {
                        nextReference = 1;
                    } else {
                        ++nextReference;
                    }

                    if (nextReference == start) {
                        throw new InvalidOperationException("Reference key space exhausted.");
                    }
                }

                references[nextReference] = obj;

                return nextReference;
            }
        }

        // Delegate-like, but doesn't need a particular delegate type to do its work (which would be a problem for
        // functions auto-generated from a CLR object method).
        internal class MethodWrapper
        {
            public object Target { get; private set; }
            public MethodInfo Method { get; private set; }

            public MethodWrapper(object target, MethodInfo method)
            {
                if (method == null) { throw new ArgumentNullException("method"); }

                Target = target;
                Method = method;
            }

            public MethodWrapper(Delegate d)
            {
                if (d == null) { throw new ArgumentNullException("d"); }

                Target = d.Target;
                Method = d.Method;
            }

            public object Invoke(params object[] parms)
            {
                return Method.Invoke(Target, parms);
            }
        }
    }
}


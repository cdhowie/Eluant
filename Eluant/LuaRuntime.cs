using System;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Eluant
{
    public class LuaRuntime : IDisposable
    {
        protected internal delegate IntPtr LuaAllocator(IntPtr ud, IntPtr ptr, IntPtr osize, IntPtr nsize);

        internal IntPtr LuaState { get; private set; }

        // Separate field for the corner case where customAllocator was collected first.
        private bool hasCustomAllocator = false;
        private LuaAllocator customAllocator;

        private const string MAIN_THREAD_KEY = "eluant_main_thread";
        private const string REFERENCES_KEY = "eluant_references";

        private const string WEAKREFERENCE_METATABLE = "eluant_weakreference";

        private const string OPAQUECLROBJECT_METATABLE = "eluant_opaqueclrobject";
        private LuaApi.lua_CFunction opaqueClrObjectGcCallback;

        private LuaApi.lua_CFunction delegateWrapperCallCallback;

        private LuaFunction createManagedCallWrapper;

        private ConcurrentQueue<int> releasedReferences = new ConcurrentQueue<int>();

        public LuaRuntime()
        {
            customAllocator = CreateAllocatorDelegate();

            if (customAllocator != null) {
                hasCustomAllocator = true;
                LuaState = LuaApi.lua_newstate(customAllocator, IntPtr.Zero);
            } else {
                hasCustomAllocator = false;
                LuaState = LuaApi.luaL_newstate();
            }

            Globals = new LuaGlobalsTable(this);

            Initialize();
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
        protected virtual LuaAllocator CreateAllocatorDelegate()
        {
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

        private void Initialize()
        {
            PreInitialize();

            LuaApi.lua_newtable(LuaState);
            LuaApi.lua_setfield(LuaState, LuaApi.LUA_REGISTRYINDEX, REFERENCES_KEY);

            LuaApi.luaL_openlibs(LuaState);

            LuaApi.lua_pushlightuserdata(LuaState, LuaState);
            LuaApi.lua_setfield(LuaState, LuaApi.LUA_REGISTRYINDEX, MAIN_THREAD_KEY);

            opaqueClrObjectGcCallback = OpaqueClrObjectGcCallback;

            LuaApi.luaL_newmetatable(LuaState, OPAQUECLROBJECT_METATABLE);

            LuaApi.lua_pushstring(LuaState, "__gc");
            LuaApi.lua_pushcfunction(LuaState, opaqueClrObjectGcCallback);
            LuaApi.lua_settable(LuaState, -3);

            LuaApi.lua_pushstring(LuaState, "__metatable");
            LuaApi.lua_pushboolean(LuaState, 0);
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

            delegateWrapperCallCallback = DelegateWrapperCallCalback;

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
                    // critical nature (or things that the OS won't do when the runtime process quits, anyway).  This
                    // implies that GCHandles allocated for opaque CLR object references will not be freed, either.
                    //
                    // Consumers should make sure that they dispose Lua runtimes before the CLR begins shutting down to
                    // avoid this scenario.
                    LuaState = IntPtr.Zero;
                } else {
                    LuaApi.lua_close(LuaState);
                    LuaState = IntPtr.Zero;
                }
            }
        }

        protected void CheckDisposed()
        {
            if (LuaState == IntPtr.Zero) {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public LuaGlobalsTable Globals { get; private set; }

        /*public LuaValue this[LuaValue key]
        {
            get {
                CheckDisposed();

                var top = LuaApi.lua_gettop(LuaState);

                try {
                    LuaApi.lua_pushvalue(LuaState, LuaApi.LUA_GLOBALSINDEX);
                    Push(key);
                    LuaApi.lua_gettable(LuaState, -2);

                    return Wrap(-1);
                } finally {
                    LuaApi.lua_settop(LuaState, top);
                }
            }
            set {
                CheckDisposed();

                var top = LuaApi.lua_gettop(LuaState);

                try {
                    LuaApi.lua_pushvalue(LuaState, LuaApi.LUA_GLOBALSINDEX);
                    Push(key);
                    Push(value);
                    LuaApi.lua_settable(LuaState, -3);
                } finally {
                    LuaApi.lua_settop(LuaState, top);
                }
            }
        }*/

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
                    if (HasMetatable(index, OPAQUECLROBJECT_METATABLE)) {
                        return new LuaOpaqueClrObjectReference(this, CreateReference(index));
                    }

                    return new LuaLightUserdata(this, CreateReference(index));

                case LuaApi.LuaType.Userdata:
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

        internal void PushOpaqueClrObject(object obj)
        {
            var objPtr = (IntPtr)GCHandle.Alloc(obj);

            if (objPtr == IntPtr.Zero) {
                throw new InvalidOperationException("Could not allocate GC handle to object.");
            }

            LuaApi.lua_pushlightuserdata(LuaState, objPtr);
            LuaApi.luaL_getmetatable(LuaState, OPAQUECLROBJECT_METATABLE);
            LuaApi.lua_setmetatable(LuaState, -2);
        }

        internal object GetOpaqueClrObject(int index)
        {
            CheckDisposed();

            // Make sure this is an opaque object!
            //
            // This test can fail if we are on a Lua thread, since we will be using the wrong Lua state.  In practice we
            // should have already checked to make sure we are not on a different Lua thread, but this is here as an
            // additional safeguard since there are potential security implications if things go wrong here.
            if (LuaApi.lua_type(LuaState, index) == LuaApi.LuaType.LightUserdata) {
                LuaApi.lua_getmetatable(LuaState, index);
                LuaApi.luaL_getmetatable(LuaState, OPAQUECLROBJECT_METATABLE);

                var hasCorrectMetatable = LuaApi.lua_rawequal(LuaState, -1, -2) != 0;

                LuaApi.lua_pop(LuaState, 2);

                if (hasCorrectMetatable) {
                    var handle = (GCHandle)LuaApi.lua_touserdata(LuaState, index);
                    return handle.Target;
                }
            }

            throw new InvalidOperationException("Attempt to obtain CLR object from a Lua object that does not represent a CLR object.");
        }

        public LuaOpaqueClrObjectReference CreateOpaqueClrObjectReference(object obj)
        {
            CheckDisposed();

            PushOpaqueClrObject(obj);

            var wrap = Wrap(-1);

            LuaApi.lua_pop(LuaState, 1);

            return (LuaOpaqueClrObjectReference)wrap;
        }

        private int OpaqueClrObjectGcCallback(IntPtr state)
        {
            // Don't CheckDisposed() here... we were called from Lua, so lua_close() could not have been called yet.

            var handle = (GCHandle)LuaApi.lua_touserdata(state, 1);
            handle.Free();

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

        private int DelegateWrapperCallCalback(IntPtr state)
        {
            // We need to do this check as early as possible to avoid using the wrong state pointer.
            if (state != GetMainThread(state)) {
                LuaApi.lua_pushboolean(state, 0);
                LuaApi.lua_pushstring(state, "Cannot enter the CLR from inside of a Lua coroutine.");
                return 2;
            }

            OnEnterClr();
            try {
                var d = (Delegate)GetOpaqueClrObject(LuaApi.lua_upvalueindex(1));

                return MakeManagedCall(state, d);
            } finally {
                OnEnterLua();
            }
        }

        public LuaFunction CreateFunctionFromDelegate(Delegate d)
        {
            if (d == null) { throw new ArgumentNullException("d"); }

            CheckDisposed();

            var top = LuaApi.lua_gettop(LuaState);

            try {
                Push(createManagedCallWrapper);

                PushOpaqueClrObject(d);
                LuaApi.lua_pushcclosure(LuaState, delegateWrapperCallCallback, 1);

                if (LuaApi.lua_pcall(LuaState, 1, 1, 0) != 0) {
                    throw new InvalidOperationException("Unable to create delegate wrapper.");
                }

                return (LuaFunction)Wrap(-1);
            } finally {
                LuaApi.lua_settop(LuaState, top);
            }
        }

        private int MakeManagedCall(IntPtr state, Delegate d)
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
                var parms = d.Method.GetParameters();
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
                                // With opaque CLR objects, we have ambiguity.  We could test if the parameter type is
                                // compatible with LuaLightUserdata first, and if so wrap the Lua object.  But, perhaps the
                                // opaque object IS a LuaLightUserdata instance?  There's really no way to be smart in that
                                // situation.  Therefore, we will just unwrap any opaque CLR object and pray that was the
                                // right thing to do.  (Especially since it's kind of silly to hand Lua code userdata
                                // wrapped in userdata.  Further, we are trying to map to CLR types; if code wants Eluant
                                // objects then it should take a LuaVararg instead.)
                                if (HasMetatable(i + 1, OPAQUECLROBJECT_METATABLE)) {
                                    args[i] = GetOpaqueClrObject(i + 1);
                                } else if (ptype.IsAssignableFrom(typeof(LuaLightUserdata))) {
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
                                if (!ptype.IsAssignableFrom(typeof(LuaUserdata))) {
                                    throw new LuaException(string.Format("Argument {0}: Cannot be userdata.", i + 1));
                                }

                                args[i] = wrapped = Wrap(i + 1);
                                toDispose.Add(wrapped);
                                break;

                            default:
                                throw new LuaException(string.Format("Argument {0}: Cannot proxy Lua type {1}.", i + 1, luaType));
                        }
                    }
                }

                object ret;
                try {
                    ret = d.DynamicInvoke(args);
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
                if (d.Method.ReturnType == typeof(void)) {
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

                    if (LuaApi.lua_checkstack(LuaState, retVararg.Count) == 0) {
                        throw new LuaException("Cannot grow stack for results.");
                    }

                    foreach (var a in retVararg) {
                        Push(a);
                    }

                    return retVararg.Count + 1;
                }

                var retValue = AsLuaValue(ret, true);

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
                    o.Dispose();
                }
            }
        }

        public LuaValue AsLuaValue(object obj, bool permitBox)
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

            if (permitBox) {
                return new LuaOpaqueClrObject(obj);
            }

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
    }
}


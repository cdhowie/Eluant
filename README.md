Eluant
======

Eluant is a set of bindings that exposes Lua to the Common Language Runtime for consumption by managed languages like C#, VB.Net, and F#.

Motivation
----------

Eluant has a strong focus on proper memory management, as many existing Lua-to-CLR bindings tend to leak memory due to improper typing of Lua objects.  (For example, LuaInterface/NLua return call results as `object[]`, which implies that the caller doesn't have to do anything special with the result set, when in fact any disposable objects in the set need to be disposed or a Lua object is **permanently** leaked.)

Further, exsiting bindings tend to call `lua_error()` to trigger errors, which causes a longjmp.  This action can corrupt information that the CLR has regarding the stack, and skips over `finally` blocks!  Eluant tries very hard to make sure that Lua errors are not raised in or across CLR stack frames.

It is also a goal that Eluant should work with an unmodified build of the Lua C library.

Quick Start
===========

Make sure that you have Lua 5.1 installed on your machine.  If on Linux, you may need to install the development package in order for the CLR to find the Lua 5.1 library.  (If you have issues with DllNotFoundException being thrown, make sure that `liblua5.1.so` or `lua5.1.dll` are in your dynamic library search path.)

After building Eluant, run the Eluant.Tests test suite to verify that the CLR can find Lua.

Using Eluant is straightforward in a lot of respects.  Make sure that you dispose of Lua references, however, as not doing so will lead to sub-optimal GC performance, both in Lua and the CLR.

    using System;
    using Eluant;

    class Program
    {
        static void Main()
        {
            using (var runtime = new LuaRuntime()) {
                using (var fn = runtime.CreateFunctionFromDelegate(new Func<int, int>(x => x * x))) {
                    runtime.Globals["square"] = fn;
                }

                runtime.DoString("print(square(4))").Dispose();
            }   
        }
    }

Output:

    16

Lua Wrapper Class Heirarchy
===========================

To facilitate using Lua values in the CLR, there are a series of classes that mimic Lua types at a conceptual level.  `LuaValue` is the root of this heirarchy.

    + LuaValue
        + LuaReference
            + LuaFunction
            + LuaLightUserdata
            + LuaTable
            + LuaThread
            + LuaUserdata
                + LuaClrObjectReference
        + LuaValueType
            + LuaBoolean
            + LuaClrObjectValue
                + LuaCustomClrObject
                + LuaOpaqueClrObject
            + LuaNil
            + LuaNumber
            + LuaString
        + LuaWeakReference<T>
    + LuaVararg
    + IClrObject

There is another type, `LuaVararg`, that does not inherit from `LuaValue`, just as varargs in Lua (`...`) are not first-class Lua values.  `LuaVararg` is provided as a way to facilitate proper memory management when dealing with the results of Lua function calls, as well as a way to accept and return multiple values from delegates.  (More on both topics later.)

`LuaReference` is the root for types that hold a reference to a Lua object.  These objects should be disposed when the reference is no longer needed, or the Lua garbage collector will have to wait for both the CLR garbage collector *and* Eluant before it can collect the object.  As long as the `LuaReference` object is alive in the CLR, the corresponding Lua object is guaranteed to exist in the Lua runtime.

`LuaValueType` is the root for types that are non-reference values in Lua.  These objects do not need to be disposed, as there is no reference held.  (Strings are technically objects in Lua, but are automatically interned by the Lua runtime such that two equal strings refer to the same object.  For that reason, it is not necessary to retain references to Lua strings.)

`LuaBoolean` and `LuaNil` objects cannot be created.  There is a set of singletons that can be used: `LuaBoolean.False`, `LuaBoolean.True`, and `LuaNil.Instance`.  Since these types are very common and have an extremely limited value domain, reusing the same objects saves memory and strain on the CLR garbage collector.

There is special support for the Lua `nil` value in the form of `LuaNil`.  When Eluant converts `nil` to `LuaValue`, it always returns an instance of `LuaNil`.  This means that `LuaValue` objects obtained from Eluant will not be null!  An extension method `IsNil()` is provided to fill this gap, and will return true when the `LuaValue` object is null or is the `LuaNil` singleton.  However, note that Eluant will accept a null reference to a `LuaValue` as being equivalent to a reference to a `LuaNil` object, so you don't have to be as careful when passing values into Eluant.

`LuaNumber` can be treated like a `Nullable<Double>` (`double?` in C#).  It has operator overloads that allow it to be used in arithmetic with any other `LuaNumber` object or CLR numeric type that could normally be used with a double or nullable double, as well as an implicit conversion to and from a nullable double.  Null `LuaNumber` objects act exactly like null nullable doubles.

`LuaClrObjectValue` is the base type for wrappers around CLR objects.  Eluant will ensure that CLR objects are not eligible for garbage collection in the CLR until all Lua values referencing them are released.  The different subtypes all have different semantics with regards to how Lua code can interact with the CLR object.  See the section "Exposing CLR Objects to Lua" for more information.

`LuaClrObjectReference` refers to a specific instance of a Lua value that wraps a CLR object.  This allows you to give Lua not only a wrapper object that is equivalent to the one it already has, but the exact same wrapper object it already has.  All Lua wrappers around CLR objects that are given to CLR code as a `LuaValue` will be instances of this type, which gives you access to the Eluant wrapper object as well as directly to the CLR object contained in the Eluant wrapper object.

The `IClrObject` interface is provided as a way for code to generically accept any of the Eluant types that wrap CLR objects without caring which one was used to provide the object to Lua.  All Eluant wrapper types that correspond to a CLR object implement this interface.

`LuaWeakReference<T>` implements weak references to Lua objects.  See the section "Weak CLR References to Lua Objects" for details.

Memory Management
=================

CLR References to Lua Objects
-----------------------------

Managing memory is important.  Since Lua has a garbage collector, it is free to destroy reference objects like tables and functions any time that such objects are not referenced from within Lua.  This creates a problem, since we would like to expose such references as CLR objects without fear that the Lua garbage collector will destroy the object while managed code is using it.

Eluant solves this by holding an in-Lua reference to such objects for the life of the corresponding CLR objects.  To make this efficient, Eluant needs to know when you are no longer using the CLR object so that it can release the Lua reference and make it eligible for collection by Lua's garbage collector.  This is accomplished by disposing such CLR objects.  It is important to note that disposing of these CLR wrappers does *not* cause the Lua object to be destroyed, but only releases a reference to it; the Lua object will only be collected when no Lua or CLR references exist to the object.

Note that any Lua value that inherits from LuaValueType does not need to be disposed, and is a lightweight wrapper around a CLR value.  For example, LuaNumber and LuaString are both value types from the perspective of Lua and therefore need not be disposed.  However, LuaFunction and LuaTable are.  You will note in the "Quick Start" example code that the return value from CreateFunctionFromDelegate() is disposed (via the `using` block).  This is necessary because the `fn` object is holding onto a reference to this Lua function object.  After the function has been stored in a Lua global, we no longer need the CLR reference and we can destroy it.

Take note of these patterns, as they will cause short-term leaks:

    // Leaks if the Lua 'bar' global is of a reference type.
    runtime.Globals["foo"] = runtime.Globals["bar"];

    // Leaks a reference to the Lua function object.
    runtime.Globals["foo"] = runtime.CreateFunctionFromDelegate(new Action(() => {}));

    // Leaks many ways:
    //
    // 1. The LuaFunction reference is not disposed.
    // 2. If the 'bar' global is of a reference type, its reference is not disposed.
    // 3. The result list from the call is not disposed, leaking any references it contains.
    ((LuaFunction) runtime.Globals["foo"]).Call(runtime.Globals["bar"]);

The finalizers for Lua reference objects will ensure that the reference is properly released at an unspecified future time.  Eluant cannot release such references immediately upon CLR object finalization, because Lua is not thread-safe.  Finalized references are queued to be released at a later time.  Between the time the reference is leaked and the time that Eluant collects the reference, the Lua object is not eligible for collection.  When the object is explicitly disposed, however, Eluant assumes that the disposal happened on the thread with control of the Lua runtime, and immediately releases the reference to the Lua object.  (One should not dispose of Lua references while another thread is using the runtime.  See the "Thread Safety" section.)

Disposing a `LuaRuntime` will destroy all Lua objects created within the context of that runtime and free all unmanaged memory allocated by the runtime.  This invalidates all `LuaReference` objects created from that runtime.

One can call `CopyReference()` on any `LuaValue` object to create a copy of any contained reference.  The object returned by this method will reference the same Lua object, but have an independent lifetime from the original reference.  (`LuaValueType` objects will simply return themselves from this method call, since they are not references and disposing them has no effect.)

`LuaReference`-derived objects are bound to a particular runtime and may not be used within the context of another runtime.  Doing so will cause an exception.  For example:

    using (var runtime1 = new LuaRuntime())
    using (var runtime2 = new LuaRuntime()) {
        using (var table = runtime1.CreateTable()) {
            runtime2.Globals["foo"] = table;    // InvalidOperationException
        }
    }

Weak CLR References to Lua Objects
----------------------------------

`LuaReference` implements strong references that keep the target Lua object alive.  Eluant also supports weak references via `LuaWeakReference<T>`.  Such an object references a specific Lua object, but does not prevent the Lua object from being collected.  To create a weak reference, call `CreateWeakReference()` on the reference object.

`LuaWeakReference<T>.CreateReferenceToTarget()` will return a strong reference to the target object if it still exists, or a null reference if Lua has destroyed the target object.  Each call will return a new reference object, which should be disposed as with any other `LuaReference` object.  (The verbose method name serves as a reminder that a new reference is returned.)

`LuaWeakReference<T>` objects should be disposed when they are no longer needed.  While they do not keep the target object alive, there is an infrastructure object kept alive within Lua to allow the lifetime of the Lua object to be tracked.  Even after the target object has been destroyed by the Lua garbage collector, the infrastructure object will remain alive.  Disposing the `LuaWeakReference<T>` allows Lua to collect the infrastructure object.

Weak references can be passed to Lua functions or returned from delegates, as with any other `LuaValue`.  If the tracked object is alive, Lua will receive a strong reference to it, otherwise `nil`.

Lua References to CLR Objects
-----------------------------

Eluant does not (yet) support transparent interoperation between Lua and the CLR.  The only mechanism currently provided is the `LuaOpaqueClrObject` type, which wraps a CLR object in an opaque Lua object.  Lua cannot invoke methods or properties of the target object, nor read its fields.  It is, however, able to pass such an object to a CLR delegate, where it will be unwrapped by Eluant into the target CLR object.

Eluant will ensure that CLR objects referenced by Lua are not collected by the CLR garbage collector for as long as Lua has a reference to the object.  This mechanism is also used when wrapping CLR delegate objects as Lua functions, so the delegate object is guaranteed to exist for as long as the corresponding Lua function object exists.

Thread Safety
=============

Lua is not guaranteed to be thread-safe, and Eluant does not strengthen this guarantee.  The same Lua runtime must not be manipulated from multiple threads.  This includes usage of any `LuaRuntime` members as well as the members of any `LuaReference`-derived objects or `LuaWeakReference<T>` objects that refer to an object in that runtime. (`LuaValueType`-derived objects do not refer to any particular runtime and and guaranteed to be thread-safe.)

Different Lua runtimes are completely independent, and it is safe to use multiple runtimes simultaneously from different threads, as long as no two threads are using the *same* runtime at any particular moment in time.

Exposing CLR Functions to Lua
=============================

We have already seen an example of `LuaRuntime.CreateFunctionFromDelegate()`.  This method returns a `LuaFunction` object that will ultimately call the given CLR delegate.  Eluant will try to map Lua types to CLR types, though this is not always possible.

If the delegate takes exactly one parameter of type `LuaVararg` then all arguments passed from Lua will be passed in this parameter.  This is currently the only way for a CLR delegate to accept a variable number of parameters from Lua (though one can "fake it" by passing a table).  Note that the delegate does not have to dispose the `LuaVararg`; Eluant itself will do this when the delegate returns.  This means that any contained references will become invalid!  If the delegate wishes to retain a reference to any Lua object then it should call `LuaValue.CopyReference()` to create a new reference to the Lua object.

If the delegate has any other signature then Eluant will convert Lua values as necessary.  Note that, per Lua convention, extra arguments to the delegate are ignored, and if too few arguments are provided then Eluant will pretend that `nil` was provided for those that are missing -- which will cause an error *only* if an explicit `nil` otherwise would.  In these examples, `fn` represents a Lua function wrapper around a delegate that takes three parameters:

    // The following two calls are identical from the perspective of the delegate:
    fn('foo', 'bar', 42)
    fn('foo', 'bar', 42, 84)    -- The fourth argument is ignored.

    // As are all of these:
    fn('foo', nil, nil)
    fn('foo', nil)              -- The third argument is implicitly nil.
    fn('foo')                   -- As is the second argument here.

The rules for mapping arguments are as follows:

* For `nil`: If the CLR parameter is declared optional with a default value, the default value will be used.  Otherwise, the CLR `null` value will be used for any reference type or nullable type (`T?` in C#, `Nullable<T>` elsewhere).  For non-nullable value types without a default value, an error will be returned to the caller.
* For booleans: The parameter type must be compatible with `Boolean` or `Nullable<Boolean>`, or an error is returned.
* For functions: The parameter type must be compatible with `LuaFunction`, or an error is returned.  (There may be work done in the future to allow automatic wrapping of `LuaFunction` objects as CLR delegates.)
* For opaque CLR objects: The parameter type must be a reference type that is compatible with the type of the passed CLR object, or an error is returned.
* For light userdata: The parameter type must be compatible with `LuaLightUserdata`, or an error is returned.
* For numbers: If the parameter type is `Object` then a boxed `Double` will be passed.  Otherwise, the parameter type must be a numeric CLR type (this currently excludes nullable types), or an error is returned.  `Double` is the native type of Lua numbers; any other type risks overflow or loss of precision.  Conversion to integral values will round any fraction, and any overflow will cause an error to be returned.
* For strings: The parameter type must be compatible with `String`, or an error is returned.
* For tables: The parameter type must be compatible with `LuaTable`, or an error is returned.
* For threads (coroutines): The parameter type must be compatible with `LuaThread`, or an error is returned.
* For userdata: The parameter type must be compatible with `LuaUserdata`, or an error is returned.

Note that "compatible with" means that the parameter type can be directly assigned from the given type.  For example, the `Object` type is compatible with every other type, so it should meet the criteria for any Lua type.

As with the "single `LuaVararg`" case, Lua reference objects passed to the delegate will be disposed after it returns.  Use `LuaValue.CopyReference()` if the delegate needs to store a reference that should exist after the delegate returns.

If the delegate is declared to return void, then no result is returned to the caller.  (Bear in mind that "no result" is implicitly convertible to `nil` in Lua.)

In all other cases, the declared return type is ignored and the actual type of the returned value is considered:

* If it is `null` (this includes nullable value types with no value): `nil`.
* If it is a `LuaVararg`: Eluant will return the contents back to the Lua caller as multiple return values.  Eluant will also dispose of the `LuaVararg` object in this case, so if the delegate wants to return an object but also retain a reference to it then it should construct the `LuaVararg` with the `takeOwnership` argument set to `false`.  (This instructs `LuaVararg` to make copies of any references.  It is then these copies that will be disposed.)
* If it is compatible with `LuaValue`: No conversion is performed and the value is returned as-is.  If it is a Lua reference type then it will be disposed by Eluant automatically, so return a copy of any references you don't want disposed.
* If it is a `Boolean`: The corresponding Lua boolean value.
* If it is a delegate: A Lua function object that follows the same set of rules in this documentation section.
* If it is a `String`: The corresponding Lua string.
* If it is a numeric type: The corresponding Lua number.
* In all other cases: An error is raised in the calling code.

For maximum control, it is recommended that delegates intended to be called from Lua code declare a return type of `LuaValue`.  There are implicit conversions from numeric types, strings, and bools to `LuaValue`, and `null` is treated identically to a `LuaNil` object.  This allows the code author to be sure at compile-time that an error will not be returned to Lua due to an object that can't be converted to a Lua value.

All objects that Eluant disposes automatically -- arguments and return values -- will be disposed after return value processing is complete.  This means that it is safe to return from a delegate a Lua reference that was passed to that delegate.

Exception Handling
------------------

If the delegate throws a `LuaException` then the exception's message will be raised as as error from within Lua.

If the delegate throws any other type of exception, then a summary message with the value of `Exception.ToString()` will be raised as an error from within Lua.

In both cases, Eluant will dispose of any Lua references passed to the delegate (in a `LuaVararg` or as formal arguments), just as it would upon successful termination of the delegate.

Exposing CLR Objects to Lua
===========================

There are three distinct ways that CLR objects can be presented to Lua.

All CLR objects exposed to Lua through any of these mechanisms will kept alive with a strong reference in the CLR automatically.  This reference is released when the Lua garbage collector finalizes the corresponding Lua value.

Opaque CLR Objects
------------------

An opaque object (`LuaOpaqueClrObject`) is intended to allow Lua code to pass the object back to managed code while not allowing Lua code to mutate or inspect the object in any meaningful way by itself.  Such inspection or mutation can only be implemented for opaque objects by giving Lua code access to managed methods that perform that inspection or mutation.

Opaque objects cannot be manipulated directly from Lua.  Lua will receive a Lua userdata value with no user-accessible members or metamethods.  As these values are opaque, two different Lua values that wrap the same CLR object will not compare as equal.  This is intentional.

If a null CLR object reference is wrapped, it will become a Lua userdata value as with any other CLR object.  Lua code will be unable to determine if this value represents a null CLR object.  As with non-null objects, two different wrapper values around the null CLR object will not compare as equal.

Custom CLR Objects
------------------

A custom object is one implementing one or more of the metamethod binding interfaces.  The methods of these interfaces will be called when Lua code invokes the corresponding metamethod of the object.  This allows for completely custom object behaviors.

Like opaque CLR objects, two different Lua values wrapping the same CLR object will not compare as equal by default.  However, one can implement the `ILuaEqualityBinding` interface to provide custom equality logic.

Unlike opaque CLR objects, if a null CLR object reference is given to Lua as a custom object, Lua will receive it as `nil`.

Eluant supports all Lua metamethods via the following interfaces, which exist in the `Eluant.ObjectBinding` namespace:

+ `ILuaAdditionBinding` (`__add`)
+ `ILuaCallBinding` (`__call`)
+ `ILuaConcatenationBinding` (`__concat`)
+ `ILuaDivisionBinding` (`__div`)
+ `ILuaEqualityBinding` (`__eq`)
+ `ILuaExponentiationBinding` (`__pow`)
+ `ILuaFinalizedBinding` (`__gc`)
+ `ILuaLengthBinding` (`__len`)
+ `ILuaLessThanBinding` (`__lt`)
+ `ILuaLessThanOrEqualToBinding` (`__le`)
+ `ILuaModuloBinding` (`__mod`)
+ `ILuaMultiplicationBinding` (`__mul`)
+ `ILuaSubtractionBinding` (`__sub`)
+ `ILuaTableBinding` (`__index` and `__newindex`)
+ `ILuaUnaryMinusBinding` (`__unm`)

Note that methods of interfaces for binary operators (addition, subtraction, etc.) take two arguments, not one.  This is because the custom object is not always guaranteed to be the first object.  For example, if `o` is a custom CLR object then the expression `2 - o` will invoke the `__add` metamethod of the CLR object, but the CLR object will be the right-side operand.  Binary operator methods should generally not reference the object on which they are invoked, but should evaluate only their arguments.  (In other words, binary operator methods should be implemented as though they were static.)

For convenience, there is also an interface `ILuaMathBinding` that inherits all of the metamethod interfaces that would be useful when implementing custom math logic: `ILuaAdditionBinding`, `ILuaDivisionBinding`, `ILuaEqualityBinding`, `ILuaExponentiationBinding`, `ILuaLessThanBinding`, `ILuaLessThanOrEqualToBinding`, `ILuaModuloBinding`, `ILuaMultiplicationBinding`, `ILuaSubtractionBinding`, and `ILuaUnaryMinusBinding`.

Any exceptions raised from `ILuaFinalizedBinding.Finalize()` are ignored.  Any exceptions raised from any other metamethod interface method will result in a Lua error being raised in the calling code.

Transparent CLR Objects
-----------------------

A transparent object given to Lua code will allow Lua to directly invoke its methods and read and/or write to its properties and fields.  Access to specific members is governed by `IBindingSecurityPolicy`.

Two different Lua values that refer to the same transparent CLR object will compare as equal.  A null CLR object reference will be given to Lua as `nil`.

An attempt to access a member that does not exist on the CLR object will result in a `nil` value being returned to Lua.  An attempt to write to a member that either does not exist or is not a property or field will result in an error being raised in Lua.  (Note that the "return `nil`" behavior on retrieving values of members that don't exist means that one cannot distinguish between the cases where the object does not have that member, or the object's member's value is marshalled as `nil`.  However, this allows Lua code to test for the presence of features without needing to use `pcall()` and is more in line with common Lua coding styles.)

There are some rules regarding which members can be accessed from Lua code.

* Only public members marked with `LuaMemberAttribute` can be accessed.
* Indexers cannot be accessed.  Lua syntax does not distinguish between member and index look-up, as both are equivalent.  (`x.y` means the same thing as `x['y']`.)  In particular, if the CLR object defines an indexer taking one string argument, every member access from the Lua side has the potential to be ambiguous, and there is no clear way that ambiguity can be resolved.
* Open generic methods cannot be accessed.  For arguments of a generic type, there may be multiple options for a given Lua value.  (Should a string be passed as `LuaString` or `String`, for example?)  If the only reference to a generic type is the return value, Lua has no syntax for expressing what that type should be.  (Closed generic methods -- methods of a generic type that are not themselves generic -- are acceptable as their argument and return types are fully known.)
* Methods cannot be overloaded.  This restriction may be removed in a future release, once a set of overload-resolution rules is determined.  To provide the illusion of an overloaded method, declare the method to accept `LuaVararg` and inspect the arguments.

The default constructor of `LuaMemberAttribute` causes the member to be visible to Lua code using the same name as the member itself.  The attribute has a string constructor that can be used to override this name.  The attribute can be specified multiple times to make the member visible to Lua code under multiple names.

Because functions are first-class values in Lua, when the Lua code `x.y()` is executed, this looks up the `y` member of `x` and then attempts to invoke it.  Eluant responds to this by creating a proxy function when a method is "read" from Lua code.  It is also possible for Lua code to store functions for later execution (`z = x.y`).  Eluant makes sure that each Lua function representing a method holds a strong reference to the CLR object the method is declared on.

All of the same rules in the section "Exposing CLR Functions to Lua" also apply to Lua functions representing methods of CLR objects including argument handling, return value handling, and exception handling.

Note that one can implement a custom `ILuaBinder`, which allows you to alter the logic used to map member names to members, and the logic used to convert CLR values and objects to Lua values.  A custom `IBindingSecurityPolicy` will let you customize the logic for determining which members should be Lua-accessible.  (The default security policy denies access to all members not marked with `LuaMemberAttribute`.)

Coroutine Support
=================

Lua supports coroutines, and Eluant does not place any restrictions on the use of coroutines from within Lua.  However, due to complexities in the runtime Eluant does not allow Lua to call into the CLR while executing a coroutine.  This will cause a Lua error.  All interaction with the CLR must be from the main Lua thread.

This restriction may be lifted at a future date if the design issues surrounding coroutines can be solved.

The `LuaThread` type (which represents a Lua coroutine object) does not provide any special support for coroutine control.  It can be used with Lua's own coroutine control methods, but otherwise acts like an opaque object.

Note that coroutines are not real operating system threads, but rather implement cooperative multitasking.  Only one coroutine is active at any one moment.  Coroutines cannot be run on multiple threads, from within Lua or without.  (Attempting to run multiple Lua coroutines using multiple CLR threads will cause corruption of the runtime state, as Lua is not thread-safe.)

If one wants to parallelize Lua code, then coroutines are not the answer.  Any solution would involve *multiple* Lua runtimes, one for each thread.

Limiting Memory Consumption
===========================

Eluant provides `MemoryConstrainedLuaRuntime` which is a specialization of `LuaRuntime` designed to place an artificial limit on the amount of memory that Lua may allocate.  This can be useful when running untrusted code.

The `MemoryUse` property returns how much memory has been allocated by Lua in bytes, and `MaxMemoryUse` sets the limit on how many bytes may be allocated by Lua in total.  If a memory allocation by Lua would cause Lua to exceed this quota, the request will be denied and Lua will raise an error in response.

`MaxMemoryUse` defaults to `Int64.MaxValue` by default.  Note that it's possible to exclude memory allocated by Lua during initialization, or by any code that customizes the Lua environment, by adding `MemoryUse` to the predetermined memory limit after all initialization is complete.  For example, if you want to give untrusted code an additional 10MB beyond the memory required by the initial environment, you could set `MaxMemoryUse = MemoryUse + (10 * 1024 * 1024)`.

Because longjmps over CLR stack frames must be avoided, allocations will never be denied while the CLR is executing code.  This means it may be possible for a memory-constrained runtime to exceed its allocation limit, but only while execution is in the CLR.  As soon as control returns to Lua, any attempt to allocate additional memory would be denied.  (Attempts to release memory will never result in an error under any circumstances.)

Of course, if Lua is referencing any CLR objects, those object allocations will have been allocated by the CLR and not Lua; they will not be accounted for since Lua did not allocate them.  (Of course, the Lua object that wraps the CLR object will be accounted for.)  Be careful when exposing CLR code to Lua that you don't create a vulnerability by which Lua can allocate an unbounded amount of memory!

When using a memory-constrained runtime, it is more critical than ever that one dispose of CLR references to Lua objects.  If this is not done, then these Lua objects otherwise eligible for collection will have an artificially extended lifetime and will count against the runtime's allocation limit.

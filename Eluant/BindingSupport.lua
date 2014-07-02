-- Eluant bindings

-- Make copies of global functions to prevent sandboxed scripts from altering
-- the behavior of Eluant bindings.
local real_pcall = pcall
local real_select = select
local real_error = error

-- Lua -> managed call support.
--
-- Since longjmp() can have bad effects on the CLR, we try to avoid generating
-- Lua errors in managed code.  Therefore we need a mechanism to raise errors
-- that does not rely on managed code.
--
-- The following code creates a function that is given a userdata representing
-- a CLR delegate and returns a Lua function that will call it.  The CLR code
-- is required to follow the same protocol as pcall(): the first return value is
-- true on success, followed by results, and false on failure, followed by the
-- error message.  The Lua proxy function will raise the error if necessary.
--
-- The other half of this protocol is implemented in C#, where the wrapper
-- responsible for unpacking Lua arguments into CLR types can capture exceptions
-- and propagate them as a false return flag.
local function process_managed_call_result(flag, flag2, ...)
    -- Because we pcall the wrapper, we have two levels of flags.  Outer is from
    -- pcall, inner is from the CLR side.
    
    -- pcall
    if not flag then real_error(flag2, ...) end
    
    -- CLR
    if not flag2 then real_error(...) end
    
    -- No error
    return ...
end

function eluant_create_managed_call_wrapper(fn)
    return function(...)
        return process_managed_call_result(real_pcall(fn, ...))
    end
end

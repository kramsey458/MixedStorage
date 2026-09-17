using System;
using HarmonyLib;
using Timberborn.InputSystem;

namespace MixedStorage
{
    // Timberborn polls shortcuts outside UI Toolkit's event propagation. Use its native
    // blocker only during this update; text editing continues through UI Toolkit normally.
    [HarmonyPatch(typeof(InputService), nameof(InputService.UpdateSingleton))]
    internal static class TextEditingInputPatch
    {
        static void Prefix(InputBlocker ____inputBlocker, out bool __state)
        {
            __state = StorageView.IsEditingText;
            if (__state) ____inputBlocker.Block();
        }

        static Exception Finalizer(InputBlocker ____inputBlocker, bool __state, Exception __exception)
        {
            if (__state) ____inputBlocker.Unblock();
            return __exception;
        }
    }
}

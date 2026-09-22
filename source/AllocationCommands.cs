using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Timberborn.InventorySystem;

namespace MixedStorage
{
    public enum SubmissionResult { Applied, Queued, Rejected }

    // Small optional integration boundary. The base mod has no BeaverBuddies assembly dependency.
    public static class AllocationCommands
    {
        public static Func<SingleGoodAllower, string, SubmissionResult> MultiplayerSubmit;

        internal static SubmissionResult Submit(StorageState state, string payload)
        {
            if (MultiplayerSubmit != null) return MultiplayerSubmit(state.Allower, payload);
            // Without the bridge, never change allocations locally in a multiplayer game: the others would not.
            var bb = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "BeaverBuddies");
            if (bb != null && !IsSinglePlayer(bb))
            {
                Report(state.Allower, false, OptionalMultiplayer.UnavailableReason ??
                    "MixedStorage multiplayer integration is unavailable. Install the same complete MixedStorage release on every computer and restart.");
                return SubmissionResult.Rejected;
            }
            bool applied = ApplyReplay(state.Allower, payload, out string message);
            Report(state.Allower, applied, message);
            return applied ? SubmissionResult.Applied : SubmissionResult.Rejected;
        }

        // BeaverBuddies has no event connection outside multiplayer. If that cannot be read, assume multiplayer.
        private static bool IsSinglePlayer(Assembly beaverBuddies)
        {
            try
            {
                var isNull = beaverBuddies.GetType("BeaverBuddies.IO.EventIO")?.GetProperty("IsNull", BindingFlags.Public | BindingFlags.Static);
                return isNull?.PropertyType == typeof(bool) && (bool)isNull.GetValue(null);
            }
            catch (Exception) { return false; }
        }

        public static bool ApplyReplay(SingleGoodAllower allower, string payload, out string message)
        {
            var state = StorageState.Get(allower);
            if (state == null) { message = "Storage no longer exists or is unsupported."; return false; }
            Dictionary<string, int> plan;
            try { plan = AllocationPlan.Deserialize(payload); }
            catch (FormatException) { message = "The allocation could not be read. Nothing was changed."; return false; }
            if (!state.TryApply(plan, out message)) return false;
            message = "Applied. Excess stock is preserved and can be hauled out.";
            return true;
        }

        public static void Report(SingleGoodAllower allower, bool success, string message)
        {
            var state = StorageState.Get(allower);
            if (state == null) return;
            state.Pending = false;
            state.LastMessage = message;
            state.LastSuccess = success;
            state.MessageRevision++;
        }
    }
}

using System;
using System.Linq;
using Timberborn.InventorySystem;

namespace MixedWarehouses
{
    public enum SubmissionResult { Applied, Queued, Rejected }

    // Small optional integration boundary. The base mod has no BeaverBuddies assembly dependency.
    public static class AllocationCommands
    {
        public static Func<SingleGoodAllower, string, SubmissionResult> MultiplayerSubmit;

        internal static SubmissionResult Submit(WarehouseState state, string payload)
        {
            if (MultiplayerSubmit != null) return MultiplayerSubmit(state.Allower, payload);
            // Do not fall back to local mutations when BB is present without the integration mod.
            var bb = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "BeaverBuddies");
            var io = bb?.GetType("BeaverBuddies.IO.EventIO");
            if (io != null && !(bool)io.GetProperty("IsNull").GetValue(null))
            {
                Report(state.Allower, false, "Enable Mixed Warehouses – BeaverBuddies on every player before applying allocations.");
                return SubmissionResult.Rejected;
            }
            bool applied = ApplyReplay(state.Allower, payload, out string message);
            Report(state.Allower, applied, message);
            return applied ? SubmissionResult.Applied : SubmissionResult.Rejected;
        }

        public static bool ApplyReplay(SingleGoodAllower allower, string payload, out string message)
        {
            var state = WarehouseState.Get(allower);
            if (state == null) { message = "Storage no longer exists or is unsupported."; return false; }
            if (!state.TryApply(AllocationPlan.Deserialize(payload), out message)) return false;
            message = "Applied. Excess stock is preserved and can be hauled out.";
            return true;
        }

        public static void Report(SingleGoodAllower allower, bool success, string message)
        {
            var state = WarehouseState.Get(allower);
            if (state == null) return;
            state.Pending = false;
            state.LastMessage = message;
            state.LastSuccess = success;
            state.MessageRevision++;
        }
    }
}

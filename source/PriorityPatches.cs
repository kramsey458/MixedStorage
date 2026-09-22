using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.Carrying;
using Timberborn.InventorySystem;
using Timberborn.StockpilePrioritySystem;

namespace MixedStorage
{
    [HarmonyPatch]
    internal static class ObtainPatch
    {
        static MethodBase TargetMethod() => AccessTools.Method(
            "Timberborn.StockpilePrioritySystem.ObtainGoodWorkplaceBehavior:Decide");

        // Replaces the original (returns false): run after any other mod's prefix, so every co-op player runs them in the same order.
        [HarmonyPriority(Priority.Last)]
        static bool Prefix(BaseComponent __instance, BehaviorAgent agent, ref Decision __result)
        {
            var state = StorageState.Get(__instance);
            if (state?.Active != true) return true;
            __result = Decision.ReleaseNow();
            if (!state.Inventory.Enabled || !__instance.GetComponent<GoodObtainer>().IsObtaining) return false;
            var finder = agent.GetComponent<CarrierInventoryFinder>();
            // Fill the least-full allocations first; deterministic tie breaks across multiplayer peers.
            foreach (string good in state.Shares.Keys.Where(x => state.Limit(x) > 0 && state.Inventory.HasUnreservedCapacity(x))
                         .OrderBy(x => (decimal)(state.Inventory.AmountInStock(x) + state.Inventory.ReservedCapacity(x)) / state.Limit(x))
                         .ThenBy(x => x, StringComparer.Ordinal))
            {
                if (!finder.TryCarryFromAnyInventory(good, state.Inventory, CanObtainFrom)) continue;
                __result = Decision.ReleaseNextTick();
                break;
            }
            return false;
        }

        private static bool CanObtainFrom(Inventory inventory)
        {
            var obtainer = inventory.GetComponent<GoodObtainer>();
            return !obtainer || !obtainer.IsObtaining;
        }
    }

    [HarmonyPatch(typeof(SupplyGoodWorkplaceBehavior), nameof(SupplyGoodWorkplaceBehavior.Decide))]
    internal static class SupplyPatch
    {
        // Replaces the original (returns false): run after any other mod's prefix, so every co-op player runs them in the same order.
        [HarmonyPriority(Priority.Last)]
        static bool Prefix(SupplyGoodWorkplaceBehavior __instance, BehaviorAgent agent, ref Decision __result)
        {
            var state = StorageState.Get(__instance);
            if (state?.Active != true) return true;
            __result = Decision.ReleaseNow();
            if (!state.Inventory.Enabled || !__instance.GetComponent<GoodSupplier>().IsSupplying) return false;
            var finder = agent.GetComponent<CarrierInventoryFinder>();
            // Offer the most stocked goods first; goods with nothing to carry are skipped without a district search.
            foreach (string good in AllocationPlan.SupplyCandidates(state.Shares.Keys, state.Inventory.UnreservedAmountInStock))
            {
                if (!finder.TryCarryToAnyInventory(good, state.Inventory, CanGiveTo)) continue;
                __result = Decision.ReleaseNextTick();
                break;
            }
            return false;
        }

        private static bool CanGiveTo(Inventory inventory)
        {
            var supplier = inventory.GetComponent<GoodSupplier>();
            return !supplier || !supplier.IsSupplying;
        }
    }

    [HarmonyPatch]
    internal static class MixedDropdownLabelPatch
    {
        static MethodBase TargetMethod() => AccessTools.Method("Timberborn.StockpilesUI.StockpileDropdownProvider:FormatDisplayText");
        // Replaces the original (returns false): run after any other mod's prefix, like the hauling prefixes above.
        [HarmonyPriority(Priority.Last)]
        static bool Prefix(BaseComponent __instance, bool selected, ref string __result)
        {
            var state = StorageState.Get(__instance);
            if (!selected || state?.Active != true) return true;
            int count = state.Shares.Count;
            __result = "Mixed (" + count + (count == 1 ? " good" : " goods") + ") — edit storage panel";
            return false;
        }
    }
}

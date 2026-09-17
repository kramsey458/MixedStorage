using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.Carrying;
using Timberborn.InventorySystem;
using Timberborn.StockpilePrioritySystem;

namespace MixedWarehouses
{
    [HarmonyPatch]
    internal static class ObtainPatch
    {
        static MethodBase TargetMethod() => AccessTools.Method(
            "Timberborn.StockpilePrioritySystem.ObtainGoodWorkplaceBehavior:Decide");

        static bool Prefix(BaseComponent __instance, BehaviorAgent agent, ref Decision __result)
        {
            var state = WarehouseState.Get(__instance);
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
        static bool Prefix(SupplyGoodWorkplaceBehavior __instance, BehaviorAgent agent, ref Decision __result)
        {
            var state = WarehouseState.Get(__instance);
            if (state?.Active != true) return true;
            __result = Decision.ReleaseNow();
            if (!state.Inventory.Enabled || !__instance.GetComponent<GoodSupplier>().IsSupplying) return false;
            var finder = agent.GetComponent<CarrierInventoryFinder>();
            foreach (string good in state.Shares.Keys.OrderByDescending(x => state.Inventory.UnreservedAmountInStock(x))
                         .ThenBy(x => x, StringComparer.Ordinal))
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
        static bool Prefix(BaseComponent __instance, bool selected, ref string __result)
        {
            var state = WarehouseState.Get(__instance);
            if (!selected || state?.Active != true) return true;
            __result = "Mixed (" + state.Shares.Count + " goods) — edit storage panel";
            return false;
        }
    }
}

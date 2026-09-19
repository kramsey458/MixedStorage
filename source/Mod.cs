using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.ModManagerScene;
using Timberborn.StockpilesUI;
using Timberborn.StockpileVisualization;
using Timberborn.WorldPersistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace MixedStorage
{
    public sealed class ModStarter : IModStarter
    {
        public void StartMod(IModEnvironment environment)
        {
            OptionalMultiplayer.Initialize();
            new Harmony("kyler.mixedstorage").PatchAll(typeof(ModStarter).Assembly);
            Debug.Log("[MixedStorage] 0.5.3 loaded; warehouse and pile allocations for Timberborn 1.1.2.4.");
        }
    }

    [HarmonyPatch(typeof(SingleGoodAllower), nameof(SingleGoodAllower.Initialize))]
    internal static class AttachPatch
    {
        static void Postfix(SingleGoodAllower __instance, Inventory inventory) => StorageState.Attach(__instance, inventory);
    }

    [HarmonyPatch(typeof(SingleGoodAllower), nameof(SingleGoodAllower.AllowedAmount))]
    internal static class LimitPatch
    {
        static bool Prefix(SingleGoodAllower __instance, string goodId, ref int __result)
        {
            var state = StorageState.Get(__instance);
            if (state?.Active != true) return true;
            __result = state.Limit(goodId);
            return false;
        }
    }

    // Legacy single-good controls must not silently erase an active multi-good allocation.
    [HarmonyPatch(typeof(SingleGoodAllower), nameof(SingleGoodAllower.Allow))]
    internal static class AllowPatch
    {
        static bool Prefix(SingleGoodAllower __instance)
        {
            var state = StorageState.Get(__instance);
            return state?.Active != true || state.InternalChange;
        }
    }
    [HarmonyPatch(typeof(SingleGoodAllower), nameof(SingleGoodAllower.Disallow))]
    internal static class DisallowPatch
    {
        static bool Prefix(SingleGoodAllower __instance)
        {
            var state = StorageState.Get(__instance);
            return state?.Active != true || state.InternalChange;
        }
    }
    [HarmonyPatch(typeof(SingleGoodAllower), nameof(SingleGoodAllower.Save))]
    internal static class SavePatch
    {
        static void Postfix(SingleGoodAllower __instance, IEntitySaver entitySaver) => StorageState.Get(__instance)?.Save(entitySaver);
    }
    [HarmonyPatch(typeof(SingleGoodAllower), nameof(SingleGoodAllower.Load))]
    internal static class LoadPatch
    {
        static void Postfix(SingleGoodAllower __instance, IEntityLoader entityLoader) => StorageState.Get(__instance)?.Load(entityLoader);
    }
    [HarmonyPatch(typeof(SingleGoodAllower), nameof(SingleGoodAllower.DuplicateFrom))]
    internal static class DuplicatePatch
    {
        static void Postfix(SingleGoodAllower __instance, SingleGoodAllower source) => StorageState.Get(__instance)?.Duplicate(StorageState.Get(source));
    }
    [HarmonyPatch(typeof(StockpileVisualizers), "OnDisallowedGoodsChanged")]
    internal static class VisualizerPatch
    {
        static bool Prefix(StockpileVisualizers __instance, ref DisallowedGoodsChangedEventArgs e)
        {
            var state = StorageState.Get(__instance);
            if (state?.Active != true) return true;
            if (!state.Allower.HasAllowedGood) return false;
            e = new DisallowedGoodsChangedEventArgs(state.Allower.AllowedGood);
            return true;
        }
    }

    internal static class Views
    {
        internal static readonly ConditionalWeakTable<StockpileInventoryFragment, StorageView> All = new ConditionalWeakTable<StockpileInventoryFragment, StorageView>();
    }
    [HarmonyPatch(typeof(StockpileInventoryFragment), nameof(StockpileInventoryFragment.InitializeFragment))]
    internal static class InitializeViewPatch
    {
        static void Postfix(StockpileInventoryFragment __instance, IGoodService ____goodService, VisualElementLoader ____visualElementLoader, ref VisualElement __result)
        {
            var view = new StorageView(____goodService, ____visualElementLoader, __result);
            Views.All.Add(__instance, view);
            __result = view.Root;
        }
    }
    [HarmonyPatch(typeof(StockpileInventoryFragment), nameof(StockpileInventoryFragment.ShowFragment))]
    internal static class ShowViewPatch
    {
        static void Postfix(StockpileInventoryFragment __instance, BaseComponent entity)
        { if (Views.All.TryGetValue(__instance, out var view)) view.Show(StorageState.Get(entity)); }
    }
    [HarmonyPatch(typeof(StockpileInventoryFragment), nameof(StockpileInventoryFragment.UpdateFragment))]
    internal static class UpdateViewPatch
    {
        static void Postfix(StockpileInventoryFragment __instance)
        { if (Views.All.TryGetValue(__instance, out var view)) view.Refresh(); }
    }
    [HarmonyPatch(typeof(StockpileInventoryFragment), nameof(StockpileInventoryFragment.ClearFragment))]
    internal static class ClearViewPatch
    {
        static void Postfix(StockpileInventoryFragment __instance)
        { if (Views.All.TryGetValue(__instance, out var view)) view.Clear(); }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.InventorySystem;
using Timberborn.Persistence;
using Timberborn.Stockpiles;
using Timberborn.TemplateSystem;
using Timberborn.WorldPersistence;

namespace MixedWarehouses
{
    internal sealed class WarehouseState
    {
        private static readonly ConditionalWeakTable<SingleGoodAllower, WarehouseState> States = new ConditionalWeakTable<SingleGoodAllower, WarehouseState>();
        private static readonly ComponentKey SaveKey = new ComponentKey("MixedWarehouses.Allocation");
        private static readonly PropertyKey<string> SharesKey = new PropertyKey<string>("Shares");
        private static readonly MethodInfo NotifyGood = AccessTools.Method(typeof(SingleGoodAllower), "InvokeDisallowedGoodsChangedEvent");
        private static readonly HashSet<string> SupportedTemplates = new HashSet<string>(StringComparer.Ordinal)
        {
            "SmallWarehouse.Folktails", "MediumWarehouse.Folktails", "LargeWarehouse.Folktails",
            "SmallWarehouse.IronTeeth", "MediumWarehouse.IronTeeth", "LargeWarehouse.IronTeeth",
            "SmallPile.Folktails", "LargePile.Folktails", "UndergroundPile.Folktails",
            "SmallIndustrialPile.IronTeeth", "LargeIndustrialPile.IronTeeth"
        };

        public readonly SingleGoodAllower Allower;
        public readonly Inventory Inventory;
        public Dictionary<string, int> Shares { get; private set; }
        private Dictionary<string, int> _limits;
        private int _cachedCapacity = -1;
        public bool Active => Shares != null;
        public bool InternalChange { get; private set; }
        public int Revision { get; private set; }
        public bool Pending;
        public string LastMessage;
        public bool LastSuccess;
        public int MessageRevision;

        private WarehouseState(SingleGoodAllower allower, Inventory inventory) { Allower = allower; Inventory = inventory; }

        public static void Attach(SingleGoodAllower allower, Inventory inventory)
        {
            var template = allower.GetComponent<TemplateSpec>();
            if (template != null && SupportedTemplates.Contains(template.TemplateName))
                States.GetValue(allower, _ => new WarehouseState(allower, inventory));
        }

        public static WarehouseState Get(SingleGoodAllower allower) =>
            allower != null && States.TryGetValue(allower, out var state) ? state : null;

        public static WarehouseState Get(BaseComponent entity) => Get(entity.GetComponent<SingleGoodAllower>());

        public int Limit(string good)
        {
            if (_limits == null || _cachedCapacity != Inventory.Capacity)
            {
                _limits = AllocationPlan.Capacities(Shares, Inventory.Capacity);
                _cachedCapacity = Inventory.Capacity;
            }
            return _limits.TryGetValue(good, out var limit) ? limit : 0;
        }

        public Dictionary<string, int> Draft()
        {
            var draft = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var good in Inventory.InputGoods) draft[good] = 0;
            if (Active) foreach (var item in Shares) draft[item.Key] = item.Value;
            else if (Allower.HasAllowedGood) draft[Allower.AllowedGood] = AllocationPlan.Total;
            return draft;
        }

        public bool TryApply(IReadOnlyDictionary<string, int> draft, out string error)
        {
            error = null;
            if (!AllocationPlan.IsValid(draft)) { error = "Percentages must total exactly 100%."; return false; }
            if (draft.Any(x => x.Value > 0 && !Inventory.Takes(x.Key)))
            { error = "Set unavailable goods to 0% before applying."; return false; }
            var limits = AllocationPlan.Capacities(draft, Inventory.Capacity);
            foreach (var good in Inventory.InputGoods)
            {
                limits.TryGetValue(good, out int limit);
                if (AllocationPlan.ConflictsWithDelivery(Inventory.AmountInStock(good), Inventory.ReservedCapacity(good), limit))
                { error = "Wait for incoming deliveries to finish before lowering their limits."; return false; }
            }
            SetShares(draft);
            Publish();
            return true;
        }

        private void SetShares(IReadOnlyDictionary<string, int> shares)
        {
            Shares = shares.Where(x => x.Value > 0).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            _limits = null;
            Revision++;
        }

        private void Publish()
        {
            // Vanilla banners/statuses require a representative good; all actual limits use Shares.
            string representative = Shares.Where(x => Inventory.Takes(x.Key))
                .OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key).FirstOrDefault();
            InternalChange = true;
            try { Allower.Allow(representative); }
            finally { InternalChange = false; }
            foreach (string good in Inventory.InputGoods) NotifyGood.Invoke(Allower, new object[] { good });
        }

        public void Save(IEntitySaver saver)
        {
            if (Active) saver.GetComponent(SaveKey).Set(SharesKey, AllocationPlan.Serialize(Shares));
        }

        public void Load(IEntityLoader loader)
        {
            if (loader.TryGetComponent(SaveKey, out var component) && component.Has(SharesKey))
            {
                // Do not silently discard a malformed allocation and start accepting different goods.
                SetShares(AllocationPlan.Deserialize(component.Get(SharesKey)));
            }
        }

        public void Duplicate(WarehouseState source)
        {
            if (source != null && source.Active) TryApply(source.Shares, out _);
        }
    }
}

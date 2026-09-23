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
using UnityEngine;

namespace MixedStorage
{
    internal sealed class StorageState : IStorageContents
    {
        private static readonly ConditionalWeakTable<SingleGoodAllower, StorageState> States = new ConditionalWeakTable<SingleGoodAllower, StorageState>();
        private static readonly ComponentKey SaveKey = new ComponentKey("MixedStorage.Allocation");
        private static readonly PropertyKey<string> SharesKey = new PropertyKey<string>("Shares");
        // The game's private announcement that a good's limit changed; Inventory and the game's caches of which
        // buildings have room for each good listen to it. Looked up by signature, so an added overload cannot
        // make the lookup ambiguous. Null when a game update renamed or changed it (see UnavailableReason).
        private static readonly MethodInfo NotifyGood = AccessTools.Method(typeof(SingleGoodAllower), "InvokeDisallowedGoodsChangedEvent", new[] { typeof(string) });
        private static readonly HashSet<string> SupportedTemplates = new HashSet<string>(StringComparer.Ordinal)
        {
            "SmallWarehouse.Folktails", "MediumWarehouse.Folktails", "LargeWarehouse.Folktails",
            "SmallWarehouse.IronTeeth", "MediumWarehouse.IronTeeth", "LargeWarehouse.IronTeeth",
            "SmallPile.Folktails", "LargePile.Folktails", "UndergroundPile.Folktails",
            "SmallIndustrialPile.IronTeeth", "LargeIndustrialPile.IronTeeth"
        };

        // Set when the installed game lacks that announcement. Changing an allocation without it would set the
        // new shares but leave the game unaware of the new limits, so allocations cannot change: Apply and copies
        // are refused with this reason and mixed buildings stay mixed. Saved allocations still limit their
        // buildings, and the game still starts; ModStarter logs the reason.
        internal static string UnavailableReason => NotifyGood != null ? null :
            "MixedStorage cannot change allocations with this game version: the game no longer has SingleGoodAllower.InvokeDisallowedGoodsChangedEvent(string). " +
            "Existing allocations still apply. Install the MixedStorage version made for this game version.";

        // Why a Duplicate settings copy from source (null when that building has no mixed-storage state) onto this
        // building must be refused, or null: while allocations are frozen, no copy may set or leave one.
        internal string FrozenCopyReason(StorageState source) => Active || source?.Active == true ? UnavailableReason : null;

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

        private StorageState(SingleGoodAllower allower, Inventory inventory) { Allower = allower; Inventory = inventory; }

        public static void Attach(SingleGoodAllower allower, Inventory inventory)
        {
            var template = allower.GetComponent<TemplateSpec>();
            if (template != null && SupportedTemplates.Contains(template.TemplateName))
                States.GetValue(allower, _ => new StorageState(allower, inventory));
        }

        public static StorageState Get(SingleGoodAllower allower) =>
            allower != null && States.TryGetValue(allower, out var state) ? state : null;

        public static StorageState Get(BaseComponent entity) => Get(entity.GetComponent<SingleGoodAllower>());

        // Also runs on LateGamePerformance's worker threads, one worker per storage (see LimitPatch). Shares is only
        // replaced on the main thread, never changed in place.
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

        public bool CanApply(IReadOnlyDictionary<string, int> draft, out string error)
        {
            error = UnavailableReason;
            return error == null && AllocationPlan.CanApply(draft, this, out error);
        }

        int IStorageContents.Capacity => Inventory.Capacity;
        IEnumerable<string> IStorageContents.Goods { get { foreach (string good in Inventory.InputGoods) yield return good; } }
        bool IStorageContents.Takes(string good) => Inventory.Takes(good);
        int IStorageContents.Stock(string good) => Inventory.AmountInStock(good);
        int IStorageContents.Incoming(string good) => Inventory.ReservedCapacity(good);

        public bool TryApply(IReadOnlyDictionary<string, int> draft, out string error)
        {
            if (!CanApply(draft, out error)) return false;
            SetShares(draft);
            Publish();
            return true;
        }

        // Apply with every good at 0%: the building stores nothing, as when it was just built. It leaves mixed mode
        // and gives up its single good. Every limit drops to 0, so it gets the same incoming-delivery guard as any
        // other lowered limit; stock already here stays as excess.
        public bool TryClear(out string error)
        {
            error = Active ? UnavailableReason : null;
            if (error != null || !AllocationPlan.CanLeave(null, this, out error)) return false;
            Deactivate();
            Allower.Disallow();
            // An open editor reloads its draft, also when the building was not mixed.
            Revision++;
            return true;
        }

        private void SetShares(IReadOnlyDictionary<string, int> shares)
        {
            Shares = shares?.Where(x => x.Value > 0).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
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
            foreach (string good in Inventory.InputGoods) Notify(good);
        }

        // Returns the building to the base game's single-good rules; the caller then sets that good.
        public void Deactivate()
        {
            // Without the announcement the building stays mixed (see UnavailableReason), and AllowPatch then keeps
            // the base game's copy from switching its good.
            if (!Active || UnavailableReason != null) return;
            var previous = Shares;
            SetShares(null);
            Announce(previous.Keys);
        }

        // Announces goods whose limit changed, because the game caches which buildings have room for each good.
        // Only goods the building accepts: the game limits any other good to 0 whatever the allocation says
        // (Inventory.LimitedAmount), and a saved allocation can name a good the game no longer knows, which
        // would throw from InventoryRegistry. While these fire, the building still has its representative good;
        // the visuals must follow that good (VisualizerPatch), not whichever good is announced last. Without the
        // announcement method (see UnavailableReason) nothing is announced.
        private void Announce(IEnumerable<string> goods)
        {
            if (NotifyGood == null) return;
            InternalChange = true;
            try { foreach (string good in goods) if (Inventory.Takes(good)) Notify(good); }
            finally { InternalChange = false; }
        }

        private void Notify(string good) => NotifyGood.Invoke(Allower, new object[] { good });

        // Also runs on LateGamePerformance's save workers (see SavePatch).
        public void Save(IEntitySaver saver)
        {
            if (Active) saver.GetComponent(SaveKey).Set(SharesKey, AllocationPlan.Serialize(Shares));
        }

        public void Load(IEntityLoader loader)
        {
            // The state mirrors the save, also when the map editor's undo reloads an existing building. A building
            // loaded for the first time is never mixed yet, so a mixed one is already in the world: its changed
            // limits are announced as Apply and Deactivate do, which also sends its visuals back to the native pile
            // when it leaves mixed mode (MixedAllocationVisualPatch).
            var previous = Shares;
            if (!loader.TryGetComponent(SaveKey, out var component) || !component.Has(SharesKey)) { if (Active) SetShares(null); }
            else
            {
                string saved = component.Get(SharesKey);
                try { SetShares(AllocationPlan.Deserialize(saved)); }
                catch (FormatException ex)
                {
                    // One unreadable building must not stop the whole save from loading. It keeps the
                    // single good the base game saved alongside it, and the warning says what was lost.
                    if (Active) SetShares(null);
                    Debug.LogWarning("[MixedStorage] Ignoring an unreadable saved allocation for " + Allower.Name + " (" + ex.Message + "): " + saved);
                }
            }
            if (previous != null) Announce(previous.Keys.Union(Shares?.Keys ?? Enumerable.Empty<string>(), StringComparer.Ordinal));
        }

        public void Duplicate(StorageState source)
        {
            if (TryApply(source.Shares, out string error)) return;
            Debug.LogWarning("[MixedStorage] Copied allocations were not applied to " + Allower.Name + ": " + error);
        }
    }
}

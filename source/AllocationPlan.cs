using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MixedStorage
{
    // What the allocation guards read from a building. All of it is simulation state that every co-op
    // player shares, so every player replaying a change decides the same way.
    public interface IStorageContents
    {
        int Capacity { get; }
        // The goods the building accepts; it only ever holds those.
        IEnumerable<string> Goods { get; }
        bool Takes(string good);
        int Stock(string good);
        int Incoming(string good);
    }

    // What the game's Duplicate settings tool does to a building this mod manages.
    public enum CopyPlan { BaseGame, CopyAllocation, LeaveMixed, Refuse }

    // What DuplicatePatch does for a plan, in this order: leave mixed mode, let the base game copy the single
    // good, apply the copied allocation afterwards, and log a refusal.
    [Flags]
    public enum CopySteps { None = 0, LeaveMixed = 1, RunBaseGame = 2, ApplyAllocation = 4, LogRefusal = 8 }

    // Integer hundredths of a percent: validation never depends on float tolerances.
    public static class AllocationPlan
    {
        public const int Total = 10000;

        public static Dictionary<string, int> Max(IEnumerable<string> goods, string selected)
        {
            var result = goods.Distinct(StringComparer.Ordinal).ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
            if (!result.ContainsKey(selected)) throw new ArgumentException("Good is unavailable.");
            result[selected] = Total;
            return result;
        }

        public static bool TryPaste(IReadOnlyDictionary<string, int> copied, IEnumerable<string> allowed,
            out Dictionary<string, int> draft)
        {
            draft = null;
            if (!IsValid(copied)) return false;
            var result = allowed.Distinct(StringComparer.Ordinal).ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
            if (copied.Any(x => x.Value > 0 && !result.ContainsKey(x.Key))) return false;
            foreach (var item in copied.Where(x => x.Value > 0)) result[item.Key] = item.Value;
            draft = result;
            return true;
        }

        public static bool TryParsePercent(string text, out int units)
        {
            units = 0;
            if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.CurrentCulture, out var percent) &&
                !decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out percent)) return false;
            if (percent < 0 || percent > 100 || percent * 100 != decimal.Truncate(percent * 100)) return false;
            units = (int)(percent * 100);
            return true;
        }

        public static string Format(int units) => (units / 100m).ToString("0.##", CultureInfo.CurrentCulture);

        public static bool IsValid(IReadOnlyDictionary<string, int> shares) =>
            shares != null && shares.Count > 0 &&
            shares.All(x => !string.IsNullOrEmpty(x.Key) && x.Value >= 0 && x.Value <= Total) &&
            shares.Sum(x => (long)x.Value) == Total;

        public static Dictionary<string, int> Capacities(IReadOnlyDictionary<string, int> shares, int capacity)
        {
            if (!IsValid(shares)) throw new ArgumentException("Allocations must total exactly 100%.");
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            var result = shares.ToDictionary(x => x.Key, x => (int)((long)capacity * x.Value / Total), StringComparer.Ordinal);
            int remainder = capacity - result.Values.Sum();
            // Largest remainder, with an ordinal good-ID tie break: stable across locales and saves.
            foreach (var item in shares.Where(x => x.Value > 0)
                         .OrderByDescending(x => (long)capacity * x.Value % Total)
                         .ThenBy(x => x.Key, StringComparer.Ordinal).Take(remainder))
                result[item.Key]++;
            return result;
        }

        public static bool ConflictsWithDelivery(int stock, int incoming, int limit) =>
            incoming > 0 && (long)stock + incoming > limit;

        public static bool CanApply(IReadOnlyDictionary<string, int> draft, IStorageContents storage, out string error)
        {
            error = null;
            if (!IsValid(draft)) { error = "Percentages must total exactly 100%."; return false; }
            if (draft.Any(x => x.Value > 0 && !storage.Takes(x.Key)))
            { error = "Set unavailable goods to 0% before applying."; return false; }
            return FitsDeliveries(Capacities(draft, storage.Capacity), storage, out error);
        }

        // Leaving mixed mode for the base game's single-good rules lowers limits just like Apply, so it gets the
        // same incoming-delivery guard. The base game (SingleGoodAllower.AllowedAmount) has room only for the
        // kept good, and none for it either while any other good is in stock.
        public static bool CanLeave(string kept, IStorageContents storage, out string error)
        {
            var limits = new Dictionary<string, int>(StringComparer.Ordinal);
            if (kept != null && !storage.Goods.Any(x => x != kept && storage.Stock(x) > 0)) limits[kept] = storage.Capacity;
            return FitsDeliveries(limits, storage, out error);
        }

        private static bool FitsDeliveries(IReadOnlyDictionary<string, int> limits, IStorageContents storage, out string error)
        {
            error = null;
            foreach (string good in storage.Goods)
            {
                limits.TryGetValue(good, out int limit);
                if (ConflictsWithDelivery(storage.Stock(good), storage.Incoming(good), limit))
                { error = "Wait for incoming deliveries to finish before lowering their limits."; return false; }
            }
            return true;
        }

        // The game's Duplicate settings tool onto a building this mod manages. sourceShares is the source's
        // allocation, null unless it is mixed, and sourceGood its single good. A mixed source applies through
        // the same checks as Apply. Any other source gives the target that good, or none, as in the base game,
        // so a mixed target leaves mixed mode first, unless that conflicts with an incoming delivery.
        public static CopyPlan PlanCopy(IReadOnlyDictionary<string, int> sourceShares, string sourceGood, bool targetMixed,
            IStorageContents target, out string error)
        {
            error = null;
            if (sourceShares != null) return CanApply(sourceShares, target, out error) ? CopyPlan.CopyAllocation : CopyPlan.Refuse;
            // The base game leaves the target unchanged when it does not take the source's good.
            if (!targetMixed || sourceGood != null && !target.Takes(sourceGood)) return CopyPlan.BaseGame;
            return CanLeave(sourceGood, target, out error) ? CopyPlan.LeaveMixed : CopyPlan.Refuse;
        }

        // What DuplicatePatch does for each plan, kept here so the allocation tests cover it. A copied allocation
        // lets the base game give the target the source's representative good first (AllowPatch ignores that on
        // a mixed target), then applies. A leave happens before the base game's copy, which AllowPatch would
        // otherwise block. A refusal changes nothing on the target and is logged.
        public static CopySteps Steps(CopyPlan plan)
        {
            switch (plan)
            {
                case CopyPlan.CopyAllocation: return CopySteps.RunBaseGame | CopySteps.ApplyAllocation;
                case CopyPlan.LeaveMixed: return CopySteps.LeaveMixed | CopySteps.RunBaseGame;
                case CopyPlan.Refuse: return CopySteps.LogRefusal;
                default: return CopySteps.RunBaseGame;
            }
        }

        public static string Serialize(IReadOnlyDictionary<string, int> shares)
        {
            if (!IsValid(shares)) throw new ArgumentException("Invalid allocation.");
            return "1|" + string.Join(";", shares.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => Uri.EscapeDataString(x.Key) + "=" + x.Value.ToString(CultureInfo.InvariantCulture)));
        }

        public static Dictionary<string, int> Deserialize(string value)
        {
            if (value == null || !value.StartsWith("1|", StringComparison.Ordinal)) throw new FormatException("Unsupported allocation format.");
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string pair in value.Substring(2).Split(';'))
            {
                var parts = pair.Split('=');
                if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int share))
                    throw new FormatException("Invalid saved allocation.");
                if (!result.TryAdd(Uri.UnescapeDataString(parts[0]), share))
                    throw new FormatException("Duplicate good in saved allocation.");
            }
            if (!IsValid(result)) throw new FormatException("Saved allocations do not total 100%.");
            return result;
        }
    }
}

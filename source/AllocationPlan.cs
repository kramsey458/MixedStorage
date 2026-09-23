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
    public enum CopyPlan { BaseGame, CopyAllocation, LeaveMixed, Refuse, KeepAllocation }

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

        // A draft with every good at 0%. Applying it makes the building store nothing, as when it was just built
        // (StorageState.TryClear), instead of being refused for not totaling 100%.
        public static bool IsNothing(IReadOnlyDictionary<string, int> draft) =>
            draft != null && draft.Count > 0 && draft.Values.All(x => x == 0);

        // Whether the editor's draft differs from what the building has applied (StorageState.Draft), so the editor
        // can point at Apply.
        public static bool Differs(IReadOnlyDictionary<string, int> draft, IReadOnlyDictionary<string, int> applied) =>
            draft.Count != applied.Count || draft.Any(x => !applied.TryGetValue(x.Key, out int share) || share != x.Value);

        // The editor's total line, and whether its draft may be applied. A saved allocation can still name a good this
        // building does not accept, for example after a goods mod was removed. Such a draft can total 100% and still
        // not apply (StorageState.CanApply), so the line says what to change instead. A draft of all 0% applies too,
        // but only a valid allocation can be copied.
        public static (bool Valid, string Text) DraftStatus(IReadOnlyDictionary<string, int> draft, Func<string, bool> takes, bool fieldsValid)
        {
            if (!fieldsValid) return (false, "Enter valid percentages (0–100, 2 decimals)");
            if (IsNothing(draft)) return (true, "0% allocated — Apply to store nothing");
            long total = draft.Values.Sum(x => (long)x);
            if (total != Total)
                return (false, (total / 100m).ToString("0.##") + "% / 100% — " + (Math.Abs(total - Total) / 100m).ToString("0.##") +
                    (total < Total ? "% remaining" : "% over"));
            if (draft.Any(x => x.Value > 0 && !takes(x.Key))) return (false, "Set unavailable goods to 0% before applying");
            return (IsValid(draft), "100% / 100% allocated");
        }

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

        // Supply mode's order: most unreserved stock first, with an ordinal good-ID tie break so every multiplayer
        // peer tries the goods in the same order. A good without unreserved stock is left out. The game's search
        // for a building to take it scans the whole district, then can only fail, because the load is capped at
        // that stock; and goods without stock sort last, so leaving them out never changes which good is carried.
        public static IEnumerable<string> SupplyCandidates(IEnumerable<string> goods, Func<string, int> unreserved) =>
            goods.Select(x => (Good: x, Stock: unreserved(x))).Where(x => x.Stock > 0)
                .OrderByDescending(x => x.Stock).ThenBy(x => x.Good, StringComparer.Ordinal).Select(x => x.Good);

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
        // the same checks as Apply. A source set to store nothing, such as a building just placed, leaves a mixed
        // target's allocation alone, so copying its other settings (the storage mode, for example) does not wipe
        // it. Any other source gives the target that good, as in the base game, so a mixed target leaves mixed
        // mode first, unless that conflicts with an incoming delivery.
        public static CopyPlan PlanCopy(IReadOnlyDictionary<string, int> sourceShares, string sourceGood, bool targetMixed,
            IStorageContents target, out string error)
        {
            error = null;
            if (sourceShares != null) return CanApply(sourceShares, target, out error) ? CopyPlan.CopyAllocation : CopyPlan.Refuse;
            // The base game leaves the target unchanged when it does not take the source's good.
            if (!targetMixed || sourceGood != null && !target.Takes(sourceGood)) return CopyPlan.BaseGame;
            if (sourceGood == null) return CopyPlan.KeepAllocation;
            return CanLeave(sourceGood, target, out error) ? CopyPlan.LeaveMixed : CopyPlan.Refuse;
        }

        // What DuplicatePatch does for each plan, kept here so the allocation tests cover it. A copied allocation
        // lets the base game give the target the source's representative good first (AllowPatch ignores that on
        // a mixed target), then applies. A leave happens before the base game's copy, which AllowPatch would
        // otherwise block. A refusal changes nothing on the target and is logged. Keeping the allocation skips the
        // base game's copy, which would clear the target's good (AllowPatch blocks that on a mixed target anyway).
        public static CopySteps Steps(CopyPlan plan)
        {
            switch (plan)
            {
                case CopyPlan.CopyAllocation: return CopySteps.RunBaseGame | CopySteps.ApplyAllocation;
                case CopyPlan.LeaveMixed: return CopySteps.LeaveMixed | CopySteps.RunBaseGame;
                case CopyPlan.Refuse: return CopySteps.LogRefusal;
                case CopyPlan.KeepAllocation: return CopySteps.None;
                default: return CopySteps.RunBaseGame;
            }
        }

        public static string Serialize(IReadOnlyDictionary<string, int> shares)
        {
            if (!IsValid(shares)) throw new ArgumentException("Invalid allocation.");
            return "1|" + string.Join(";", shares.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => Uri.EscapeDataString(x.Key) + "=" + x.Value.ToString(CultureInfo.InvariantCulture)));
        }

        // Apply's payload for a draft of all 0% (IsNothing). Saves never hold it: a building that stores nothing has no
        // allocation. Earlier versions cannot read it, so co-op players must run the same version.
        public const string Nothing = "1|";

        public static string SerializeCommand(IReadOnlyDictionary<string, int> draft) => IsNothing(draft) ? Nothing : Serialize(draft);

        // An Apply payload: an allocation, or no goods at all for Nothing.
        public static Dictionary<string, int> DeserializeCommand(string value) =>
            value == Nothing ? new Dictionary<string, int>(StringComparer.Ordinal) : Deserialize(value);

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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MixedWarehouses
{
    // Integer hundredths of a percent: validation never depends on float tolerances.
    public static class AllocationPlan
    {
        public const int Total = 10000;

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
                result.Add(Uri.UnescapeDataString(parts[0]), share);
            }
            if (!IsValid(result)) throw new FormatException("Saved allocations do not total 100%.");
            return result;
        }
    }
}

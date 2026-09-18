using MixedStorage;
using System.Globalization;

int assertions = 0;
void Check(bool test, string description) { assertions++; if (!test) throw new Exception(description); }
void Reject(Action action, string description)
{
    bool rejected = false;
    try { action(); } catch (ArgumentException) { rejected = true; } catch (FormatException) { rejected = true; }
    Check(rejected, description);
}

foreach (int capacity in new[] { 20, 30, 180, 200, 1000, 1200 })
{
    var plan = new Dictionary<string, int> { ["Plank"] = 5000, ["Bread"] = 5000, ["Gear"] = 0 };
    var limits = AllocationPlan.Capacities(plan, capacity);
    Check(limits["Plank"] == capacity / 2 && limits["Bread"] == capacity / 2, "50/50 limits");
    Check(limits["Gear"] == 0, "Disabled good must receive no rounding slots");
}
var thirds = new Dictionary<string, int> { ["C"] = 3334, ["B"] = 3333, ["A"] = 3333 };
var small = AllocationPlan.Capacities(thirds, 30);
Check(small.Values.All(x => x == 10), "Small warehouse thirds");
var medium = AllocationPlan.Capacities(thirds, 200);
Check(medium["C"] == 67 && medium["A"] == 67 && medium["B"] == 66, "Largest remainder and deterministic tie break");

var rng = new Random(27);
for (int run = 0; run < 2000; run++)
{
    int count = rng.Next(1, 101);
    var cuts = Enumerable.Range(0, count - 1).Select(_ => rng.Next(10001)).Append(0).Append(10000).Order().ToArray();
    var shares = Enumerable.Range(0, count).ToDictionary(i => "Good" + i, i => cuts[i + 1] - cuts[i]);
    int capacity = new[] { 20, 30, 180, 200, 1000, 1200, 1, 1000000 }[run % 8];
    var limits = AllocationPlan.Capacities(shares, capacity);
    Check(limits.Values.Sum() == capacity, "Rounding conserves capacity with up to 100 goods");
    Check(limits.All(x => x.Value >= 0 && (shares[x.Key] != 0 || x.Value == 0)), "No negative or disabled allocations");
    Check(limits.All(x => Math.Abs(x.Value - (decimal)capacity * shares[x.Key] / 10000) < 1), "Every rounding error is less than one item");
    var reordered = AllocationPlan.Capacities(shares.Reverse().ToDictionary(x => x.Key, x => x.Value), capacity);
    Check(limits.All(x => reordered[x.Key] == x.Value), "Order-independent allocation");
    var saved = AllocationPlan.Deserialize(AllocationPlan.Serialize(shares));
    Check(AllocationPlan.IsValid(saved) && shares.Where(x => x.Value > 0).All(x => saved[x.Key] == x.Value), "Save/load exactness");
}

foreach (string invalid in new[] { "-1", "100.01", "NaN", "Infinity", "1.001", "", "1,000", "1e2" })
    Check(!AllocationPlan.TryParsePercent(invalid, out _), "Reject percent " + invalid);
Check(AllocationPlan.TryParsePercent("33.33", out int parsed) && parsed == 3333, "Exact decimal parsing");
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
Check(AllocationPlan.TryParsePercent("33,33", out parsed) && parsed == 3333, "Localized decimal parsing");
Check(AllocationPlan.TryParsePercent("33.33", out parsed) && parsed == 3333, "Invariant fallback parsing");
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Reject(() => AllocationPlan.Capacities(new Dictionary<string, int> { ["A"] = 9999 }, 200), "Reject 99.99%");
Reject(() => AllocationPlan.Capacities(new Dictionary<string, int> { ["A"] = 10001 }, 200), "Reject 100.01%");
Reject(() => AllocationPlan.Deserialize("1|A=5000;A=5000"), "Reject duplicate saved IDs");
Reject(() => AllocationPlan.Deserialize("2|A=10000"), "Reject unknown save version");
Reject(() => AllocationPlan.Deserialize("1|A=9999"), "Reject invalid saved sum");
var unusual = new Dictionary<string, int> { ["Mod.Good;= |🌲"] = 10000 };
Check(AllocationPlan.Deserialize(AllocationPlan.Serialize(unusual)).Keys.Single() == unusual.Keys.Single(), "Escape modded IDs");
Check(AllocationPlan.ConflictsWithDelivery(45, 10, 50), "Reject incoming delivery over new cap");
Check(!AllocationPlan.ConflictsWithDelivery(40, 10, 50), "Allow delivery exactly to cap");
Check(!AllocationPlan.ConflictsWithDelivery(70, 0, 50), "Existing excess stock can remain without deletion");
var maxPlan = AllocationPlan.Max(new[] { "Log", "Plank", "ScrapMetal" }, "Plank");
Check(maxPlan["Plank"] == 10000 && maxPlan["Log"] == 0 && maxPlan["ScrapMetal"] == 0, "Max clears every other good");
Reject(() => AllocationPlan.Max(new[] { "Log" }, "Bread"), "Max rejects unavailable good");
var clipboardPlan = new Dictionary<string, int> { ["Log"] = 3333, ["Plank"] = 6667, ["UnavailableZero"] = 0 };
Check(AllocationPlan.TryPaste(clipboardPlan, new[] { "Log", "Plank", "ScrapMetal" }, out var pasted), "Paste ignores zero-only unsupported goods");
Check(pasted["Log"] == 3333 && pasted["Plank"] == 6667 && pasted["ScrapMetal"] == 0, "Paste preserves exact percentages and resets other goods");
Check(AllocationPlan.Capacities(pasted, 180).Values.Sum() == 180 && AllocationPlan.Capacities(pasted, 1200).Values.Sum() == 1200, "Paste rescales to destination capacity");
pasted["Log"] = 0;
Check(clipboardPlan["Log"] == 3333, "Editing pasted draft does not mutate copied allocation");
Check(!AllocationPlan.TryPaste(clipboardPlan, new[] { "Bread" }, out var incompatible) && incompatible == null, "Reject incompatible category atomically");
Check(!AllocationPlan.TryPaste(null, new[] { "Log" }, out _), "Reject empty clipboard");
Check(!AllocationPlan.TryPaste(new Dictionary<string, int> { ["Log"] = 9999 }, new[] { "Log" }, out _), "Reject invalid copied total");
Console.WriteLine($"PASS: {assertions:N0} assertions, including 2,000 randomized allocations, 100-good lists, rounding, persistence, validation, and delivery guards.");

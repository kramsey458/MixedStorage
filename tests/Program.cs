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
// Loading catches FormatException only, so every malformed saved value must fail with exactly that.
foreach (string malformed in new[] { null, "", "1", "1|", "1|A", "1|A=", "1|A=x", "1|A=-1", "1|A=+5", "1|A= 10000", "1|A=99999999999",
             "1|A=10000;", "1|=10000", "1|A=5000;A=5000", "1|A=9999", "1|A=10001", "1|A=10000=1", "2|A=10000" })
{
    bool format = false;
    try { AllocationPlan.Deserialize(malformed); } catch (FormatException) { format = true; }
    Check(format, "Malformed saved allocation fails with FormatException: " + (malformed ?? "null"));
}
var unusual = new Dictionary<string, int> { ["Mod.Good;= |🌲"] = 10000 };
Check(AllocationPlan.Deserialize(AllocationPlan.Serialize(unusual)).Keys.Single() == unusual.Keys.Single(), "Escape modded IDs");
Check(AllocationPlan.ConflictsWithDelivery(45, 10, 50), "Reject incoming delivery over new cap");
Check(!AllocationPlan.ConflictsWithDelivery(40, 10, 50), "Allow delivery exactly to cap");
Check(!AllocationPlan.ConflictsWithDelivery(70, 0, 50), "Existing excess stock can remain without deletion");

// Supply mode (SupplyPatch) offers the goods with the most unreserved stock first. A good with none has nothing to
// carry, so the game's district-wide search for a building to take it could only fail: it is not offered.
string SupplyOrder(IReadOnlyDictionary<string, int> unreserved) =>
    string.Join(",", AllocationPlan.SupplyCandidates(unreserved.Keys, x => unreserved[x]));
var supplyStock = new Dictionary<string, int> { ["A"] = 0, ["B"] = 5, ["C"] = 0, ["D"] = 5 };
Check(SupplyOrder(supplyStock) == "B,D", "Supply offers only goods with unreserved stock, ties in ordinal order; got " + SupplyOrder(supplyStock));
Check(SupplyOrder(new Dictionary<string, int> { ["Log"] = 1, ["Plank"] = 7, ["Gear"] = 3 }) == "Plank,Gear,Log", "Supply offers the most stock first");
Check(SupplyOrder(new Dictionary<string, int> { ["b"] = 3, ["a"] = 3, ["B"] = 3 }) == "B,a,b", "Supply ties use ordinal good IDs, not the player's culture");
Check(SupplyOrder(new Dictionary<string, int> { ["Log"] = -1, ["Plank"] = 0 }) == "" && SupplyOrder(new Dictionary<string, int>()) == "",
    "Supply offers nothing without unreserved stock");
// The same carry as the unfiltered order this replaced, for every input. SupplyPatch carries the first offered good
// that the game's TryCarryToAnyInventory accepts, and that never accepts a good without unreserved stock: it caps
// the load at that stock (CarryAmountCalculator.AmountToCarry) and fails, reserving nothing, when that is 0.
var supplyRng = new Random(55);
var supplyNames = new[] { "A", "a", "B", "b", "Log", "log", "Plank", "Gear", "Bread", "Berries", "Ä", "Mod.Good" };
int searchesSaved = 0;
for (int run = 0; run < 2000; run++)
{
    var unreserved = supplyNames.OrderBy(_ => supplyRng.Next()).Take(supplyRng.Next(0, supplyNames.Length + 1))
        .ToDictionary(x => x, _ => supplyRng.Next(-1, 4));
    var taken = unreserved.Keys.Where(_ => supplyRng.Next(3) > 0).ToHashSet();
    bool Carries(string good) => unreserved[good] > 0 && taken.Contains(good);
    var before = unreserved.Keys.OrderByDescending(x => unreserved[x]).ThenBy(x => x, StringComparer.Ordinal).ToList();
    var after = AllocationPlan.SupplyCandidates(unreserved.Keys, x => unreserved[x]).ToList();
    Check(before.FirstOrDefault(Carries) == after.FirstOrDefault(Carries), "Supply carries the same good as before");
    Check(after.SequenceEqual(before.Take(after.Count)) && after.All(x => unreserved[x] > 0), "Supply drops only goods without unreserved stock");
    Check(after.SequenceEqual(AllocationPlan.SupplyCandidates(unreserved.Keys.Reverse(), x => unreserved[x])), "Supply order is independent of allocation order");
    int Searches(List<string> order) => order.TakeWhile(x => !Carries(x)).Count() + (order.Any(Carries) ? 1 : 0);
    searchesSaved += Searches(before) - Searches(after);
}
Check(searchesSaved > 0, "The randomized Supply checks include searches the filter skips");

// The editor's total line and whether Apply and Copy are available (StorageView.Validate). A saved allocation can
// name a good the building no longer accepts, for example after a goods mod is removed; the line must say why.
void Status(IReadOnlyDictionary<string, int> draft, Func<string, bool> takes, bool fieldsValid, bool valid, string text, string description)
{
    var status = AllocationPlan.DraftStatus(draft, takes, fieldsValid);
    Check(status.Valid == valid && status.Text == text, description + "; got " + status.Valid + ", \"" + status.Text + "\"");
}
Status(new Dictionary<string, int> { ["Log"] = 10000 }, _ => false, true, false, "Set unavailable goods to 0% before applying",
    "A 100% draft with a good this building does not accept says why Apply is off");
Status(new Dictionary<string, int> { ["Log"] = 5000, ["Plank"] = 5000 }, x => x != "Log", true, false, "Set unavailable goods to 0% before applying",
    "One good this building does not accept blocks Apply");
Status(new Dictionary<string, int> { ["Log"] = 0, ["Plank"] = 10000 }, x => x != "Log", true, true, "100% / 100% allocated",
    "A good this building does not accept may stay listed at 0%");
Status(new Dictionary<string, int> { ["Plank"] = 10000 }, _ => true, true, true, "100% / 100% allocated", "A valid draft can be applied");
Status(new Dictionary<string, int> { ["Log"] = 10000 }, _ => false, false, false, "Enter valid percentages (0–100, 2 decimals)",
    "Unreadable fields are reported first");
Status(new Dictionary<string, int> { ["Log"] = 5000, ["Plank"] = 1000 }, x => x != "Log", true, false, "60% / 100% — 40% remaining",
    "The total is reported before goods this building does not accept");
Status(new Dictionary<string, int> { ["A"] = 6000, ["B"] = 5000 }, _ => true, true, false, "110% / 100% — 10% over", "An over-allocated total");
Status(new Dictionary<string, int>(), _ => true, true, false, "0% / 100% — 100% remaining", "An empty draft");
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
Status(new Dictionary<string, int> { ["A"] = 3333 }, _ => true, true, false, "33,33% / 100% — 66,67% remaining", "The total uses the player's number format");
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
// Apply and Copy stay available exactly when they were before: a readable 100% draft of goods this building accepts.
var statusRng = new Random(66);
for (int run = 0; run < 1000; run++)
{
    int count = statusRng.Next(1, 8);
    var cuts = Enumerable.Range(0, count - 1).Select(_ => statusRng.Next(10001)).Append(0).Append(10000).Order().ToArray();
    var draft = Enumerable.Range(0, count).ToDictionary(i => "Good" + i, i => Math.Max(0, cuts[i + 1] - cuts[i] + (statusRng.Next(4) == 0 ? statusRng.Next(-50, 51) : 0)));
    var takenGoods = draft.Keys.Where(_ => statusRng.Next(4) > 0).ToHashSet();
    bool fieldsValid = statusRng.Next(8) > 0;
    var status = AllocationPlan.DraftStatus(draft, takenGoods.Contains, fieldsValid);
    Check(status.Valid == (fieldsValid && AllocationPlan.IsValid(draft) && draft.All(x => x.Value == 0 || takenGoods.Contains(x.Key))),
        "Apply is available exactly for a readable 100% draft of accepted goods");
}

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

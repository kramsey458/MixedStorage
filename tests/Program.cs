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

// The game's Duplicate settings tool onto a mixed warehouse (DuplicatePatch).
const string DeliveryError = "Wait for incoming deliveries to finish before lowering their limits.";
var berriesCarrot = new Dictionary<string, int> { ["Berries"] = 5000, ["Carrot"] = 5000 };
var target = new FakeStorage(180, "Berries", "Carrot", "Potato");
target.StockOf["Berries"] = 20;
target.StockOf["Carrot"] = 30;
CopyPlan Copy(IReadOnlyDictionary<string, int> shares, string good, bool mixed, out string error) =>
    AllocationPlan.PlanCopy(shares, good, mixed, target, out error);
Check(Copy(berriesCarrot, "Berries", true, out string copyError) == CopyPlan.CopyAllocation && copyError == null, "A mixed source copies its allocation");
Check(Copy(berriesCarrot, "Berries", false, out _) == CopyPlan.CopyAllocation, "A mixed source also copies onto a normal building");
Check(Copy(new Dictionary<string, int> { ["Log"] = 10000 }, "Log", true, out copyError) == CopyPlan.Refuse &&
    copyError == "Set unavailable goods to 0% before applying.", "A mixed source with goods the target does not take is refused");
Check(Copy(null, "Berries", true, out copyError) == CopyPlan.LeaveMixed && copyError == null, "A normal source turns a mixed target back into a normal one");
Check(Copy(null, null, true, out _) == CopyPlan.LeaveMixed, "A source storing nothing turns a mixed target back into a normal one");
Check(Copy(null, "Log", true, out _) == CopyPlan.BaseGame, "A good the target does not take is left to the base game, which skips it");
Check(Copy(null, "Berries", false, out _) == CopyPlan.BaseGame && Copy(null, null, false, out _) == CopyPlan.BaseGame, "A normal target follows the base game");
// A hauler is bringing 10 Carrot. Leaving mixed mode gives Carrot no room, and Berries none while Carrot is in stock.
target.IncomingOf["Carrot"] = 10;
Check(Copy(berriesCarrot, "Berries", true, out _) == CopyPlan.CopyAllocation, "An unchanged Carrot limit still fits its delivery");
Check(Copy(new Dictionary<string, int> { ["Berries"] = 10000 }, "Berries", true, out copyError) == CopyPlan.Refuse && copyError == DeliveryError,
    "A copied allocation that drops Carrot waits for the Carrot delivery");
Check(Copy(null, "Berries", true, out copyError) == CopyPlan.Refuse && copyError == DeliveryError, "Leaving mixed mode for Berries waits for the Carrot delivery");
Check(Copy(null, null, true, out copyError) == CopyPlan.Refuse && copyError == DeliveryError, "Leaving mixed mode for no good waits for the Carrot delivery");
Check(Copy(null, "Carrot", true, out copyError) == CopyPlan.Refuse && copyError == DeliveryError, "Carrot has no room either while Berries are in stock");
Check(Copy(null, "Berries", false, out _) == CopyPlan.BaseGame, "A delivery does not stop the base game's copy onto a normal building");
target.StockOf.Remove("Berries");
Check(Copy(null, "Carrot", true, out copyError) == CopyPlan.LeaveMixed && copyError == null, "Once only Carrot is in stock, keeping Carrot fits its delivery");
Check(Copy(null, "Berries", true, out _) == CopyPlan.Refuse, "Berries still give Carrot no room");
target.IncomingOf.Remove("Carrot");
target.IncomingOf["Potato"] = 5;
Check(Copy(null, "Berries", true, out _) == CopyPlan.Refuse, "A delivery of a good outside the allocation cannot fit either");
Check(Copy(null, "Potato", true, out _) == CopyPlan.Refuse, "Potato has no room while Carrot is in stock");
target.StockOf.Remove("Carrot");
Check(Copy(null, "Potato", true, out _) == CopyPlan.LeaveMixed, "Keeping the delivered good fits once nothing else is in stock");

// BeaverBuddies replays a copy on every player with the same simulation state, but the game lists a building's
// accepted goods in hash-set order. The decision must depend on its inputs alone, never on that order.
var pool = Enumerable.Range(0, 12).Select(i => "Good" + i).ToArray();
var outcomes = new HashSet<CopyPlan>();
for (int run = 0; run < 2000; run++)
{
    int capacity = new[] { 30, 180, 200, 1200 }[run % 4];
    var accepted = pool.Where(_ => rng.Next(3) > 0).DefaultIfEmpty(pool[0]).ToArray();
    var forward = new FakeStorage(capacity, accepted);
    var backward = new FakeStorage(capacity, accepted.Reverse().ToArray());
    foreach (string good in accepted)
    {
        if (rng.Next(2) == 0) forward.StockOf[good] = backward.StockOf[good] = rng.Next(1, capacity / 4 + 1);
        if (rng.Next(6) == 0) forward.IncomingOf[good] = backward.IncomingOf[good] = rng.Next(1, 10);
    }
    Dictionary<string, int> shares = null;
    if (rng.Next(2) == 0)
    {
        var chosen = pool.Where(x => rng.Next(4) == 0 || x == accepted[0]).Where(x => rng.Next(8) > 0 || accepted.Contains(x)).ToArray();
        var cuts = Enumerable.Range(0, chosen.Length - 1).Select(_ => rng.Next(10001)).Append(0).Append(10000).Order().ToArray();
        shares = Enumerable.Range(0, chosen.Length).ToDictionary(i => chosen[i], i => cuts[i + 1] - cuts[i]);
    }
    string single = rng.Next(4) == 0 ? null : pool[rng.Next(pool.Length)];
    bool mixed = rng.Next(3) > 0;
    var plan = AllocationPlan.PlanCopy(shares, single, mixed, forward, out string forwardError);
    var reordered = shares?.Reverse().ToDictionary(x => x.Key, x => x.Value);
    Check(AllocationPlan.PlanCopy(reordered, single, mixed, backward, out string backwardError) == plan && backwardError == forwardError,
        "The copy decision does not depend on the order of goods");
    outcomes.Add(plan);
}
Check(outcomes.Count == 4, "Randomized copies reach every outcome");
Console.WriteLine($"PASS: {assertions:N0} assertions, including 2,000 randomized allocations, 2,000 randomized copies checked in both goods orders, 100-good lists, rounding, persistence, validation, copied settings, and delivery guards.");

// A building for the allocation guards: capacity, accepted goods, stock and incoming deliveries.
sealed class FakeStorage : IStorageContents
{
    private readonly string[] _goods;
    public readonly Dictionary<string, int> StockOf = new(), IncomingOf = new();
    public FakeStorage(int capacity, params string[] goods) { Capacity = capacity; _goods = goods; }
    public int Capacity { get; }
    public IEnumerable<string> Goods => _goods;
    public bool Takes(string good) => _goods.Contains(good);
    public int Stock(string good) => StockOf.GetValueOrDefault(good);
    public int Incoming(string good) => IncomingOf.GetValueOrDefault(good);
}

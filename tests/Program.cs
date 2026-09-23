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
Status(new Dictionary<string, int> { ["A"] = 10001, ["B"] = -1 }, _ => true, true, false, "100% / 100% allocated",
    "A 100% total with a share out of range still cannot be applied");
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

// Every good at 0% (Clear all, or resetting the last allocated good) applies too: the building stores nothing, as when
// it was just built. Copy stays off for it (StorageView.Validate), since there is no allocation to copy.
const string NothingText = "0% allocated — Apply to store nothing";
Status(new Dictionary<string, int> { ["Log"] = 0, ["Plank"] = 0 }, _ => true, true, true, NothingText, "A draft of all 0% can be applied");
Status(new Dictionary<string, int> { ["Log"] = 0 }, _ => false, true, true, NothingText, "Goods this building does not accept may stay at 0% when storing nothing");
Status(new Dictionary<string, int> { ["Log"] = 0 }, _ => true, false, false, "Enter valid percentages (0–100, 2 decimals)", "Unreadable fields still come first");
Check(AllocationPlan.IsNothing(new Dictionary<string, int> { ["Log"] = 0, ["Plank"] = 0 }), "All 0% stores nothing");
Check(!AllocationPlan.IsNothing(new Dictionary<string, int> { ["Log"] = 0, ["Plank"] = 1 }), "0.01% of one good is not nothing");
Check(!AllocationPlan.IsNothing(new Dictionary<string, int>()) && !AllocationPlan.IsNothing(null), "A draft without goods is not an Apply to store nothing");
// Apply's payload: an allocation reads back as before, and storing nothing as no goods. Saves never read the latter.
var nothingDraft = new Dictionary<string, int> { ["Log"] = 0, ["Plank"] = 0 };
Check(AllocationPlan.SerializeCommand(nothingDraft) == AllocationPlan.Nothing && AllocationPlan.DeserializeCommand(AllocationPlan.Nothing).Count == 0,
    "Storing nothing is sent as no goods");
var halves = new Dictionary<string, int> { ["Log"] = 5000, ["Plank"] = 5000, ["Gear"] = 0 };
var sentHalves = AllocationPlan.DeserializeCommand(AllocationPlan.SerializeCommand(halves));
Check(sentHalves.Count == 2 && sentHalves["Log"] == 5000 && sentHalves["Plank"] == 5000, "An allocation is sent as before");
Check(AllocationPlan.SerializeCommand(halves) == AllocationPlan.Serialize(halves), "An allocation's payload is unchanged, so saves are too");
Reject(() => AllocationPlan.Deserialize(AllocationPlan.Nothing), "A save cannot hold an allocation of nothing");
Reject(() => AllocationPlan.SerializeCommand(new Dictionary<string, int> { ["Log"] = 5000 }), "A 50% draft cannot be sent");
foreach (string malformed in new[] { null, "", "1", "1|A", "1|A=9999", "2|" })
{
    bool format = false;
    try { AllocationPlan.DeserializeCommand(malformed); } catch (FormatException) { format = true; }
    Check(format, "A malformed Apply payload fails with FormatException: " + (malformed ?? "null"));
}
// Storing nothing lowers every limit to 0, so it waits for every incoming delivery (StorageState.TryClear).
var clearing = new FakeStorage(180, "Log", "Plank");
clearing.StockOf["Log"] = 40;
Check(AllocationPlan.CanLeave(null, clearing, out string clearError) && clearError == null, "Stock alone does not stop storing nothing; it stays as excess");
clearing.IncomingOf["Plank"] = 5;
Check(!AllocationPlan.CanLeave(null, clearing, out clearError) && clearError == "Wait for incoming deliveries to finish before lowering their limits.",
    "Storing nothing waits for an incoming delivery");

// Apply stands out while the draft differs from what the building has applied (StorageView.ShowUnapplied).
var applied = new Dictionary<string, int> { ["Log"] = 10000, ["Plank"] = 0 };
Check(!AllocationPlan.Differs(new Dictionary<string, int> { ["Plank"] = 0, ["Log"] = 10000 }, applied), "The applied allocation, in any order, is not a change");
Check(AllocationPlan.Differs(new Dictionary<string, int> { ["Log"] = 0, ["Plank"] = 10000 }, applied), "Max on another good is a change");
Check(AllocationPlan.Differs(new Dictionary<string, int> { ["Log"] = 0, ["Plank"] = 0 }, applied), "Clearing every good is a change");
Check(AllocationPlan.Differs(new Dictionary<string, int> { ["Log"] = 10000 }, applied) &&
    AllocationPlan.Differs(new Dictionary<string, int> { ["Log"] = 10000, ["Gear"] = 0 }, applied), "A different list of goods is a change");

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
Check(Copy(null, null, true, out copyError) == CopyPlan.KeepAllocation && copyError == null, "A source storing nothing, such as a new building, keeps a mixed target's allocation");
Check(Copy(null, "Log", true, out _) == CopyPlan.BaseGame, "A good the target does not take is left to the base game, which skips it");
Check(Copy(null, "Berries", false, out _) == CopyPlan.BaseGame && Copy(null, null, false, out _) == CopyPlan.BaseGame, "A normal target follows the base game");
// A hauler is bringing 10 Carrot. Leaving mixed mode gives Carrot no room, and Berries none while Carrot is in stock.
target.IncomingOf["Carrot"] = 10;
Check(Copy(berriesCarrot, "Berries", true, out _) == CopyPlan.CopyAllocation, "An unchanged Carrot limit still fits its delivery");
Check(Copy(new Dictionary<string, int> { ["Berries"] = 10000 }, "Berries", true, out copyError) == CopyPlan.Refuse && copyError == DeliveryError,
    "A copied allocation that drops Carrot waits for the Carrot delivery");
Check(Copy(null, "Berries", true, out copyError) == CopyPlan.Refuse && copyError == DeliveryError, "Leaving mixed mode for Berries waits for the Carrot delivery");
Check(Copy(null, null, true, out copyError) == CopyPlan.KeepAllocation && copyError == null, "A source storing nothing keeps the allocation without waiting for deliveries");
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
Check(outcomes.Count == Enum.GetValues<CopyPlan>().Length, "Randomized copies reach every outcome");

// What DuplicatePatch does with each plan (AllocationPlan.Steps): only a leave deactivates, only a refusal is logged,
// and a refusal touches nothing, so a refused copy can neither wipe an allocation nor silently keep a stale one.
foreach (var plan in Enum.GetValues<CopyPlan>())
{
    var steps = AllocationPlan.Steps(plan);
    Check(((steps & CopySteps.LeaveMixed) != 0) == (plan == CopyPlan.LeaveMixed), "Only leaving mixed mode deactivates the target");
    Check(((steps & CopySteps.LogRefusal) != 0) == (plan == CopyPlan.Refuse), "Only a refused copy is logged");
    Check(((steps & CopySteps.ApplyAllocation) != 0) == (plan == CopyPlan.CopyAllocation), "Only a mixed source's allocation is applied");
}
Check(AllocationPlan.Steps(CopyPlan.Refuse) == CopySteps.LogRefusal, "A refused copy changes nothing on the target: no leave, no base game copy, no apply");
Check(AllocationPlan.Steps(CopyPlan.LeaveMixed) == (CopySteps.LeaveMixed | CopySteps.RunBaseGame), "Leaving mixed mode comes first, then the base game gives the target the source's good");
Check(AllocationPlan.Steps(CopyPlan.CopyAllocation) == (CopySteps.RunBaseGame | CopySteps.ApplyAllocation), "A copied allocation applies after the base game's copy");
Check(AllocationPlan.Steps(CopyPlan.BaseGame) == CopySteps.RunBaseGame, "Anything else is the base game's copy");
Check(AllocationPlan.Steps(CopyPlan.KeepAllocation) == CopySteps.None, "Keeping the allocation skips the base game's copy, which would clear the target's good");
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

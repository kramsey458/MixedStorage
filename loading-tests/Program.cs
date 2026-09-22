using System.Reflection;
using System.Runtime.Loader;

var mode = args[0];
var mainPath = Path.GetFullPath(args[1]);
var managed = Path.Combine(args[2], "Timberborn_Data", "Managed");
var harmony = Path.GetDirectoryName(args[3])!;
var bbPath = args[4];
// "incompatible" loads a BeaverBuddies whose API differs from the one the bridge was built against.
bool beaverBuddies = mode == "with" || mode == "incompatible";
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var dirs = beaverBuddies ? new[] { managed, harmony, Path.GetDirectoryName(bbPath)! } : new[] { managed, harmony };
    foreach (var dir in dirs)
    {
        var file = Path.Combine(dir, name.Name + ".dll");
        if (File.Exists(file)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
    }
    return null;
};
if (beaverBuddies) Assembly.Load(File.ReadAllBytes(bbPath));
var main = Assembly.Load(File.ReadAllBytes(mainPath));
if (main.GetReferencedAssemblies().Any(a => a.Name!.Contains("BeaverBuddies") || a.Name.Contains("MultiplayerBridge")))
    throw new Exception("Main mod has a hard multiplayer dependency.");
var types = main.GetTypes(); // Same eager enumeration used by Timberborn's mod loader.
var init = main.GetType("MixedStorage.OptionalMultiplayer")!.GetMethod("Initialize")!;
if (mode == "legacy")
{
    System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("MixedStorage.BeaverBuddies"), System.Reflection.Emit.AssemblyBuilderAccess.Run);
    try { init.Invoke(null, null); throw new Exception("Legacy addon was accepted."); }
    catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException && ex.InnerException.Message.Contains("Remove or disable"))
    { Console.WriteLine("PASS: legacy separate addon rejected with upgrade instructions."); return; }
}
if (mode == "incompatible")
{
    // The game must still start: the bridge is skipped with a reason, and nothing is installed.
    var optional = main.GetType("MixedStorage.OptionalMultiplayer")!;
    if ((bool)init.Invoke(null, null)!) throw new Exception("Incompatible BeaverBuddies was accepted.");
    var reason = (string)optional.GetProperty("UnavailableReason")!.GetValue(null)!;
    if (reason == null || !reason.Contains("(BeaverBuddies has no ReplayEvent.DoPrefix)") || optional.GetProperty("Failure")!.GetValue(null) == null)
        throw new Exception("Missing or wrong reason for the incompatible BeaverBuddies: " + reason);
    if (main.GetType("MixedStorage.AllocationCommands")!.GetField("MultiplayerSubmit")!.GetValue(null) != null)
        throw new Exception("Multiplayer submit delegate installed for an incompatible BeaverBuddies.");
    if ((bool)init.Invoke(null, null)! || AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "MixedStorage.MultiplayerBridge") != 1)
        throw new Exception("A failed bridge is loaded again on the next initialization.");
    // Without the bridge, Apply may only change allocations locally when BeaverBuddies is not connected,
    // and must treat an unreadable BeaverBuddies as connected.
    var singlePlayer = main.GetType("MixedStorage.AllocationCommands")!.GetMethod("IsSinglePlayer", BindingFlags.NonPublic | BindingFlags.Static)!;
    var stub = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "BeaverBuddies");
    var connection = stub.GetType("BeaverBuddies.IO.StubConnection")!.GetField("Connected")!;
    if (!(bool)singlePlayer.Invoke(null, new object[] { stub })!) throw new Exception("Single-player BeaverBuddies was treated as connected.");
    connection.SetValue(null, true);
    if ((bool)singlePlayer.Invoke(null, new object[] { stub })!) throw new Exception("Connected BeaverBuddies was treated as single-player.");
    if ((bool)singlePlayer.Invoke(null, new object[] { typeof(object).Assembly })!) throw new Exception("Unreadable BeaverBuddies was treated as single-player.");
    Console.WriteLine("PASS: incompatible BeaverBuddies skipped without stopping startup (" + reason + "); multiplayer Apply refused without the bridge.");
    return;
}
bool loaded = (bool)init.Invoke(null, null)!;
if (loaded != (mode == "with")) throw new Exception("Incorrect optional loading result.");
var bridge = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == "MixedStorage.MultiplayerBridge");
if (mode == "without" && bridge != null) throw new Exception("Bridge loaded without BeaverBuddies.");
if (mode == "with")
{
    if (bridge == null || bridge.GetTypes().All(t => t.Name != "StorageAllocationEvent")) throw new Exception("Replay event missing.");
    var field = main.GetType("MixedStorage.AllocationCommands")!.GetField("MultiplayerSubmit")!;
    var first = field.GetValue(null);
    if (first == null) throw new Exception("Multiplayer submit delegate not installed.");
    init.Invoke(null, null);
    if (!ReferenceEquals(first, field.GetValue(null))) throw new Exception("Initialization is not idempotent.");
}
Console.WriteLine($"PASS: {mode} BeaverBuddies — enumerated {types.Length} main types; optional load and delegate checks passed.");

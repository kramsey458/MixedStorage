using System.Reflection;
using System.Runtime.Loader;

var mode = args[0];
var mainPath = Path.GetFullPath(args[1]);
var managed = Path.Combine(args[2], "Timberborn_Data", "Managed");
var harmony = Path.GetDirectoryName(args[3])!;
var bbPath = args[4];
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var dirs = mode == "with" ? new[] { managed, harmony, Path.GetDirectoryName(bbPath)! } : new[] { managed, harmony };
    foreach (var dir in dirs)
    {
        var file = Path.Combine(dir, name.Name + ".dll");
        if (File.Exists(file)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
    }
    return null;
};
if (mode == "with") Assembly.Load(File.ReadAllBytes(bbPath));
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

using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using HarmonyLib;

internal static class GameApiTests
{
    public static void Run(string gameDir, string modPath)
    {
        var managed = Path.Combine(gameDir, "Timberborn_Data", "Managed");
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(managed, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        Type GameType(string assembly, string name) =>
            Assembly.LoadFrom(Path.Combine(managed, assembly + ".dll")).GetType(name, true)!;
        var good = GameType("Timberborn.Goods", "Timberborn.Goods.GoodSpec");
        int checks = 0;
        void Method(Type type, string name, params Type[] signature)
        {
            var method = AccessTools.Method(type, name, signature);
            if (method == null || !method.GetParameters().Select(p => p.ParameterType).SequenceEqual(signature))
                throw new Exception($"Invalid game API binding: {type.FullName}.{name}");
            checks++;
        }
        bool reproduced = false;
        foreach (var name in new[] { "StockpileGoodColumnVisualizer", "StockpileGoodPileVisualizer", "StockpilePlaneVisualizer" })
        {
            var type = GameType("Timberborn.StockpileVisualization", "Timberborn.StockpileVisualization." + name);
            try { AccessTools.Method(type, "Initialize"); }
            catch (AmbiguousMatchException) { reproduced = true; }
            Method(type, "Initialize", good, typeof(int));
            Method(type, "UpdateAmount", typeof(int));
        }
        var materials = GameType("Timberborn.Rendering", "Timberborn.Rendering.EntityMaterials");
        Method(materials, "AddMaterial", typeof(UnityEngine.Transform), typeof(UnityEngine.Material));
        Method(materials, "DestroyMaterial", typeof(UnityEngine.Material));
        Method(GameType("Timberborn.SelectionSystem", "Timberborn.SelectionSystem.HighlightableObject"), "UpdateColorAndHighlight");
        // StorageState announces every good whose limit it changes through this private method.
        var allower = GameType("Timberborn.InventorySystem", "Timberborn.InventorySystem.SingleGoodAllower");
        Method(allower, "InvokeDisallowedGoodsChangedEvent", typeof(string));
        if (!reproduced) throw new Exception("Expected v0.4.0 Initialize ambiguity was not reproduced; review regression fixture.");
        Console.WriteLine("PASS: reproduced v0.4.0 name-only Initialize lookup failure; typed lookups succeed.");
        Console.WriteLine($"PASS: {checks} native API signatures (rendering, and the goods announcement) resolved against installed game assemblies.");
        GoodsAnnouncement(modPath, allower);
        AnnouncementTests.Run(Assembly.LoadFrom(modPath), GameType);
    }

    // The built mod resolves the goods announcement at startup. If a game update renamed or changed it, the
    // mod must say so and refuse to change allocations instead of half-applying one (shares set and the
    // representative good switched, but the game's inventories never told about the new limits).
    private static void GoodsAnnouncement(string modPath, Type allower)
    {
        const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
        const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
        const string Name = "InvokeDisallowedGoodsChangedEvent";
        var state = Assembly.LoadFrom(modPath).GetType("MixedStorage.StorageState", true)!;
        var notify = state.GetField("NotifyGood", Static)?.GetValue(null) as MethodInfo;
        if (notify == null || notify != AccessTools.Method(allower, Name, new[] { typeof(string) }))
            throw new Exception($"MixedStorage.StorageState.NotifyGood does not resolve to SingleGoodAllower.{Name}(string).");
        var reasonProperty = state.GetProperty("UnavailableReason", Static)
            ?? throw new Exception($"MixedStorage.StorageState has no UnavailableReason: nothing checks {Name} at startup, so a game without it would fail in the middle of Apply.");
        if (reasonProperty.GetValue(null) is string unexpected) throw new Exception("Allocations are unavailable with the installed game: " + unexpected);
        if (AllocationProblem(state.Assembly) is string problem) throw new Exception("ModStarter reports a problem with the installed game: " + problem);
        var shares = new Dictionary<string, int>(StringComparer.Ordinal) { ["Log"] = 10000 };
        var working = RuntimeHelpers.GetUninitializedObject(state);
        state.GetProperty("Shares")!.SetValue(working, shares);
        if (state.GetMethod("FrozenCopyReason", Instance)!.Invoke(working, new[] { working }) != null)
            throw new Exception("Copy Settings between mixed buildings is refused with the installed game.");
        StartupCode(state, Name);

        // The same mod, as if the game had renamed the method: the name is changed in the DLL's string heap.
        var broken = LoadRenamed(modPath, Encoding.Unicode.GetBytes(Name), Encoding.Unicode.GetBytes(Name.Substring(0, Name.Length - 1) + "X"), Name)
            .GetType("MixedStorage.StorageState", true)!;
        if (broken.GetField("NotifyGood", Static)!.GetValue(null) != null) throw new Exception("The renamed method still resolved.");
        if (!(broken.GetProperty("UnavailableReason", Static)!.GetValue(null) is string reason))
            throw new Exception($"A game without {Name} starts without a reason.");
        if (AllocationProblem(broken.Assembly) != reason) throw new Exception($"ModStarter does not report why allocations cannot change without {Name}.");
        // Both must stop before they touch the building, so this storage needs no game objects.
        var storage = RuntimeHelpers.GetUninitializedObject(broken);
        object Call(string method, string failure, params object[] arguments)
        {
            try { return broken.GetMethod(method)!.Invoke(storage, arguments); }
            catch (TargetInvocationException ex) { throw new Exception(failure + " " + ex.InnerException!.GetType().Name, ex); }
        }
        var canApply = new object[] { shares, null };
        if ((bool)Call("CanApply", $"Apply went ahead without {Name}:", canApply) || (string)canApply[1] != reason)
            throw new Exception($"Apply was not refused with the startup reason without {Name}: {canApply[1]}");
        // Copy settings from a single-good building must not leave mixed mode unannounced.
        broken.GetProperty("Shares")!.SetValue(storage, shares);
        Call("Deactivate", $"A mixed building tried to leave mixed mode without {Name}:");
        if (broken.GetProperty("Shares")!.GetValue(storage) != shares) throw new Exception($"A mixed building left mixed mode without {Name}.");
        // DuplicatePatch refuses, with the reason, every copy that would set or leave an allocation, and only those.
        var single = RuntimeHelpers.GetUninitializedObject(broken);
        var frozenCopy = broken.GetMethod("FrozenCopyReason", Instance)!;
        string Copy(object target, object source) => (string)frozenCopy.Invoke(target, new[] { source });
        if (Copy(storage, null) != reason || Copy(storage, single) != reason || Copy(single, storage) != reason)
            throw new Exception($"Copy Settings could set or leave an allocation without {Name}.");
        if (Copy(single, null) != null || Copy(single, single) != null)
            throw new Exception($"Copy Settings between single-good buildings was refused without {Name}; the base game's copy should run.");
        Console.WriteLine($"PASS: without SingleGoodAllower.{Name} the mod reports why at startup, refuses Apply and copies that would change an allocation, and keeps mixed buildings mixed.");

        // A StorageState that cannot even load after a game update (here a game type it keeps in a static field is
        // renamed, in the DLL's metadata names) is reported at startup too, not thrown out of StartMod.
        var unloadable = LoadRenamed(modPath, Encoding.UTF8.GetBytes("\0ComponentKey\0"), Encoding.UTF8.GetBytes("\0ComponentKeX\0"), "ComponentKey");
        string failure;
        try { failure = AllocationProblem(unloadable); }
        catch (TargetInvocationException ex) { throw new Exception("ModStarter.AllocationProblem threw when StorageState could not load: " + ex.InnerException, ex); }
        if (failure == null || !failure.Contains("ComponentKeX")) throw new Exception("ModStarter did not report a StorageState that cannot load: " + failure);
        bool loads;
        try { unloadable.GetType("MixedStorage.StorageState", true)!.GetProperty("UnavailableReason", Static)!.GetValue(null); loads = true; }
        catch (Exception) { loads = false; }
        if (loads) throw new Exception("Renaming ComponentKey no longer breaks StorageState; pick another type for this check.");
        Console.WriteLine("PASS: a StorageState that cannot load after a game update is reported at startup instead of stopping the game from starting.");
    }

    private static string AllocationProblem(Assembly mod) =>
        (string)(mod.GetType("MixedStorage.ModStarter", true)!.GetMethod("AllocationProblem", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("MixedStorage.ModStarter has no AllocationProblem: StartMod reads StorageState outside any catch, so a StorageState that cannot start would stop the game from starting."))
        .Invoke(null, null);

    // A copy of the built mod with every occurrence of a name changed, loaded next to the real one.
    private static Assembly LoadRenamed(string modPath, byte[] from, byte[] to, string label)
    {
        var image = File.ReadAllBytes(modPath);
        int renamed = 0;
        for (int i = 0; i <= image.Length - from.Length; i++)
            if (image.AsSpan(i, from.Length).SequenceEqual(from)) { to.CopyTo(image, i); renamed++; }
        if (renamed == 0) throw new Exception($"{label} is not a name in the built mod.");
        var context = new AssemblyLoadContext("MixedStorage without " + label);
        context.Resolving += (_, name) => AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        return context.LoadFromStream(new MemoryStream(image));
    }

    // What the tests above cannot run: StartMod itself (it needs Unity and Harmony's runtime). Its JIT must not load
    // StorageState, which only ReadAllocationProblem touches, inside AllocationProblem's catch; and StorageState
    // must look the announcement up by its (string) signature, or an overload added by a game update would make
    // StorageState fail to start.
    private static void StartupCode(Type state, string name)
    {
        var starter = state.Assembly.GetType("MixedStorage.ModStarter", true)!;
        var start = starter.GetMethod("StartMod")!;
        var members = IlReader.Read(start).Select(i => IlReader.Member(start, i)).ToList();
        int asked = members.FindIndex(m => m is MethodInfo method && method.DeclaringType == starter && method.Name == "AllocationProblem");
        int patched = members.FindIndex(m => m is ConstructorInfo c && c.DeclaringType?.FullName == "HarmonyLib.Harmony");
        if (asked < 0 || patched < 0 || asked > patched) throw new Exception("ModStarter.StartMod does not report AllocationProblem before it patches the game.");
        if (members.Any(m => m != null && (m == state || m.DeclaringType == state)))
            throw new Exception("ModStarter.StartMod uses StorageState directly, so a StorageState that cannot load would stop the game from starting.");
        var read = starter.GetMethod("ReadAllocationProblem", BindingFlags.NonPublic | BindingFlags.Static);
        if (read == null || (read.MethodImplementationFlags & MethodImplAttributes.NoInlining) == 0)
            throw new Exception("ModStarter.ReadAllocationProblem must exist and must not be inlined into AllocationProblem's try block.");

        var init = state.TypeInitializer!;
        var code = IlReader.Read(init);
        int at = code.FindIndex(i => i.Code == OpCodes.Ldstr && init.Module.ResolveString(i.Token) == name);
        int call = at < 0 ? -1 : code.FindIndex(at, i => i.Code == OpCodes.Call && IlReader.Member(init, i) is MethodInfo { Name: "Method" } m && m.DeclaringType?.FullName == "HarmonyLib.AccessTools");
        if (call < 0) throw new Exception($"StorageState's static setup no longer looks {name} up with AccessTools.Method; update this check.");
        var lookup = (MethodInfo)IlReader.Member(init, code[call])!;
        var signature = code.GetRange(at, call - at).Where(i => i.Code == OpCodes.Ldtoken).Select(i => IlReader.Member(init, i)).ToList();
        if (lookup.GetParameters().ElementAtOrDefault(2)?.ParameterType != typeof(Type[]) || signature.Count != 1 || !Equals(signature[0], typeof(string)))
            throw new Exception($"StorageState looks {name} up without its (string) signature; an overload added by a game update would make StorageState fail to start.");

        // DuplicatePatch cannot run here either (it needs the game's components): it must ask FrozenCopyReason
        // before it decides anything else about the copy.
        var prefix = state.Assembly.GetType("MixedStorage.DuplicatePatch", true)!.GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static)!;
        var calls = IlReader.Calls(prefix).ToList();
        int frozen = calls.FindIndex(m => m.DeclaringType == state && m.Name == "FrozenCopyReason");
        int decided = calls.FindIndex(m => m.DeclaringType == state && (m.Name == "CanApply" || m.Name == "Deactivate" || m.Name == "Duplicate"));
        if (frozen < 0 || decided >= 0 && decided < frozen)
            throw new Exception("DuplicatePatch.Prefix does not check FrozenCopyReason before it decides the copy, so a frozen copy could be half-applied.");
        Console.WriteLine("PASS: StartMod reports allocation problems before patching without touching StorageState, StorageState looks the announcement up by signature, and Copy Settings checks for frozen allocations first.");
    }
}

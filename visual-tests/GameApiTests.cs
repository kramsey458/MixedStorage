using System.Reflection;
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
    }

    // The built mod resolves the goods announcement at startup. If a game update renamed or changed it, the
    // mod must say so and refuse to change allocations instead of half-applying one (shares set and the
    // representative good switched, but the game's inventories never told about the new limits).
    private static void GoodsAnnouncement(string modPath, Type allower)
    {
        const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
        const string Name = "InvokeDisallowedGoodsChangedEvent";
        var state = Assembly.LoadFrom(modPath).GetType("MixedStorage.StorageState", true)!;
        var notify = state.GetField("NotifyGood", Static)?.GetValue(null) as MethodInfo;
        if (notify == null || notify != AccessTools.Method(allower, Name, new[] { typeof(string) }))
            throw new Exception($"MixedStorage.StorageState.NotifyGood does not resolve to SingleGoodAllower.{Name}(string).");
        var reasonProperty = state.GetProperty("UnavailableReason", Static)
            ?? throw new Exception($"MixedStorage.StorageState has no UnavailableReason: nothing checks {Name} at startup, so a game without it would fail in the middle of Apply.");
        if (reasonProperty.GetValue(null) is string unexpected) throw new Exception("Allocations are unavailable with the installed game: " + unexpected);

        // The same mod, as if the game had renamed the method: the name is changed in the DLL's string heap.
        var image = File.ReadAllBytes(modPath);
        byte[] from = Encoding.Unicode.GetBytes(Name), to = Encoding.Unicode.GetBytes(Name.Substring(0, Name.Length - 1) + "X");
        int renamed = 0;
        for (int i = 0; i <= image.Length - from.Length; i++)
            if (image.AsSpan(i, from.Length).SequenceEqual(from)) { to.CopyTo(image, i); renamed++; }
        if (renamed == 0) throw new Exception($"{Name} is not a string in the built mod.");
        var context = new AssemblyLoadContext("MixedStorage without " + Name);
        context.Resolving += (_, name) => AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        var broken = context.LoadFromStream(new MemoryStream(image)).GetType("MixedStorage.StorageState", true)!;
        if (broken.GetField("NotifyGood", Static)!.GetValue(null) != null) throw new Exception("The renamed method still resolved.");
        if (!(broken.GetProperty("UnavailableReason", Static)!.GetValue(null) is string reason))
            throw new Exception($"A game without {Name} starts without a reason.");
        // Both must stop before they touch the building, so this storage needs no game objects.
        var storage = RuntimeHelpers.GetUninitializedObject(broken);
        var shares = new Dictionary<string, int>(StringComparer.Ordinal) { ["Log"] = 10000 };
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
        Console.WriteLine($"PASS: without SingleGoodAllower.{Name} the mod reports why at startup, refuses to change allocations, and keeps mixed buildings mixed.");
    }
}

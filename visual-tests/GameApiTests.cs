using System.Reflection;
using System.Runtime.Loader;
using HarmonyLib;

internal static class GameApiTests
{
    public static void Run(string gameDir)
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
        if (!reproduced) throw new Exception("Expected v0.4.0 Initialize ambiguity was not reproduced; review regression fixture.");
        Console.WriteLine("PASS: reproduced v0.4.0 name-only Initialize lookup failure; typed lookups succeed.");
        Console.WriteLine($"PASS: {checks} native rendering API signatures resolved against installed game assemblies.");
    }
}

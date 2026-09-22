using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;

// Everything the built mod finds in the game by name. Harmony resolves patch targets, "___field" injections and
// parameter names when ModStarter calls PatchAll, and the game's mod starter does not catch what that throws, so
// a game update that renamed any of them would stop the game from starting. The rest is read by reflection.
// GameApiTests.Run has already installed the resolver for the game's Managed folder.
internal static class PatchTargetTests
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    private static readonly string[] PatchKinds = { "Prefix", "Postfix", "Finalizer", "Transpiler" };
    private static readonly string[] Unsupported = { "TargetMethods", "Prepare", "Cleanup", "ReversePatch", "ILManipulator" };

    public static void Run(string gameDir, string modPath, string beaverBuddiesPath)
    {
        var managed = Path.Combine(gameDir, "Timberborn_Data", "Managed");
        // The game loads all of its assemblies before mods start; string targets are looked up among loaded ones.
        foreach (var file in Directory.GetFiles(managed, "Timberborn.*.dll")) Assembly.LoadFrom(file);
        var mod = Assembly.LoadFrom(modPath);
        var types = mod.GetTypes();
        var patches = HarmonyPatches(types);
        LateGamePerformanceContract(mod, patches);
        ReflectedMembers(types, managed);
        ConnectionState(mod, beaverBuddiesPath);
        Templates(mod, Path.Combine(gameDir, "Timberborn_Data", "StreamingAssets", "Modding", "Blueprints.zip"));
    }

    // Mirrors Harmony 2.4's PatchClassProcessor and MethodPatcher for the features the mod uses; anything else fails
    // here so this check is extended before it is relied on.
    private static List<(MethodBase Target, MethodInfo Patch)> HarmonyPatches(Type[] types)
    {
        var resolved = new List<(MethodBase Target, MethodInfo Patch)>();
        int classes = 0, targets = 0, fields = 0, parameters = 0;
        foreach (var type in types.Where(t => t.GetCustomAttributes(true).OfType<HarmonyAttribute>().Any()).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            classes++;
            var container = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(type));
            var methods = type.GetMethods(All);
            MethodInfo Auxiliary(string name) => methods.SingleOrDefault(m => m.Name == name || m.GetCustomAttributes(true).Any(a => a.GetType().FullName == "HarmonyLib.Harmony" + name));
            foreach (var name in Unsupported)
                if (Auxiliary(name) != null) throw new Exception($"{type.FullName} uses Harmony's {name}, which PatchTargetTests does not check yet.");
            var targetMethod = Auxiliary("TargetMethod");
            var patches = methods.Where(m => PatchKinds.Any(kind => m.Name == kind || m.GetCustomAttributes(true).Any(a => a.GetType().FullName == "HarmonyLib.Harmony" + kind))).ToList();
            if (patches.Count == 0) throw new Exception($"{type.FullName} has Harmony attributes but no patch method.");
            foreach (var patch in patches)
            {
                string where = $"{type.FullName}.{patch.Name}";
                MethodBase original;
                if (targetMethod != null)
                {
                    if (targetMethod.GetParameters().Length != 0) throw new Exception($"{type.FullName}.TargetMethod takes parameters, which PatchTargetTests does not check yet.");
                    original = (MethodBase)targetMethod.Invoke(null, null);
                }
                else
                {
                    var info = container.Merge(HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromMethod(patch)));
                    if ((info.methodType ?? MethodType.Normal) != MethodType.Normal) throw new Exception($"{where} patches a {info.methodType}, which PatchTargetTests does not check yet.");
                    original = info.declaringType == null || string.IsNullOrEmpty(info.methodName) ? null
                        : AccessTools.DeclaredMethod(info.declaringType, info.methodName, info.argumentTypes);
                }
                if (original == null) throw new Exception($"Patch target not found in the game: {where}.");
                resolved.Add((original, patch));
                targets++;
                string target = $"{original.DeclaringType!.FullName}.{original.Name}";
                if (patch.Name == "Transpiler") continue;
                var originalParameters = original.GetParameters();
                foreach (var parameter in patch.GetParameters())
                {
                    string name = parameter.Name!;
                    var type2 = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
                    if (parameter.GetCustomAttributes(true).Any(a => a.GetType().FullName == "HarmonyLib.HarmonyArgument"))
                        throw new Exception($"{where} renames a parameter with [HarmonyArgument], which PatchTargetTests does not check yet.");
                    if (name == "__state" || name == "__exception" || name == "__runOriginal" || name == "__originalMethod" || name == "__args") continue;
                    if (name == "__instance")
                    {
                        if (original.IsStatic || !type2.IsAssignableFrom(original.DeclaringType))
                            throw new Exception($"{where}: __instance {type2.Name} does not match {target}.");
                    }
                    else if (name == "__result")
                    {
                        var returns = (original as MethodInfo)?.ReturnType;
                        if (returns == null || returns == typeof(void) || !type2.IsAssignableFrom(returns))
                            throw new Exception($"{where}: __result {type2.Name} does not match what {target} returns.");
                    }
                    else if (name.StartsWith("___", StringComparison.Ordinal))
                    {
                        // Harmony looks injected fields up by name on the target's type and its base types.
                        var field = AccessTools.Field(original.DeclaringType, name.Substring(3));
                        if (field == null) throw new Exception($"{where}: field {name.Substring(3)} not found on {original.DeclaringType!.FullName}.");
                        if (parameter.ParameterType.IsByRef ? type2 != field.FieldType : !type2.IsAssignableFrom(field.FieldType))
                            throw new Exception($"{where}: {name} is {type2.Name}, but the game's field is {field.FieldType.Name}.");
                        fields++;
                    }
                    else if (name.StartsWith("__", StringComparison.Ordinal))
                        throw new Exception($"{where} uses {name}, which PatchTargetTests does not check yet.");
                    else
                    {
                        var match = originalParameters.SingleOrDefault(p => p.Name == name)
                            ?? throw new Exception($"{where}: parameter \"{name}\" not found in {target}.");
                        var game = match.ParameterType.IsByRef ? match.ParameterType.GetElementType()! : match.ParameterType;
                        if (parameter.ParameterType.IsByRef ? type2 != game : !type2.IsAssignableFrom(game))
                            throw new Exception($"{where}: parameter \"{name}\" is {type2.Name}, but {target} passes {game.Name}.");
                        parameters++;
                    }
                }
            }
        }
        Console.WriteLine($"PASS: {targets} Harmony patch targets in {classes} patch classes resolve against installed game assemblies, with {fields} injected fields and {parameters} named parameters matching.");
        return resolved;
    }

    // LateGamePerformance runs two of these patches on worker threads and trusts them by (target "Type.Method",
    // Harmony id, patch "Namespace.Type.Method"): DistrictCounts.ReviewedPatches and SaveGuard.ReviewedPatches (read at
    // LateGamePerformance 0.4.26). A rename makes it stand down to the main thread, and so does any other patch on
    // AllowedAmount, a Save, or the Inventory methods its counting workers call.
    private static void LateGamePerformanceContract(Assembly mod, List<(MethodBase Target, MethodInfo Patch)> patches)
    {
        const string Id = "kyler.mixedstorage";
        var reviewed = new[] { ("SingleGoodAllower.AllowedAmount", "MixedStorage.LimitPatch.Prefix"), ("SingleGoodAllower.Save", "MixedStorage.SavePatch.Postfix") };
        var inventoryWorkerMethods = new[] { "GetCapacity", "LimitedAmount", "Gives", "AmountInStock", "get_Stock", "get_PublicInput" };
        string id = HarmonyId(mod);
        if (id != Id) throw new Exception($"ModStarter patches as Harmony id \"{id}\", but LateGamePerformance trusts MixedStorage's patches as \"{Id}\".");
        var found = patches.Select(p => (Target: p.Target.DeclaringType!.Name + "." + p.Target.Name, Patch: p.Patch.DeclaringType!.FullName + "." + p.Patch.Name, Method: p.Target)).ToList();
        foreach (var entry in reviewed)
            if (!found.Any(p => (p.Target, p.Patch) == entry))
                throw new Exception($"LateGamePerformance trusts {entry.Item2} on {entry.Item1}, which the mod no longer has; update LateGamePerformance's ReviewedPatches together with the rename.");
        foreach (var p in found)
        {
            bool watched = p.Method.Name == "AllowedAmount" || p.Method.Name == "Save"
                || p.Method.DeclaringType!.FullName == "Timberborn.InventorySystem.Inventory" && inventoryWorkerMethods.Contains(p.Method.Name);
            if (watched && !reviewed.Contains((p.Target, p.Patch)))
                throw new Exception($"{p.Patch} patches {p.Target}, which LateGamePerformance runs on worker threads only while every patch on it was reviewed; it would fall back to the main thread.");
        }
        Console.WriteLine($"PASS: LateGamePerformance's reviewed patches exist under Harmony id {Id} (LimitPatch.Prefix, SavePatch.Postfix), and no other patch is on the methods its workers call.");
    }

    // The id ModStarter.StartMod passes to new Harmony(...), read from its IL: ldstr "<id>" directly followed by newobj.
    private static string HarmonyId(Assembly mod)
    {
        var start = mod.GetType("MixedStorage.ModStarter", true)!.GetMethod("StartMod")!;
        var il = start.GetMethodBody()!.GetILAsByteArray()!;
        var ids = new List<string>();
        for (int i = 0; i + 10 <= il.Length; i++)
        {
            if (il[i] != 0x72 || il[i + 5] != 0x73 || il[i + 4] != 0x70) continue;
            MethodBase constructor;
            try { constructor = start.Module.ResolveMethod(BitConverter.ToInt32(il, i + 6)); }
            catch (ArgumentException) { continue; }
            if (constructor?.DeclaringType?.FullName == "HarmonyLib.Harmony") ids.Add(start.Module.ResolveString(BitConverter.ToInt32(il, i + 1)));
        }
        if (ids.Count != 1) throw new Exception($"ModStarter.StartMod creates {ids.Count} Harmony instances with a literal id; expected one. Update this check.");
        return ids[0];
    }

    private static void ReflectedMembers(Type[] types, string managed)
    {
        // Every reflection handle the mod keeps in a static field (MixedContentsRenderer, StorageState) is resolved
        // when its class is first used; a missing member is null and an ambiguous one fails the whole class.
        int handles = 0;
        foreach (var field in types.SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                     .Where(f => typeof(MemberInfo).IsAssignableFrom(f.FieldType)).OrderBy(f => f.DeclaringType!.FullName + "." + f.Name, StringComparer.Ordinal))
        {
            object value;
            try { value = field.GetValue(null); }
            catch (TypeInitializationException ex) { throw new Exception($"{field.DeclaringType!.FullName} cannot start: {ex.InnerException?.Message}", ex); }
            if (value == null) throw new Exception($"Reflected member not found in the game: {field.DeclaringType!.FullName}.{field.Name}.");
            handles++;
        }
        if (handles < 6) throw new Exception($"Only {handles} static reflection handles found; expected MixedContentsRenderer's five and StorageState.NotifyGood.");

        // MixedContentsRenderer also reads these from whichever native visualizer draws the building (a column in
        // warehouses, a pile in piles). The names mirror MixedContentsRenderer.Schedule and NativeCells.
        Type Game(string name) => Assembly.LoadFrom(Path.Combine(managed, "Timberborn.StockpileVisualization.dll")).GetType("Timberborn.StockpileVisualization." + name, true)!;
        int members = 0;
        FieldInfo Field(Type type, string name, string fieldType)
        {
            var field = AccessTools.Field(type, name);
            if (field == null || field.FieldType.FullName != fieldType) throw new Exception($"Reflected member not found in the game: {type.FullName}.{name} ({fieldType}).");
            members++;
            return field;
        }
        foreach (var visualizer in new[] { Game("StockpileGoodColumnVisualizer"), Game("StockpileGoodPileVisualizer") })
        {
            var visualization = Field(visualizer, "_goodVisualization", "Timberborn.StockpileVisualization.GoodVisualization").FieldType;
            Field(visualization, "_visualization", "UnityEngine.GameObject");
            Field(visualization, "_entityMaterials", "Timberborn.Rendering.EntityMaterials");
            if (AccessTools.Property(visualizer, "CurrentVisualization")?.PropertyType.FullName != "Timberborn.StockpileVisualization.GoodVisualizationSpec")
                throw new Exception($"Reflected member not found in the game: {visualizer.FullName}.CurrentVisualization.");
            members++;
        }
        Console.WriteLine($"PASS: {handles} static reflection handles and {members} native visualizer members found in installed game assemblies.");
    }

    // Without the multiplayer bridge, Apply only changes allocations locally when BeaverBuddies reports no
    // connection through its public EventIO.IsNull, which the mod reads by name. This process has no connection.
    private static void ConnectionState(Assembly mod, string beaverBuddiesPath)
    {
        if (beaverBuddiesPath == null)
        {
            Console.WriteLine("SKIPPED: BeaverBuddies EventIO.IsNull; pass BeaverBuddies.dll as the third argument (build.ps1 does).");
            return;
        }
        var beaverBuddies = Assembly.LoadFrom(beaverBuddiesPath);
        var isSinglePlayer = mod.GetType("MixedStorage.AllocationCommands", true)!.GetMethod("IsSinglePlayer", BindingFlags.NonPublic | BindingFlags.Static)!;
        if (!(bool)isSinglePlayer.Invoke(null, new object[] { beaverBuddies })!)
            throw new Exception("The mod cannot read BeaverBuddies' connection state (BeaverBuddies.IO.EventIO.IsNull), so single-player Apply would be refused.");
        Console.WriteLine($"PASS: BeaverBuddies {beaverBuddies.GetName().Version} reports no connection through EventIO.IsNull, as the mod reads it.");
    }

    // StorageState only attaches to these blueprints; each must exist and hold goods of a type the editor handles.
    private static void Templates(Assembly mod, string blueprintsZip)
    {
        var names = (IEnumerable<string>)mod.GetType("MixedStorage.StorageState", true)!.GetField("SupportedTemplates", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        using var zip = ZipFile.OpenRead(blueprintsZip);
        int count = 0;
        foreach (var name in names.OrderBy(n => n, StringComparer.Ordinal))
        {
            var entries = zip.Entries.Where(e => e.Name == name + ".blueprint.json").ToList();
            if (entries.Count != 1) throw new Exception($"Blueprints.zip has {entries.Count} {name}.blueprint.json entries; expected one.");
            using var stream = entries[0].Open();
            using var json = JsonDocument.Parse(stream);
            var root = json.RootElement;
            if (!root.TryGetProperty("TemplateSpec", out var template) || template.GetProperty("TemplateName").GetString() != name)
                throw new Exception($"{entries[0].FullName} is not the {name} template.");
            string goods = root.TryGetProperty("StockpileSpec", out var stockpile) ? stockpile.GetProperty("WhitelistedGoodType").GetString() : null;
            if (goods != "Box" && goods != "Pileable") throw new Exception($"{name} stores {goods ?? "no goods"}, not Box or Pileable goods.");
            count++;
        }
        if (count != 11) throw new Exception($"Expected 11 supported templates, found {count}; update this check and DEVELOPMENT.md.");
        Console.WriteLine($"PASS: all {count} supported template names are blueprints in the game's Blueprints.zip, each storing Box or Pileable goods.");
    }
}

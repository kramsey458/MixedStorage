using System.Reflection;
using System.Reflection.Emit;

// BeaverBuddies replays the game's Duplicate settings tool on every player (DuplicationEvent), so whatever the copy
// runs may read only simulation state. This walks the IL of the patches that run inside the copy, and of every mod
// method they call, and fails on per-player state: the panel's message and pending fields, the panel and renderer,
// submitting commands, clocks, input and random numbers. The renderer's own postfixes also run during the copy; they
// only draw, and nothing the simulation reads depends on them.
internal static class ReplayChecks
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Value);
    private static readonly string[] PerPlayerTypes = { "UnityEngine.Time", "UnityEngine.Random", "UnityEngine.Input", "UnityEngine.Camera",
        "UnityEngine.Screen", "System.Random", "System.Diagnostics.Stopwatch", "MixedStorage.StorageView", "MixedStorage.MixedContentsRenderer",
        "MixedStorage.MixedContentsRefreshQueue" };
    private static readonly string[] PerPlayerMembers = { "MixedStorage.StorageState.Pending", "MixedStorage.StorageState.LastMessage",
        "MixedStorage.StorageState.LastSuccess", "MixedStorage.StorageState.MessageRevision", "MixedStorage.AllocationCommands.Report",
        "MixedStorage.AllocationCommands.Submit", "MixedStorage.AllocationCommands.MultiplayerSubmit", "System.DateTime.get_Now",
        "System.DateTime.get_UtcNow", "System.Environment.get_TickCount", "System.Environment.get_TickCount64" };

    public static void Run(Assembly main)
    {
        Type Mod(string name) => main.GetType("MixedStorage." + name, true)!;
        MethodBase Method(string type, string name) => Mod(type).GetMethod(name, Any)!;
        // DuplicateFrom's own patch, and the patches the copy reaches through the base game's Allow, limits and events.
        var roots = new[] { Method("DuplicatePatch", "Prefix"), Method("DuplicatePatch", "Postfix"), Method("AllowPatch", "Prefix"),
            Method("DisallowPatch", "Prefix"), Method("LimitPatch", "Prefix"), Method("VisualizerPatch", "Prefix") };
        var (visited, problems) = Walk(main, roots);
        if (problems.Count > 0) throw new Exception("The replayed Duplicate settings path reads per-player state:\n" + string.Join("\n", problems));
        foreach (var expected in new[] { "AllocationPlan.PlanCopy", "AllocationPlan.Steps", "AllocationPlan.CanApply", "AllocationPlan.CanLeave", "StorageState.Deactivate",
                     "StorageState.TryApply", "StorageState.Publish", "StorageState.MixedStorage.IStorageContents.Incoming" })
            if (!visited.Contains(expected)) throw new Exception("The replay scan never reached " + expected + ".");
        // Controls: the scan finds per-player state where the mod does use it.
        if (!Walk(main, new[] { Method("AllocationCommands", "Report") }).problems.Any(p => p.EndsWith("StorageState.Pending", StringComparison.Ordinal)))
            throw new Exception("The replay scan missed the panel state that AllocationCommands.Report sets.");
        if (!Walk(main, new[] { Method("MixedContentsRefreshQueue", "LateUpdate") }).problems.Any(p => p.Contains("UnityEngine.Time.")))
            throw new Exception("The replay scan missed the clock that MixedContentsRefreshQueue reads.");
        Console.WriteLine($"PASS: the replayed Duplicate settings path ({visited.Count} mod methods) reads no panel, renderer, command, clock or random state; control scans flag both.");
    }

    private static (HashSet<string> visited, List<string> problems) Walk(Assembly main, IEnumerable<MethodBase> roots)
    {
        var visited = new HashSet<string>();
        var problems = new List<string>();
        var seen = new HashSet<MethodBase>();
        var queue = new Queue<MethodBase>(roots);
        while (queue.Count > 0)
        {
            var method = queue.Dequeue();
            if (!seen.Add(method)) continue;
            visited.Add(method.DeclaringType!.Name + "." + method.Name);
            foreach (var member in References(method))
            {
                var type = member as Type ?? member.DeclaringType;
                string name = type?.FullName + (member is Type ? "" : "." + member.Name);
                if (PerPlayerMembers.Contains(name) || PerPlayerTypes.Any(t => type?.FullName == t || type?.FullName?.StartsWith(t + "+", StringComparison.Ordinal) == true))
                    problems.Add(method.DeclaringType.Name + "." + method.Name + " uses " + name);
                if (member is not MethodBase callee || callee.Module != main.ManifestModule) continue;
                foreach (var target in Implementations(main, callee)) queue.Enqueue(target);
                // Creating an iterator runs its body later, through IEnumerator.MoveNext.
                if (callee is ConstructorInfo && typeof(System.Collections.IEnumerator).IsAssignableFrom(callee.DeclaringType))
                    queue.Enqueue(callee.DeclaringType!.GetMethod("MoveNext", Any)!);
            }
        }
        return (visited, problems);
    }

    // A call through one of the mod's interfaces runs the mod's implementations of it.
    private static IEnumerable<MethodBase> Implementations(Assembly main, MethodBase method)
    {
        var face = method.DeclaringType;
        if (face == null || !face.IsInterface)
        {
            if (method.GetMethodBody() != null) yield return method;
            yield break;
        }
        foreach (var type in main.GetTypes().Where(t => !t.IsInterface && face.IsAssignableFrom(t)))
        {
            var map = type.GetInterfaceMap(face);
            int index = Array.IndexOf(map.InterfaceMethods, (MethodInfo)method);
            if (index >= 0) yield return map.TargetMethods[index];
        }
    }

    // The fields, methods and types an IL body refers to.
    private static IEnumerable<MemberInfo> References(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il == null) yield break;
        var typeArguments = method.DeclaringType!.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        for (int i = 0; i < il.Length;)
        {
            short value = il[i++];
            if (value == 0xFE) value = unchecked((short)(0xFE00 | il[i++]));
            if (!Codes.TryGetValue(value, out var code)) throw new Exception($"Unknown IL opcode 0x{value:X} in {method.DeclaringType.Name}.{method.Name}.");
            int size = code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 4
            };
            if (code.OperandType is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineTok or OperandType.InlineType)
                yield return method.Module.ResolveMember(BitConverter.ToInt32(il, i), typeArguments, methodArguments)!;
            i += size;
        }
    }
}

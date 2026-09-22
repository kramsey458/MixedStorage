#nullable enable
using System.Linq.Expressions;
using System.Reflection;

// Leaving mixed mode and reloading a building run the built mod's StorageState against the game's own
// SingleGoodAllower and Inventory, created without Unity. The allower's DisallowedGoodsChanged event records every
// good the mod announces, which is what the game's inventories and InventoryRegistry react to.
internal static class AnnouncementTests
{
    public static void Run(Assembly mod, Func<string, string, Type> gameType)
    {
        const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var stateType = mod.GetType("MixedStorage.StorageState", true)!;
        var allowerType = gameType("Timberborn.InventorySystem", "Timberborn.InventorySystem.SingleGoodAllower");
        var inventoryType = gameType("Timberborn.InventorySystem", "Timberborn.InventorySystem.Inventory");
        var registryType = gameType("Timberborn.Goods", "Timberborn.Goods.StorableGoodRegistry");
        var storableType = gameType("Timberborn.Goods", "Timberborn.Goods.StorableGood");
        var amountType = gameType("Timberborn.Goods", "Timberborn.Goods.StorableGoodAmount");
        var entityLoaderType = gameType("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.IEntityLoader");
        var objectLoaderType = gameType("Timberborn.Persistence", "Timberborn.Persistence.IObjectLoader");
        var serialize = mod.GetType("MixedStorage.AllocationPlan", true)!.GetMethod("Serialize")!;
        var shares = stateType.GetProperty("Shares")!;
        var internalChange = stateType.GetProperty("InternalChange")!;

        // A building that accepts Log and Plank. "Gone" stands for a good a saved allocation still names after the
        // mod that added it was removed: the game has no such good.
        var announced = new List<(string Good, bool Internal)>();
        object state = null!;
        object Storage()
        {
            var registry = Activator.CreateInstance(registryType)!;
            var goods = Array.CreateInstance(amountType, 2);
            for (int i = 0; i < 2; i++)
            {
                var good = storableType.GetMethod("CreateGiveableAndTakeable")!.Invoke(null, new object[] { i == 0 ? "Log" : "Plank" });
                goods.SetValue(Activator.CreateInstance(amountType, good, 30), i);
            }
            registryType.GetMethod("Add")!.Invoke(registry, new object[] { goods });
            var inventory = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(inventoryType);
            inventoryType.GetField("_allowedGoods", Instance)!.SetValue(inventory, registry);
            var allower = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(allowerType);
            var changed = allowerType.GetEvent("DisallowedGoodsChanged")!;
            var sender = Expression.Parameter(typeof(object));
            var args = Expression.Parameter(changed.EventHandlerType!.GetMethod("Invoke")!.GetParameters()[1].ParameterType);
            Action<string> record = good => announced.Add((good, (bool)internalChange.GetValue(state)!));
            var handler = Expression.Lambda(changed.EventHandlerType, Expression.Invoke(Expression.Constant(record), Expression.Property(args, "GoodId")), sender, args);
            changed.AddEventHandler(allower, handler.Compile());
            state = stateType.GetConstructors(Instance).Single().Invoke(new[] { allower, inventory });
            announced.Clear();
            return state;
        }
        Dictionary<string, int> Shares(params (string Good, int Share)[] entries) =>
            entries.ToDictionary(x => x.Good, x => x.Share, StringComparer.Ordinal);
        object Loader(Dictionary<string, int>? saved)
        {
            var loader = Proxy<EntityLoaderProxy>(entityLoaderType);
            if (saved != null)
            {
                var component = Proxy<ObjectLoaderProxy>(objectLoaderType);
                component.Shares = (string)serialize.Invoke(null, new object[] { saved })!;
                loader.Component = component;
            }
            return loader;
        }
        void Call(string method, string failure, params object[] arguments)
        {
            try { stateType.GetMethod(method)!.Invoke(state, arguments); }
            catch (TargetInvocationException ex) { throw new Exception(failure + " " + ex.InnerException, ex); }
        }
        void Expect(string check, params string[] goods)
        {
            if (!announced.Select(x => x.Good).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(goods))
                throw new Exception($"{check}; announced [{string.Join(", ", announced.Select(x => x.Good))}], expected [{string.Join(", ", goods)}].");
            if (announced.Any(x => !x.Internal) || (bool)internalChange.GetValue(state)!)
                throw new Exception(check + ", but the announcements did not all run as the mod's own change, so the visuals could follow the wrong good.");
        }

        // Copy Settings leaving mixed mode.
        Storage();
        shares.SetValue(state, Shares(("Log", 5000), ("Gone", 5000)));
        Call("Deactivate", "Leaving mixed mode with a good the game no longer knows threw:");
        if (shares.GetValue(state) != null) throw new Exception("Deactivate left the building mixed.");
        Expect("Leaving mixed mode announces the allocated goods the building accepts, and no good the game does not know", "Log");

        // The map editor's undo reloading a mixed building without an allocation.
        Storage();
        shares.SetValue(state, Shares(("Log", 5000), ("Gone", 5000)));
        Call("Load", "Reloading a mixed building without an allocation threw:", Loader(null));
        if (shares.GetValue(state) != null) throw new Exception("A building reloaded without an allocation stayed mixed.");
        Expect("A mixed building reloaded without an allocation announces its formerly allocated goods, as Deactivate does", "Log");

        // Reloading a mixed building with another allocation.
        Storage();
        shares.SetValue(state, Shares(("Log", 10000)));
        Call("Load", "Reloading a mixed building with another allocation threw:", Loader(Shares(("Log", 5000), ("Plank", 5000))));
        Expect("A mixed building reloaded with another allocation announces the goods of both allocations", "Log", "Plank");

        // Loading a save: a building loaded for the first time is not in the world yet, so nothing is announced.
        Storage();
        Call("Load", "Loading a saved allocation threw:", Loader(Shares(("Log", 5000), ("Plank", 5000))));
        if (shares.GetValue(state) == null) throw new Exception("A saved allocation was not loaded.");
        Expect("Loading a save announces nothing");
        Storage();
        Call("Load", "Loading a building without an allocation threw:", Loader(null));
        Expect("Loading a building without an allocation announces nothing");
        Console.WriteLine("PASS: leaving mixed mode and reloading a mixed building announce only goods the building accepts, never one the game no longer knows; loading a save announces nothing.");
    }

    private static T Proxy<T>(Type contract) where T : DispatchProxy =>
        (T)typeof(DispatchProxy).GetMethods().Single(m => m.Name == "Create" && m.IsGenericMethodDefinition)
            .MakeGenericMethod(contract, typeof(T)).Invoke(null, null)!;
}

// IEntityLoader: the building's saved components; only TryGetComponent(key, out loader) is used.
public class EntityLoaderProxy : DispatchProxy
{
    public object? Component;

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "TryGetComponent" && args!.Length == 2) { args[1] = Component; return Component != null; }
        throw new NotSupportedException("StorageState.Load called IEntityLoader." + method.Name);
    }
}

// IObjectLoader: the saved MixedStorage.Allocation component, holding only its Shares string.
public class ObjectLoaderProxy : DispatchProxy
{
    public string Shares = "";

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "Has") return true;
        if (method.Name == "Get" && method.ReturnType == typeof(string) && args!.Length == 1) return Shares;
        throw new NotSupportedException("StorageState.Load called IObjectLoader." + method.Name);
    }
}

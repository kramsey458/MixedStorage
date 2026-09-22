using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MixedStorage
{
    public static class OptionalMultiplayer
    {
        public const string ResourceName = "MixedStorage.OptionalMultiplayer";
        private static bool _started;
        private static bool _resolverInstalled;

        // Set when BeaverBuddies is installed but the bridge could not start. The game still starts and
        // single-player allocations still work; multiplayer Apply is refused with this message.
        public static string UnavailableReason { get; private set; }
        public static Exception Failure { get; private set; }

        public static bool Initialize()
        {
            if (_started) return true;
            if (Failure != null) return false;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (assemblies.Any(a => a.GetName().Name == "MixedStorage.BeaverBuddies"))
                throw new InvalidOperationException("MixedStorage now includes multiplayer support. Remove or disable the old MixedStorage-BeaverBuddies addon and restart Timberborn.");
            if (!assemblies.Any(a => a.GetName().Name == "BeaverBuddies")) return false;
            if (!_resolverInstalled)
            {
                // The game loads assemblies from bytes, without filesystem probing paths.
                AppDomain.CurrentDomain.AssemblyResolve += ResolveLoadedIntegration;
                _resolverInstalled = true;
            }
            using (var stream = typeof(OptionalMultiplayer).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null) throw new InvalidOperationException("MixedStorage's bundled multiplayer integration is missing. Reinstall the complete release.");
                using (var bytes = new MemoryStream())
                {
                    stream.CopyTo(bytes);
                    try
                    {
                        var bridge = Assembly.Load(bytes.ToArray());
                        var starter = bridge.GetType("MixedStorage.Multiplayer.MultiplayerStarter", true);
                        starter.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
                    }
                    catch (Exception ex)
                    {
                        // Throwing here would stop every later mod and the game from starting. Without the
                        // bridge, AllocationCommands refuses to apply in multiplayer, so nobody desyncs.
                        Failure = ex;
                        UnavailableReason = "MixedStorage's multiplayer support could not start with the installed BeaverBuddies (" +
                            ex.GetBaseException().Message + "). Install the BeaverBuddies version named in MixedStorage's README on every computer.";
                        return false;
                    }
                    _started = true;
                    return true;
                }
            }
        }

        private static Assembly ResolveLoadedIntegration(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            if (name != "MixedStorage" && name != "MixedStorage.MultiplayerBridge" && name != "BeaverBuddies") return null;
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
        }
    }
}

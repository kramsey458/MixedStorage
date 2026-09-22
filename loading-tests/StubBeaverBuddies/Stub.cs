using System;
using Timberborn.BaseComponentSystem;

// Everything the bridge uses except ReplayEvent.DoPrefix, as if BeaverBuddies had renamed its event gate.
namespace BeaverBuddies
{
    public class ReplayService
    {
        public static bool IsReplayingEvents => false;
    }
}

namespace BeaverBuddies.IO
{
    public interface EventIO
    {
        static bool IsNull => !StubConnection.Connected;
    }

    // Lets the loading test switch between single-player and a multiplayer connection.
    public static class StubConnection
    {
        public static bool Connected;
    }
}

namespace BeaverBuddies.Events
{
    public interface IReplayContext
    {
    }

    public abstract class ReplayEvent
    {
        public static readonly string LocalPlayerID = Guid.NewGuid().ToString();

        public abstract void Replay(IReplayContext context);

        public virtual string ToActionString() => GetType().Name;

        public static ReplayService GetReplayServiceIfReady() => null;

        public static string GetEntityID(BaseComponent component) => null;

        public static T GetComponent<T>(IReplayContext context, string entityID) => default;

        public static bool RecordAndCheck(Func<ReplayEvent> getEvent) => true;
    }
}

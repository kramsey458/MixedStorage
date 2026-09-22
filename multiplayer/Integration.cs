using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BeaverBuddies;
using BeaverBuddies.Events;
using BeaverBuddies.IO;
using Timberborn.BaseComponentSystem;
using Timberborn.InventorySystem;

namespace MixedStorage.Multiplayer
{
    public static class MultiplayerStarter
    {
        public static void Initialize()
        {
            CheckCompatibility();
            AllocationCommands.MultiplayerSubmit = Submit;
        }

        // The bridge is compiled against one BeaverBuddies build, and a method's calls into it are only
        // bound when that method first runs. Look up everything Submit and the event use now, so another
        // BeaverBuddies build fails at startup with a clear message instead of at the first Apply.
        private static void CheckCompatibility()
        {
            const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;
            var missing = new List<string>();
            void Require(bool found, string name) { if (!found) missing.Add(name); }
            Type Returns(MethodInfo method) => method?.ReturnType;
            var replayEvent = typeof(ReplayEvent);
            Require(typeof(EventIO).GetProperty("IsNull", Static)?.PropertyType == typeof(bool), "EventIO.IsNull");
            Require(typeof(ReplayService).GetProperty("IsReplayingEvents", Static)?.PropertyType == typeof(bool), "ReplayService.IsReplayingEvents");
            Require(Returns(replayEvent.GetMethod("GetReplayServiceIfReady", Static, null, Type.EmptyTypes, null)) == typeof(ReplayService), "ReplayEvent.GetReplayServiceIfReady");
            Require(Returns(replayEvent.GetMethod("GetEntityID", Static, null, new[] { typeof(BaseComponent) }, null)) == typeof(string), "ReplayEvent.GetEntityID");
            Require(replayEvent.GetField("LocalPlayerID", Static)?.FieldType == typeof(string), "ReplayEvent.LocalPlayerID");
            Require(Returns(replayEvent.GetMethod("DoPrefix", Static, null, new[] { typeof(Func<ReplayEvent>) }, null)) == typeof(bool), "ReplayEvent.DoPrefix");
            Require(replayEvent.GetMethods(Static).Any(m => m.Name == "GetComponent" && m.IsGenericMethodDefinition &&
                m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(IReplayContext), typeof(string) })), "ReplayEvent.GetComponent<T>");
            if (missing.Count > 0) throw new MissingMemberException("BeaverBuddies has no " + string.Join(", ", missing));
            // Creating the event loads its type, which checks it still implements every abstract member.
            GC.KeepAlive(new StorageAllocationEvent());
        }

        private static SubmissionResult Submit(SingleGoodAllower allower, string payload)
        {
            if (EventIO.IsNull)
            {
                bool applied = AllocationCommands.ApplyReplay(allower, payload, out string message);
                AllocationCommands.Report(allower, applied, message);
                return applied ? SubmissionResult.Applied : SubmissionResult.Rejected;
            }
            var replay = ReplayEvent.GetReplayServiceIfReady();
            if (replay == null || ReplayService.IsReplayingEvents)
            {
                AllocationCommands.Report(allower, false, "Multiplayer is not ready. Wait until synchronization completes.");
                return SubmissionResult.Rejected;
            }
            var command = new StorageAllocationEvent
            {
                entityID = ReplayEvent.GetEntityID(allower),
                allocation = payload,
                senderID = ReplayEvent.LocalPlayerID
            };
            if (command.entityID == null)
            {
                AllocationCommands.Report(allower, false, "Place the storage building before applying allocations.");
                return SubmissionResult.Rejected;
            }
            // Use BB's event gate: host/client/replay modes each have different queue semantics.
            if (!ReplayEvent.DoPrefix(() => command)) return SubmissionResult.Queued;
            bool success = AllocationCommands.ApplyReplay(allower, payload, out string result);
            AllocationCommands.Report(allower, success, result);
            return success ? SubmissionResult.Applied : SubmissionResult.Rejected;
        }
    }

    // Replay type metadata uses the bundled bridge assembly on every player.
    public sealed class StorageAllocationEvent : ReplayEvent
    {
        public string entityID;
        public string allocation;
        public string senderID;

        public override void Replay(IReplayContext context)
        {
            var allower = GetComponent<SingleGoodAllower>(context, entityID);
            if (allower == null) return;
            bool success = AllocationCommands.ApplyReplay(allower, allocation, out string message);
            if (senderID == LocalPlayerID) AllocationCommands.Report(allower, success, message);
        }

        public override string ToActionString() => "Setting mixed storage allocation for " + entityID;
    }
}

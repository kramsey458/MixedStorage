using BeaverBuddies;
using BeaverBuddies.Events;
using BeaverBuddies.IO;
using Timberborn.InventorySystem;
using Timberborn.ModManagerScene;
using UnityEngine;

namespace MixedWarehouses.Multiplayer
{
    public sealed class MultiplayerStarter : IModStarter
    {
        public void StartMod(IModEnvironment environment)
        {
            AllocationCommands.MultiplayerSubmit = Submit;
            Debug.Log("[MixedWarehouses] BeaverBuddies synchronized allocation events enabled.");
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
            var command = new WarehouseAllocationEvent
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

    // Newtonsoft type metadata includes this assembly; all players must install this same addon.
    public sealed class WarehouseAllocationEvent : ReplayEvent
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

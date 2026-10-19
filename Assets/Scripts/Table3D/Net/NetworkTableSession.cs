using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BiteOrBetray.Core;
using Unity.Netcode;
using UnityEngine;

namespace BiteOrBetray.Table3D.Net
{
    /// <summary>
    /// Attach with NetworkObject and spawn with server ownership. The lobby fills seats, then calls StartTable on the host.
    /// The lobby owns Transport/Relay and the room lifecycle; this component sends actions, snapshots, poses, and table visuals.
    /// </summary>
    [DefaultExecutionOrder(450), RequireComponent(typeof(NetworkObject))]
    public sealed partial class NetworkTableSession : NetworkBehaviour
    {
        public CandyTableDirector director;
        public TableAuthority Authority { get; private set; }
        public TableSnapshot Latest { get; private set; }
        public IReadOnlyList<TableSeat> Seats { get; private set; }
        public bool InputOpen { get; private set; }
        public event Action<TableSnapshot> SnapshotReceived;
        public event Action<string> ActionRejected;
        public event Action TableStopped;

        TableAction pending;
        ulong pendingSender;
        NetworkManager hostManager;
        long lastRevision;
        float nextSnapshot, secondsLeft;

        public override void OnNetworkSpawn()
        {
            if (director == null) director = FindFirstObjectByType<CandyTableDirector>();
            if (director == null) throw new InvalidOperationException("CandyTableDirector not found.");
            director.BindNetwork(this);
            if (!IsServer) return;
            hostManager = NetworkManager;
            hostManager.OnClientDisconnectCallback += ReplaceDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (hostManager != null) hostManager.OnClientDisconnectCallback -= ReplaceDisconnected;
            hostManager = null;
            ResetVisualStream();
            Authority = null;
            Latest = null;
            InputOpen = false;
            pending = null;
            if (director != null) director.UnbindNetwork(this);
        }

        /// <summary>Called by the host lobby once participants are ready; empty seats may be filled by bots.</summary>
        public void StartTable(IReadOnlyList<TableSeat> seats)
        {
            if (!IsSpawned || !IsHost) throw new InvalidOperationException("Only an active host can start the table.");
            if (seats == null || seats.Count < 2 || seats.Count > 6) throw new ArgumentException("Needs 2-6 seats.");
            if (seats.Any(s => s == null || (s.Controller == SeatController.Online &&
                    (s.ClientId == NetworkManager.LocalClientId || !NetworkManager.ConnectedClients.ContainsKey(s.ClientId)))))
                throw new ArgumentException("Online seats must belong to connected clients.");
            if (seats.Count(s => s.Controller == SeatController.Local) > 1)
                throw new ArgumentException("This table supports one local host seat.");
            Seats = seats.Select(s => new TableSeat(s.Name, s.Controller, s.ClientId, s.Difficulty)).ToArray();
            director.StartNetworkTable(this);
        }

        [ContextMenu("Start table with connected clients (local test)")]
        public void StartConnectedTable()
        {
            if (!IsSpawned || !IsHost) throw new InvalidOperationException("Start NetworkManager as host first.");
            var seats = new List<TableSeat> { new TableSeat("Host", SeatController.Local) };
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
                if (id != NetworkManager.LocalClientId) seats.Add(new TableSeat("Online " + id, SeatController.Online, id));
            if (seats.Count == 1) throw new InvalidOperationException("Connect at least one client for the online test.");
            while (seats.Count < 6) seats.Add(new TableSeat("Bot " + seats.Count, SeatController.Bot));
            StartTable(seats);
        }

        /// <summary>True while a connected online client still controls the seat.</summary>
        public bool IsOnlineSeat(int config) => Authority != null && config >= 0 && config < Authority.Seats.Count
            && Authority.Seats[config].Controller == SeatController.Online;

        // Client disconnected (BOB-037): a bot plays their seat without stopping the game, including the next Play again.
        void ReplaceDisconnected(ulong clientId)
        {
            // A shutting-down host also receives this callback for each client; that table is already finished.
            if (!IsSpawned || !IsHost || NetworkManager.ShutdownInProgress || clientId == NetworkManager.LocalClientId) return;
            TableSeat.ReplaceWithBot(Seats, clientId);
            int config = Authority?.ReplaceWithBot(clientId) ?? -1;
            if (pending != null && pendingSender == clientId) pending = null;
            if (config < 0) return;
            poseSequences.Remove(config);
            poseTimes.Remove(config);
            if (director != null) director.ShowSeatTakenByBot(config);
            Publish(secondsLeft);
        }

        public GameEngine BeginGame(RuleConfig rules, int seed)
        {
            if (!IsHost || Seats == null) throw new InvalidOperationException("Host configuration not ready yet.");
            if (Authority != null) lastRevision = Authority.Revision;
            Authority = new TableAuthority(rules, seed, Seats, lastRevision);
            ResetVisualStream();
            lastRevision = Authority.Revision;
            pending = null;
            InputOpen = false;
            return Authority.Engine;
        }

        public void SetInput(bool open, float seconds)
        {
            if (!IsHost) return;
            InputOpen = open;
            secondsLeft = Mathf.Max(0, seconds);
            if (!open) pending = null;
        }

        public bool TryTakeRemote(out TableAction action, out ulong sender)
        {
            action = pending;
            sender = pendingSender;
            pending = null;
            if (action == null) return false;
            InputOpen = false;
            return true;
        }

        public ActionResult Execute(BotDecision decision, bool timeout = false) => IsSpawned && IsHost && Authority != null
            ? Authority.ExecuteHost(decision, timeout) : ActionResult.Fail(Strings.HostEngineInactive.Text);

        public ActionResult ExecuteRemote(ulong sender, TableAction action)
        {
            if (!IsSpawned || !IsHost || Authority == null) return ActionResult.Fail(Strings.HostEngineInactive.Text);
            var result = Authority.ExecuteRemote(sender, action);
            if (!result.Success) Reject(sender, result.Error);
            return result;
        }

        public void Submit(BotDecision decision, int seat)
        {
            if (!IsSpawned || IsServer || Latest == null || !Latest.InputOpen) return;
            SubmitActionRpc(TableWire.WriteAction(new TableAction { Revision = Latest.Revision, Seat = seat, Decision = decision }));
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void SubmitActionRpc(byte[] bytes, RpcParams rpcParams = default)
        {
            if (!IsHost || Authority == null) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            try
            {
                var action = TableWire.ReadAction(bytes);
                var valid = Authority.ValidateRemote(sender, action);
                if (!valid.Success) { Reject(sender, valid.Error); return; }
                if (!InputOpen || pending != null) { Reject(sender, Strings.WaitPreviousAction.Text); return; }
                pending = action;
                pendingSender = sender;
            }
            catch (IOException) { Reject(sender, Strings.InvalidActionPacket.Text); }
        }

        void Reject(ulong sender, string error) => RejectActionRpc(sender, error);

        // Cues follow host animation timing, without guessing candy contents from masked snapshots.
        public void BroadcastGameplaySound(GameplaySound sound)
        {
            if (IsSpawned && IsHost && Latest != null) GameplaySoundRpc(Latest.GameId, (int)sound);
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void GameplaySoundRpc(long game, int sound)
        {
            if (Latest == null || Latest.GameId != game || sound < 0 || sound > (int)GameplaySound.Waiter) return;
            if (director != null) director.ReceiveGameplaySound((GameplaySound)sound);
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void RejectActionRpc(ulong target, string error)
        {
            if (NetworkManager.LocalClientId == target) ActionRejected?.Invoke(error);
        }

        public void Publish(float seconds)
        {
            if (!IsSpawned || !IsHost || Authority == null) return;
            secondsLeft = Mathf.Max(0, seconds);
            lastRevision = Authority.Revision;
            Latest = TableSnapshot.Capture(Authority.Engine, Authority.Seats, lastRevision, secondsLeft, InputOpen, Authority.GameId);
            ReceiveSnapshotRpc(TableWire.WriteSnapshot(Latest));
            nextSnapshot = Time.unscaledTime + .2f;
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void ReceiveSnapshotRpc(byte[] bytes)
        {
            if (IsServer) return;
            try
            {
                var snapshot = TableWire.ReadSnapshot(bytes);
                if (Latest != null && snapshot.Revision < Latest.Revision) return;
                Latest = snapshot;
                SnapshotReceived?.Invoke(snapshot);
            }
            catch (IOException e) { Debug.LogWarning("[BoB] Snapshot rejected: " + e.Message, this); }
        }

        void Update()
        {
            // Periodic snapshots also serve newly joined clients without replaying actions or RNG.
            if (IsSpawned && IsHost && Authority != null && Time.unscaledTime >= nextSnapshot) Publish(secondsLeft);
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void StopTableRpc()
        {
            ResetVisualStream();
            Latest = null;
            TableStopped?.Invoke();
        }

        public void StopTable()
        {
            if (!IsHost) return;
            ResetVisualStream();
            if (Authority != null)
            {
                lastRevision = Authority.Revision;
                if (IsSpawned) StopTableRpc();
            }
            Authority = null;
            pending = null;
            InputOpen = false;
            Latest = null;
        }
    }
}

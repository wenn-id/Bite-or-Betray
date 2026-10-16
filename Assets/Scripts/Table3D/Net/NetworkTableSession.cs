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
    /// Pasang bersama NetworkObject dan spawn milik server. Lobby mengisi kursi lalu memanggil StartTable di host.
    /// Transport/Relay dan lifecycle room dimiliki lobby; komponen ini hanya mengirim aksi dan snapshot permainan.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkTableSession : NetworkBehaviour
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
        long lastRevision;
        float nextSnapshot, secondsLeft;

        public override void OnNetworkSpawn()
        {
            if (director == null) director = FindFirstObjectByType<CandyTableDirector>();
            if (director == null) throw new InvalidOperationException("CandyTableDirector tidak ditemukan.");
            director.BindNetwork(this);
        }

        public override void OnNetworkDespawn()
        {
            Authority = null;
            Latest = null;
            InputOpen = false;
            pending = null;
            if (director != null) director.UnbindNetwork(this);
        }

        /// <summary>Dipanggil lobby host setelah peserta siap; kursi kosong boleh diisi bot.</summary>
        public void StartTable(IReadOnlyList<TableSeat> seats)
        {
            if (!IsSpawned || !IsHost) throw new InvalidOperationException("Hanya host aktif boleh memulai meja.");
            if (seats == null || seats.Count < 2 || seats.Count > 6) throw new ArgumentException("Butuh 2-6 kursi.");
            if (seats.Any(s => s == null || (s.Controller == SeatController.Online &&
                    (s.ClientId == NetworkManager.LocalClientId || !NetworkManager.ConnectedClients.ContainsKey(s.ClientId)))))
                throw new ArgumentException("Kursi online harus milik klien yang tersambung.");
            if (seats.Count(s => s.Controller == SeatController.Local) > 1)
                throw new ArgumentException("Meja ini mendukung satu kursi lokal host.");
            Seats = seats.Select(s => new TableSeat(s.Name, s.Controller, s.ClientId, s.Difficulty)).ToArray();
            director.StartNetworkTable(this);
        }

        [ContextMenu("Mulai meja dengan klien tersambung (uji lokal)")]
        public void StartConnectedTable()
        {
            if (!IsSpawned || !IsHost) throw new InvalidOperationException("Mulai NetworkManager sebagai host dulu.");
            var seats = new List<TableSeat> { new TableSeat("Host", SeatController.Local) };
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
                if (id != NetworkManager.LocalClientId) seats.Add(new TableSeat("Online " + id, SeatController.Online, id));
            if (seats.Count == 1) throw new InvalidOperationException("Sambungkan minimal satu klien untuk uji online.");
            while (seats.Count < 6) seats.Add(new TableSeat("Bot " + seats.Count, SeatController.Bot));
            StartTable(seats);
        }

        public GameEngine BeginGame(RuleConfig rules, int seed)
        {
            if (!IsHost || Seats == null) throw new InvalidOperationException("Konfigurasi host belum siap.");
            if (Authority != null) lastRevision = Authority.Revision;
            Authority = new TableAuthority(rules, seed, Seats, lastRevision);
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
            ? Authority.ExecuteHost(decision, timeout) : ActionResult.Fail("Engine host belum aktif.");

        public ActionResult ExecuteRemote(ulong sender, TableAction action)
        {
            if (!IsSpawned || !IsHost || Authority == null) return ActionResult.Fail("Engine host belum aktif.");
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
                if (!InputOpen || pending != null) { Reject(sender, "Tunggu aksi sebelumnya selesai."); return; }
                pending = action;
                pendingSender = sender;
            }
            catch (IOException) { Reject(sender, "Paket aksi tidak valid."); }
        }

        void Reject(ulong sender, string error) => RejectActionRpc(sender, error);

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
            catch (IOException e) { Debug.LogWarning("[BoB] Snapshot ditolak: " + e.Message, this); }
        }

        void Update()
        {
            // Snapshot berkala juga melayani klien yang baru masuk tanpa memutar ulang aksi atau RNG.
            if (IsSpawned && IsHost && Authority != null && Time.unscaledTime >= nextSnapshot) Publish(secondsLeft);
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void StopTableRpc()
        {
            Latest = null;
            TableStopped?.Invoke();
        }

        public void StopTable()
        {
            if (!IsHost) return;
            if (Authority != null)
            {
                lastRevision = Authority.Revision;
                if (IsSpawned) StopTableRpc();
            }
            Authority = null;
            pending = null;
            InputOpen = false;
        }
    }
}

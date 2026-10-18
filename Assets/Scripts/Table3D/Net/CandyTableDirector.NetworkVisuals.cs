using System.Collections.Generic;
using System.IO;
using System.Linq;
using BiteOrBetray.Core;
using BiteOrBetray.Table3D.Net;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    public sealed partial class CandyTableDirector
    {
        sealed class NetworkVisual
        {
            public int id, kind, model, color;
            public Transform transform;
            public Pose previous, target;
            public Vector3 previousScale, scale;
        }

        readonly Dictionary<int, RemoteAvatarRepresentation> networkAvatars = new Dictionary<int, RemoteAvatarRepresentation>();
        readonly List<NetworkVisual> networkItems = new List<NetworkVisual>();
        readonly Dictionary<int, NetworkVisual> receivedVisuals = new Dictionary<int, NetworkVisual>();
        int nextNetworkItem, remoteHeldSeat = -1;
        float remoteHeldAt, visualsAt;
        Vector3 remoteCandyPosition;
        bool remoteHolding, hasVisuals;
        uint receivedVisualSequence;

        Transform NetworkSeat(int config) => config < 0 ? artRoot
            : seats[config] == human ? (human.avatar != null ? human.avatar.seat : human.anchor.parent) : seats[config].character.transform.parent;

        void ConfigureNetworkAvatars(IReadOnlyList<TableSeat> bindings)
        {
            RestoreNetworkAvatars();
            if (human?.avatar == null) return;
            human.avatar.Initialize();
            human.avatar.SetSpectator(false);
            // Salin rig lokal dalam keadaan nonaktif agar tracking lokal tidak pernah berjalan pada salinan remote.
            var staging = new GameObject("Avatar staging");
            staging.SetActive(false);
            for (int i = 0; i < bindings.Count; i++)
            {
                if (i == localConfigIndex || bindings[i].Controller == SeatController.Bot) continue;
                var seat = seats[i];
                if (seat == human) continue;
                var clone = Instantiate(human.avatar.gameObject, staging.transform);
                clone.name = "Online Avatar " + i;
                var tracking = clone.GetComponent<LocalAvatarTracking>();
                if (tracking != null) { tracking.enabled = false; Destroy(tracking); }
                var avatar = clone.GetComponent<SeatedAvatar>();
                avatar.seat = seat.character.transform.parent;
                ResetClonedFingers(human.avatar.left, avatar.left);
                ResetClonedFingers(human.avatar.right, avatar.right);
                foreach (var renderer in avatar.headRenderers)
                {
                    renderer.forceRenderingOff = false;
                    renderer.gameObject.layer = avatar.gameObject.layer;
                }
                var remote = clone.GetComponent<RemoteAvatarRepresentation>();
                if (remote == null) remote = clone.AddComponent<RemoteAvatarRepresentation>();
                remote.avatar = avatar;
                remote.enabled = true;
                clone.transform.SetParent(avatar.seat, false);
                clone.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                clone.SetActive(true);
                seat.character.gameObject.SetActive(false);
                seat.avatar = avatar;
                seat.avatarReaction = clone.GetComponent<AvatarReactionController>();
                networkAvatars.Add(i, remote);
            }
            Destroy(staging);
        }

        static void ResetClonedFingers(SeatedAvatar.Hand source, SeatedAvatar.Hand target)
        {
            // Salinan runtime tidak boleh menjadikan grip pemain lokal sebagai rotasi istirahat jari remote.
            for (int i = 0; i < target.fingers.Length; i++) target.fingers[i].localRotation = source.fingerRest[i];
        }

        void RestoreNetworkAvatars()
        {
            foreach (var pair in networkAvatars)
            {
                var remote = pair.Value;
                if (remote != null) { remote.gameObject.SetActive(false); Destroy(remote.gameObject); }
            }
            networkAvatars.Clear();
            foreach (var seat in allSeats)
            {
                if (seat == human) continue;
                seat.avatar = null;
                seat.avatarReaction = null;
                if (seat.character != null) seat.character.gameObject.SetActive(seats.Contains(seat));
            }
        }

        // Satu kursi kembali ke karakter bot asli; status lumpuh/KO dipasang ulang pada karakter itu. Salinan avatar
        // hanya dimatikan karena animasi yang sedang jalan bisa masih memegang transform-nya; RestoreNetworkAvatars
        // menghancurkannya saat meja dibersihkan.
        void RestoreNetworkAvatar(int config)
        {
            if (!networkAvatars.TryGetValue(config, out var remote) || remote == null || !remote.gameObject.activeSelf) return;
            remote.gameObject.SetActive(false);
            var seat = seats[config];
            seat.avatar = null;
            seat.avatarReaction = null;
            if (seat.character != null) seat.character.gameObject.SetActive(true);
            seat.Paralyzed = seat.Paralyzed;
            seat.Defeated = seat.Defeated;
        }

        internal bool TryGetLocalNetworkPose(out int config, out AvatarPose pose, out bool held, out Vector3 candy)
        {
            config = localConfigIndex;
            pose = human?.avatar != null ? human.avatar.CurrentPose : default;
            held = human?.avatar != null && vrInput != null && vrInput.IsHoldingCandy && HumanCanAct;
            candy = held && candyObject != null ? human.avatar.seat.InverseTransformPoint(candyObject.transform.position) : Vector3.zero;
            if (candy.sqrMagnitude >= 16) { held = false; candy = Vector3.zero; }
            return ready && config >= 0 && human?.avatar != null && pose.IsValid;
        }

        internal void ReceiveNetworkPose(int config, in AvatarPose pose)
        {
            if (networkAvatars.TryGetValue(config, out var remote)) remote.ReceivePose(pose);
        }

        internal void ReceiveHeldCandy(int config, bool held, Vector3 position)
        {
            if (position.sqrMagnitude >= 16 || config < 0 || config >= seats.Count) return;
            if (!held && remoteHeldSeat != config) return;
            remoteHeldSeat = config;
            remoteHolding = held;
            remoteCandyPosition = NetworkSeat(config).TransformPoint(position);
            remoteHeldAt = Time.unscaledTime;
        }

        internal void UpdateRemoteHeldCandy()
        {
            if (network == null || !network.InputOpen) { remoteHeldSeat = -1; return; }
            if (remoteHeldSeat < 0 || candyObject == null) return;
            bool held = remoteHolding && Time.unscaledTime - remoteHeldAt < .2f;
            candyObject.transform.position = Vector3.Lerp(candyObject.transform.position, held ? remoteCandyPosition : trayPosition, .6f);
            if (!held && Vector3.Distance(candyObject.transform.position, trayPosition) < .001f) remoteHeldSeat = -1;
        }

        void TrackNetworkItem(Transform item, ItemType type)
        {
            if (!NetworkActive || item == null) return;
            networkItems.Add(new NetworkVisual { id = ++nextNetworkItem, kind = (int)type + 1, transform = item });
        }

        internal void ResetNetworkVisuals()
        {
            foreach (var visual in receivedVisuals.Values)
                if (visual.id != 0 && visual.transform != null) Destroy(visual.transform.gameObject);
            receivedVisuals.Clear();
            networkItems.Clear();
            remoteHeldSeat = -1;
            hasVisuals = false;
            foreach (var remote in networkAvatars.Values) if (remote != null) remote.ResetStream();
        }

        internal byte[] CaptureNetworkVisuals(long game, long revision, uint sequence)
        {
            networkItems.RemoveAll(v => v.transform == null);
            using var stream = new MemoryStream(128);
            using var writer = new BinaryWriter(stream);
            writer.Write(game); writer.Write(revision); writer.Write(sequence);
            bool candy = candyObject != null && shownCandy != null;
            writer.Write((byte)(networkItems.Count + (candy ? 1 : 0)));
            if (candy) WriteVisual(writer, 0, 0, shownCandy.Id, candyObject.transform, (int)shownCandy.Color);
            foreach (var item in networkItems) WriteVisual(writer, item.id, item.kind, 0, item.transform, 0);
            return stream.ToArray();
        }

        void WriteVisual(BinaryWriter writer, int id, int kind, int model, Transform target, int color)
        {
            int anchor = -1;
            float weight = 0;
            if (id == 0 && remoteHolding && remoteHeldSeat >= 0 && network.InputOpen && Time.unscaledTime - remoteHeldAt < .2f)
            { anchor = remoteHeldSeat; weight = 1; }
            var basis = NetworkSeat(anchor);
            writer.Write(id); writer.Write((byte)kind); writer.Write(model); writer.Write((byte)color); writer.Write((sbyte)anchor);
            AvatarVisualWire.WriteTransform(writer, new Pose(basis.InverseTransformPoint(target.position), Quaternion.Inverse(basis.rotation) * target.rotation));
            AvatarVisualWire.WriteScale(writer, target.localScale);
            AvatarVisualWire.WriteTransform(writer, new Pose(artRoot.InverseTransformPoint(target.position), Quaternion.Inverse(artRoot.rotation) * target.rotation));
            writer.Write((byte)Mathf.RoundToInt(weight * 255));
        }

        internal void ReceiveNetworkVisuals(byte[] bytes, long game, long revision)
        {
            if (!ready || !NetworkClient || remoteState == null || bytes == null || bytes.Length > 384) return;
            try
            {
                using var reader = new BinaryReader(new MemoryStream(bytes, false));
                if (reader.ReadInt64() != game || reader.ReadInt64() != revision) return;
                uint sequence = reader.ReadUInt32();
                if (hasVisuals && unchecked((int)(sequence - receivedVisualSequence)) <= 0) return;
                int count = reader.ReadByte();
                if (count > 6 || bytes.Length != 21 + count * 46) return;
                var incoming = new List<NetworkVisual>(count);
                var ids = new HashSet<int>();
                for (int i = 0; i < count; i++)
                {
                    var visual = new NetworkVisual { id = reader.ReadInt32(), kind = reader.ReadByte(), model = reader.ReadInt32() };
                    visual.color = reader.ReadByte();
                    int anchor = reader.ReadSByte();
                    var pose = AvatarVisualWire.ReadTransform(reader);
                    visual.scale = AvatarVisualWire.ReadScale(reader);
                    var tablePose = AvatarVisualWire.ReadTransform(reader);
                    float weight = reader.ReadByte() / 255f;
                    if (anchor < -1 || anchor >= seats.Count || visual.kind > 8 || visual.color > 2 || visual.id < 0
                        || (visual.id == 0) != (visual.kind == 0) || !ids.Add(visual.id)
                        || visual.scale.x < 0 || visual.scale.y < 0 || visual.scale.z < 0) return;
                    var basis = NetworkSeat(anchor);
                    var mapped = MapTablePose(tablePose);
                    visual.target = new Pose(Vector3.Lerp(mapped.position, basis.TransformPoint(pose.position), weight),
                        Quaternion.Slerp(mapped.rotation, basis.rotation * pose.rotation, weight));
                    incoming.Add(visual);
                }
                foreach (int id in receivedVisuals.Keys.Where(id => !ids.Contains(id)).ToArray())
                {
                    var old = receivedVisuals[id];
                    if (old.transform != null) Destroy(old.transform.gameObject);
                    if (id == 0) { candyObject = null; candySelectable = null; shownCandy = null; }
                    receivedVisuals.Remove(id);
                }
                foreach (var visual in incoming)
                {
                    receivedVisuals.TryGetValue(visual.id, out var old);
                    if (old != null && old.model != visual.model)
                    {
                        if (old.transform != null) Destroy(old.transform.gameObject);
                        old = null;
                    }
                    visual.transform = old?.transform;
                    if (visual.transform == null) CreateNetworkVisual(visual, (CandyColor)visual.color);
                    if (visual.transform == null) continue;
                    visual.previous = new Pose(visual.transform.position, visual.transform.rotation);
                    visual.previousScale = visual.transform.localScale;
                    receivedVisuals[visual.id] = visual;
                }
                hasVisuals = true;
                receivedVisualSequence = sequence;
                visualsAt = Time.unscaledTime;
            }
            catch (IOException) { /* Unreliable boleh kehilangan paket; paket berikutnya berisi state lengkap. */ }
        }

        Pose MapTablePose(Pose pose)
        {
            var world = new Pose(artRoot.TransformPoint(pose.position), artRoot.rotation * pose.rotation);
            var result = world;
            float total = 0;
            int hostLocal = System.Array.FindIndex(remoteState.Seats, s => s.Controller == SeatController.Local);
            for (int i = 0; i < seats.Count; i++)
            {
                // Rekonstruksi susunan host dari kursi scene, sebelum pemain lokal klien dipindah ke Seat_0.
                int slot = i == hostLocal || (hostLocal < 0 && i == 0) ? 0 : i < hostLocal ? i + 1 : i;
                var original = allSeats[slot];
                var source = original == human ? NetworkSeat(localConfigIndex >= 0 ? localConfigIndex : 0) : original.character.transform.parent;
                var offset = source.InverseTransformPoint(world.position);
                float distance = Mathf.Max(.01f, offset.x * offset.x + offset.z * offset.z);
                float influence = 1 / (distance * distance * distance * distance * distance * distance);
                var destination = NetworkSeat(i);
                float t = influence / (total + influence);
                result.position = Vector3.Lerp(result.position, destination.TransformPoint(offset), t);
                result.rotation = Quaternion.Slerp(result.rotation, destination.rotation * Quaternion.Inverse(source.rotation) * world.rotation, t);
                total += influence;
            }
            // Pusat meja tetap di tempatnya. Bobot kontinu mencegah loncatan di antara dua kursi yang ditukar.
            float blend = Mathf.Clamp01((new Vector2(pose.position.x, pose.position.z).magnitude - .3f) / .2f);
            return new Pose(Vector3.Lerp(world.position, result.position, blend), Quaternion.Slerp(world.rotation, result.rotation, blend));
        }

        void CreateNetworkVisual(NetworkVisual visual, CandyColor color)
        {
            if (visual.id == 0)
            {
                candyObject = SpawnCandy(color, visual.target.position);
                shownCandy = State.ActiveCandy?.Id == visual.model ? State.ActiveCandy : null;
                AddBoundsCollider(candyObject);
                candySelectable = candyObject.AddComponent<TableSelectable>();
                candySelectable.kind = TableSelectable.Kind.Candy;
                candySelectable.onSelect = OnCandySelected;
                visual.transform = candyObject.transform;
                SetMode(mode);
            }
            else if (itemTemplates.TryGetValue((ItemType)(visual.kind - 1), out var template))
            {
                var item = Instantiate(template, transform);
                item.SetActive(true);
                foreach (var collider in item.GetComponentsInChildren<Collider>()) Destroy(collider);
                visual.transform = item.transform;
                temporary.Add(item);
            }
            if (visual.transform != null)
            {
                visual.transform.SetPositionAndRotation(visual.target.position, visual.target.rotation);
                visual.transform.localScale = visual.scale;
            }
        }

        internal void ApplyNetworkVisuals()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - visualsAt) / NetworkTableSession.PoseInterval);
            foreach (var visual in receivedVisuals.Values)
            {
                if (visual.transform == null || (visual.id == 0 && vrInput != null && vrInput.IsHoldingCandy && HumanCanAct)) continue;
                visual.transform.SetPositionAndRotation(Vector3.Lerp(visual.previous.position, visual.target.position, t),
                    Quaternion.Slerp(visual.previous.rotation, visual.target.rotation, t));
                visual.transform.localScale = Vector3.Lerp(visual.previousScale, visual.scale, t);
            }
        }
    }
}

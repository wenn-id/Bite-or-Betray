using System.Collections.Generic;
using System.IO;
using BiteOrBetray.Core;
using Unity.Netcode;
using UnityEngine;

namespace BiteOrBetray.Table3D.Net
{
    public sealed partial class NetworkTableSession
    {
        public const float PoseInterval = 1f / 25;
        readonly Dictionary<int, uint> poseSequences = new Dictionary<int, uint>();
        readonly Dictionary<int, float> poseTimes = new Dictionary<int, float>();
        float nextPose, nextVisual;
        uint poseSequence, visualSequence;

        void ResetVisualStream()
        {
            poseSequences.Clear();
            poseTimes.Clear();
            nextPose = nextVisual = 0;
            // Sequence tetap monoton selama objek hidup; paket lama dari game sebelumnya memakai GameId berbeda.
            if (director != null) director.ResetNetworkVisuals();
        }

        void LateUpdate()
        {
            if (!IsSpawned || Latest == null || director == null) return;
            float now = Time.unscaledTime;
            if (now >= nextPose && director.TryGetLocalNetworkPose(out int seat, out var pose, out bool held, out var candy))
            {
                nextPose = now + PoseInterval;
                pose.sequence = ++poseSequence;
                using var stream = new MemoryStream(80);
                using var writer = new BinaryWriter(stream);
                writer.Write(Latest.GameId); writer.Write(Latest.Revision);
                AvatarVisualWire.WritePose(writer, pose);
                writer.Write(held); AvatarVisualWire.WriteVector(writer, candy);
                var bytes = stream.ToArray();
                if (IsHost) ReceivePose(seat, bytes);
                else SubmitPoseRpc(bytes);
            }
            if (IsHost && now >= nextVisual)
            {
                nextVisual = now + PoseInterval;
                director.UpdateRemoteHeldCandy();
                ReceiveVisualsRpc(director.CaptureNetworkVisuals(Latest.GameId, Latest.Revision, ++visualSequence));
            }
            if (!IsServer) director.ApplyNetworkVisuals();
        }

        [Rpc(SendTo.Server, RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
        void SubmitPoseRpc(byte[] bytes, RpcParams rpcParams = default)
        {
            if (!IsHost || Authority == null) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            for (int i = 0; i < Authority.Seats.Count; i++)
                if (Authority.Seats[i].Controller == SeatController.Online && Authority.Seats[i].ClientId == sender)
                { ReceivePose(i, bytes); return; }
        }

        void ReceivePose(int seat, byte[] bytes)
        {
            if (Latest == null || bytes == null || bytes.Length != 74) return;
            float now = Time.unscaledTime;
            // Batas juga diterapkan di host, bukan hanya pada pengirim yang kooperatif.
            if (poseTimes.TryGetValue(seat, out float lastTime) && now - lastTime < 1f / 30) return;
            try
            {
                using var reader = new BinaryReader(new MemoryStream(bytes, false));
                if (reader.ReadInt64() != Latest.GameId) return;
                long revision = reader.ReadInt64();
                var pose = AvatarVisualWire.ReadPose(reader);
                bool held = reader.ReadBoolean();
                var candy = AvatarVisualWire.ReadVector(reader);
                if (poseSequences.TryGetValue(seat, out uint last) && unchecked((int)(pose.sequence - last)) <= 0) return;
                poseSequences[seat] = pose.sequence;
                poseTimes[seat] = now;
                director.ReceiveNetworkPose(seat, pose);
                if (revision == Latest.Revision && InputOpen && Authority != null
                    && Authority.Engine.Current.ConfigIndex == seat && Authority.Engine.ActiveCandy != null
                    && (!held || (pose.leftTracked && (candy - pose.leftHand.position).sqrMagnitude < .25f)
                        || (pose.rightTracked && (candy - pose.rightHand.position).sqrMagnitude < .25f)))
                    director.ReceiveHeldCandy(seat, held, candy);
                RelayPoseRpc(seat, bytes);
            }
            catch (IOException) { /* Paket rusak tidak mengubah state atau diteruskan. */ }
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server, Delivery = RpcDelivery.Unreliable)]
        void RelayPoseRpc(int seat, byte[] bytes)
        {
            if (Latest == null || bytes == null || bytes.Length != 74) return;
            try
            {
                using var reader = new BinaryReader(new MemoryStream(bytes, false));
                if (reader.ReadInt64() != Latest.GameId) return;
                reader.ReadInt64();
                director.ReceiveNetworkPose(seat, AvatarVisualWire.ReadPose(reader));
            }
            catch (IOException) { }
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server, Delivery = RpcDelivery.Unreliable)]
        void ReceiveVisualsRpc(byte[] bytes)
        {
            if (Latest != null && director != null) director.ReceiveNetworkVisuals(bytes, Latest.GameId, Latest.Revision);
        }
    }
}

using System;
using System.Threading.Tasks;
using BiteOrBetray.Core;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.Android;

namespace BiteOrBetray.Table3D.Net
{
    /// <summary>2D voice per UGS session ID. Knockouts/spectators do not leave the channel or change player mute state.</summary>
    sealed class RoomVoiceChat
    {
        readonly GameSettings settings;
        readonly Action<string> report;
        // VivoxService singleton: share the join/leave queue across instances so logout from the previous scene's director
        // (leaving a room/unloading a scene) finishes before the new director logs in.
        static Task pending = Task.CompletedTask;
        IVivoxService service;
        string wantedChannel, activeChannel;
        int generation;

        public bool InRoom => wantedChannel != null;
        // Table messages stay generic; preserve the underlying cause for logs and PlayMode failure messages (CI.md).
        public string LastError { get; private set; }

        public RoomVoiceChat(GameSettings settings, Action<string> report)
        {
            this.settings = settings;
            this.report = report;
        }

        public void SetRoom(string sessionId)
        {
            string channel = string.IsNullOrEmpty(sessionId) ? null : "bob-room-" + sessionId;
            if (wantedChannel == channel) return;
            wantedChannel = channel;
            int ticket = ++generation;
            ApplySettings();
            // Serialize join/leave: a late await result must not revive a room already left.
            pending = ChangeRoomAsync(pending, channel, ticket);
        }

        public void ApplySettings()
        {
            if (service == null || !service.IsLoggedIn) return;
            try
            {
                bool active = activeChannel != null && activeChannel == wantedChannel;
                if (settings.VoiceMuted || !active) service.MuteInputDevice();
                else service.UnmuteInputDevice();
                service.SetOutputDeviceVolume(settings.VoiceOutputVolume);
                if (settings.VoiceVolume == 0 || !active) service.MuteOutputDevice();
                else service.UnmuteOutputDevice();
            }
            catch (Exception e) { Report(e); }
        }

        async Task ChangeRoomAsync(Task previous, string channel, int ticket)
        {
            await previous;
            if (ticket != generation) return;
            bool joined = false;
            try
            {
                activeChannel = null;
                if (service != null && service.IsLoggedIn) await service.LogoutAsync();
                if (channel == null || ticket != generation) return;
                await RequestPermissionsAsync();
                if (ticket != generation) return;
                service = VivoxService.Instance;
                if (service.InitializationState != VivoxInitializationState.Initialized) await service.InitializeAsync();
                if (ticket != generation) return;
                await service.LoginAsync();
                ApplySettings();
                if (ticket != generation) return;
                await service.JoinGroupChannelAsync(channel, ChatCapability.AudioOnly,
                    new ChannelOptions { MakeActiveChannelUponJoining = true });
                if (ticket != generation) return;
                activeChannel = channel;
                ApplySettings();
                joined = true;
            }
            catch (Exception e)
            {
                if (ticket == generation && channel != null) Report(e);
            }
            finally
            {
                if (!joined)
                {
                    activeChannel = null;
                    // Logout also releases the channel if joining finished after the player left/disconnected.
                    try
                    {
                        if (service != null && service.IsLoggedIn)
                        {
                            service.MuteInputDevice();
                            service.MuteOutputDevice();
                            await service.LogoutAsync();
                        }
                    }
                    catch (Exception e) { Debug.LogWarning("[BoB] Voice leave failed: " + e.Message); }
                }
            }
        }

        void Report(Exception e)
        {
            var cause = e.GetBaseException();
            LastError = e.GetType().Name + ": " + e.Message + (cause != e ? " <- " + cause.GetType().Name + ": " + cause.Message : "");
            Debug.LogWarning("[BoB] Voice chat: " + e);
            report?.Invoke(Strings.VoiceUnavailable.Text);
        }

        static async Task RequestPermissionsAsync()
        {
            if (Application.platform != RuntimePlatform.Android) return;
            if (!await RequestPermissionAsync(Permission.Microphone))
                throw new InvalidOperationException("Microphone permission denied.");
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                if (version.GetStatic<int>("SDK_INT") >= 31)
                    await RequestPermissionAsync("android.permission.BLUETOOTH_CONNECT");
            }
        }

        static async Task<bool> RequestPermissionAsync(string permission)
        {
            if (Permission.HasUserAuthorizedPermission(permission)) return true;
            var answer = new TaskCompletionSource<bool>();
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => answer.TrySetResult(true);
            callbacks.PermissionDenied += _ => answer.TrySetResult(false);
            Permission.RequestUserPermission(permission, callbacks);
            var completed = await Task.WhenAny(answer.Task, Task.Delay(30000));
            return completed == answer.Task && await answer.Task;
        }
    }
}

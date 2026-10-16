using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using ISDevice = UnityEngine.InputSystem.InputDevice;
using ISXRController = UnityEngine.InputSystem.XR.XRController;
using ISXRRumble = UnityEngine.InputSystem.XR.XRControllerWithRumble;
using LegacyDevice = UnityEngine.XR.InputDevice;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Input VR untuk kursi pemain. Tiap controller punya pointer; trigger memilih objek (bot, item, tombol).
    /// Permen bisa diambil (ditarik ke tangan), didekatkan ke mulut untuk makan, atau dilepas sambil menunjuk bot
    /// untuk menyuapi. Semua aksi memanggil TableSelectable.Select(), sama seperti klik mouse.
    /// Hanya aktif kalau headset XR berjalan. Tombol dibaca lewat OVRInput (Meta), dengan Input System dan
    /// UnityEngine.XR.InputDevices sebagai cadangan; pose tangan diambil dari anchor Camera Rig Meta.
    /// </summary>
    public sealed class VRTableInput : MonoBehaviour
    {
        public float rayLength = 4;
        [Tooltip("Jarak permen ke mulut (meter) supaya dihitung dimakan.")]
        public float mouthDistance = .15f;
        public Color rayColor = new Color(1, .95f, .85f, .7f);
        public Color rayHoverColor = new Color(.35f, 1, .55f, 1);
        [Tooltip("Tulis ke Console status controller dan setiap tombol yang ditekan (untuk diagnosa input).")]
        public bool logInput;

        sealed class Hand
        {
            public XRNode node;
            public string anchorName;
            public Transform pose;
            public LineRenderer line;
            public TableSelectable hovered;
            public Vector3 hitPoint;
            public bool trigger, grip, secondary, grabbedWithGrip;
            public bool hasState, ovrConnected, legacyValid;
            public ISDevice inputDevice;
            public LegacyDevice legacyDevice;
            public Transform statusPose;
        }

        readonly Hand[] hands = {
            new Hand { node = XRNode.LeftHand, anchorName = "Left" },
            new Hand { node = XRNode.RightHand, anchorName = "Right" }
        };
        CandyTableDirector director;
        Hand holder;
        TableSelectable heldCandy;
        Vector3 candyHome;
        Material lineMaterial;
        TextMesh tooltip;
        bool active;
        public bool IsXRActive => active;
        OVRCameraRig rig;
        float nextAnchorSearch;
        int anchorSearches;
        const int MaxAnchorSearches = 30;
        bool actedThisFrame;
        bool appFocused = true, appRunning = true, inputFocused = true, suspended;
        int resumeFrame = int.MinValue;
        bool Focused => appFocused && appRunning && inputFocused;

        /// <summary>
        /// False selama aplikasi tidak fokus, ter-pause, atau input diambil sistem (menu Quest terbuka).
        /// Frame pertama setelah fokus kembali juga dihitung belum fokus: Time.deltaTime di frame itu bisa memuat
        /// jeda selama pause, dan tidak boleh memotong timer giliran.
        /// </summary>
        public bool HasFocus => Focused && Time.frameCount > resumeFrame + 1;

        static readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

        /// <summary>
        /// True kalau XR sudah di-load (ada display subsystem). Sengaja tidak hanya memakai XRSettings.isDeviceActive:
        /// dengan OpenXR (Link/Simulator) nilainya baru true beberapa frame setelah scene dimuat.
        /// </summary>
        public static bool XRActive
        {
            get
            {
                if (XRSettings.isDeviceActive) return true;
                SubsystemManager.GetSubsystems(displays);
                return displays.Count > 0;
            }
        }

        void Awake()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // Nilai serialized dari scene lama juga harus mati di build release.
            logInput = false;
#endif
            director = GetComponent<CandyTableDirector>();
            TryActivate();
        }

        void OnEnable()
        {
            OVRManager.InputFocusLost += OnInputFocusLost;
            OVRManager.InputFocusAcquired += OnInputFocusAcquired;
        }

        void OnDisable()
        {
            OVRManager.InputFocusLost -= OnInputFocusLost;
            OVRManager.InputFocusAcquired -= OnInputFocusAcquired;
        }

        void OnApplicationFocus(bool focused) => SetFocus(ref appFocused, focused);
        void OnApplicationPause(bool paused) => SetFocus(ref appRunning, !paused);
        void OnInputFocusLost() => SetFocus(ref inputFocused, false);
        void OnInputFocusAcquired() => SetFocus(ref inputFocused, true);

        void SetFocus(ref bool flag, bool value)
        {
            if (flag == value) return;
            flag = value;
            if (Focused) resumeFrame = Time.frameCount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logInput) Debug.Log($"[Bite or Betray][VR] Fokus {(Focused ? "kembali" : "hilang")}");
#endif
        }

        void TryActivate()
        {
            if (active || !XRActive) return;
            active = true;
            SwitchToRig();
            CreateVisuals();
        }

        /// <summary>Nyalakan Camera Rig VR dan matikan kamera preview desktop (keduanya bertag MainCamera).</summary>
        static void SwitchToRig()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                {
                    if (root.name.Contains("Camera Rig") && !root.activeSelf) root.SetActive(true);
                    else if (root.name == "Desktop Art Preview") root.SetActive(false);
                }
        }

        void CreateVisuals()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null) lineMaterial = new Material(shader) { name = "BoB VR Pointer" };
            foreach (var hand in hands)
            {
                var go = new GameObject("Pointer " + hand.anchorName);
                go.transform.SetParent(transform, false);
                hand.line = go.AddComponent<LineRenderer>();
                hand.line.positionCount = 2;
                hand.line.startWidth = .004f;
                hand.line.endWidth = .002f;
                hand.line.useWorldSpace = true;
                hand.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                hand.line.receiveShadows = false;
                if (lineMaterial != null) hand.line.sharedMaterial = new Material(lineMaterial);
            }
            tooltip = WorldText.Create("Tooltip VR", transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                .0035f, new Color(.23f, .15f, .12f));
            tooltip.anchor = TextAnchor.LowerCenter;
        }

        /// <summary>
        /// Pose tangan diambil langsung dari anchor OVRCameraRig (anchor controller, cadangannya anchor tangan).
        /// Rig dicari paling sering sekali per detik dan paling banyak MaxAnchorSearches kali; setelah ketemu, rig
        /// disimpan dan tidak dicari lagi.
        /// </summary>
        void FindAnchors()
        {
            if (rig == null)
            {
                if (anchorSearches >= MaxAnchorSearches || Time.unscaledTime < nextAnchorSearch) return;
                anchorSearches++;
                nextAnchorSearch = Time.unscaledTime + 1;
                rig = FindFirstObjectByType<OVRCameraRig>();
                if (rig == null)
                {
                    if (anchorSearches == MaxAnchorSearches)
                        Debug.LogWarning("[Bite or Betray][VR] OVRCameraRig tidak ditemukan, pointer VR tidak aktif.");
                    return;
                }
            }
            if (hands[0].pose == null) hands[0].pose = rig.leftControllerAnchor != null ? rig.leftControllerAnchor : rig.leftHandAnchor;
            if (hands[1].pose == null) hands[1].pose = rig.rightControllerAnchor != null ? rig.rightControllerAnchor : rig.rightHandAnchor;
        }

        void Update()
        {
            if (!active)
            {
                TryActivate();
                if (!active) return;
            }
            if (!HasFocus)
            {
                Suspend();
                return;
            }
            suspended = false;
            if (hands[0].pose == null || hands[1].pose == null) FindAnchors();
            // Spectator tetap melacak kepala/tangan, tetapi pointer dan aksi gameplay berhenti sampai game selesai.
            if (director != null && director.SpectatorInputLocked)
            {
                ClearGameplayInteraction();
                foreach (var hand in hands)
                {
                    ReadButtons(hand, out hand.trigger, out hand.grip, out hand.secondary);
                    if (hand.line != null) hand.line.enabled = false;
                }
                return;
            }
            // Tangan kanan diproses lebih dulu: kalau kedua tangan memilih di frame yang sama, aksi kanan yang jalan.
            actedThisFrame = false;
            for (int i = hands.Length - 1; i >= 0; i--) UpdateHand(hands[i]);
            UpdateHeldCandy();
            UpdateTooltip();
        }

        /// <summary>
        /// Fokus hilang: sembunyikan pointer dan tooltip, kembalikan permen yang dipegang. Tombol dianggap masih
        /// ditekan, jadi setelah fokus kembali aksi baru jalan kalau tombol dilepas lalu ditekan lagi.
        /// </summary>
        void Suspend()
        {
            if (suspended) return;
            suspended = true;
            foreach (var hand in hands)
            {
                hand.trigger = hand.grip = hand.secondary = true;
                if (hand.line != null) hand.line.enabled = false;
                SetHovered(hand, null);
            }
            var candy = heldCandy;
            heldCandy = null;
            holder = null;
            if (candy != null) StartCoroutine(ReturnHome(candy.transform, candyHome));
            if (tooltip != null) tooltip.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ input

        static bool HandTracked(Hand hand)
        {
            if (hand.pose == null || !hand.pose.gameObject.activeInHierarchy) return false;
            var touch = Touch(hand.node);
            if (OVRInput.GetControllerPositionTracked(touch) && OVRInput.GetControllerOrientationTracked(touch)) return true;
            var device = InputDevices.GetDeviceAtXRNode(hand.node);
            return device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
        }

        public bool TryGetTrackedHand(XRNode node, out Transform anchor, out float grip, out float index)
        {
            var hand = hands[node == XRNode.LeftHand ? 0 : 1];
            anchor = hand.pose;
            grip = Mathf.Max(OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, Touch(node)), hand.grip ? 1 : 0);
            index = Mathf.Max(OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, Touch(node)), hand.trigger ? 1 : 0);
            return active && HandTracked(hand);
        }

        static ISDevice InputSystemHand(XRNode node) => node == XRNode.LeftHand ? ISXRController.leftHand : ISXRController.rightHand;

        static OVRInput.Controller Touch(XRNode node) => node == XRNode.LeftHand ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

        // Sumber utama OVRInput: dengan Camera Rig Meta, controller masuk lewat OVRPlugin (jalur yang juga menggerakkan
        // anchor tangan), dan di setup ini tidak muncul di Input System maupun InputDevices. Dua sisanya cadangan.
        void ReadButtons(Hand hand, out bool trigger, out bool grip, out bool secondary)
        {
            var touch = Touch(hand.node);
            var device = InputSystemHand(hand.node);
            var legacy = InputDevices.GetDeviceAtXRNode(hand.node);
            trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, touch) > .6f
                || Pressed(device, "triggerPressed") || Value(device, "trigger") > .6f
                || Button(legacy, CommonUsages.triggerButton) || Axis(legacy, CommonUsages.trigger) > .6f;
            grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, touch) > .6f
                || Pressed(device, "gripPressed") || Value(device, "grip") > .6f
                || Button(legacy, CommonUsages.gripButton) || Axis(legacy, CommonUsages.grip) > .6f;
            secondary = OVRInput.Get(OVRInput.Button.Two, touch)
                || Pressed(device, "secondaryButton") || Button(legacy, CommonUsages.secondaryButton);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!logInput) { hand.hasState = false; return; }
            bool connected = OVRInput.IsControllerConnected(touch);
            bool valid = legacy.isValid;
            if (hand.hasState && hand.ovrConnected == connected && hand.inputDevice == device
                && hand.legacyDevice == legacy && hand.legacyValid == valid && hand.statusPose == hand.pose) return;
            hand.hasState = true;
            hand.ovrConnected = connected;
            hand.inputDevice = device;
            hand.legacyDevice = legacy;
            hand.legacyValid = valid;
            hand.statusPose = hand.pose;
            // Nama native dan interpolasi hanya dibaca saat sumber input berubah.
            string state = $"OVRInput = {(connected ? "tersambung" : "tidak ada")}, Input System = {(device != null ? device.layout : "tidak ada")}, InputDevices = {(valid ? legacy.name : "tidak ada")}, pose = {(hand.pose != null ? hand.pose.name : "belum ketemu")}";
            Debug.Log($"[Bite or Betray][VR] Controller {hand.anchorName}: {state}");
#endif
        }

        void UpdateHand(Hand hand)
        {
            ReadButtons(hand, out bool trigger, out bool grip, out bool secondary);
            bool triggerPressed = trigger && !hand.trigger, triggerReleased = !trigger && hand.trigger;
            bool gripPressed = grip && !hand.grip, gripReleased = !grip && hand.grip;
            bool secondaryPressed = secondary && !hand.secondary;
            hand.trigger = trigger;
            hand.grip = grip;
            hand.secondary = secondary;

            if (!HandTracked(hand))
            {
                if (hand.line != null) hand.line.enabled = false;
                SetHovered(hand, null);
                // Tombol tetap dibaca saat tracking hilang, jadi pelepasan tidak terlewat. Arah tangan tidak diketahui,
                // sehingga permen tidak menyuapi siapa pun dan kembali ke tempatnya.
                if (holder == hand && (hand.grabbedWithGrip ? gripReleased : triggerReleased)) ReleaseCandy(hand);
                return;
            }

            // Pointer: saat memegang permen, yang bisa dituju hanya bot (untuk menyuapi).
            var ray = new Ray(hand.pose.position, hand.pose.forward);
            var target = TableSelectable.Raycast(ray, rayLength, out var hit, heldCandy, holder == hand);
            float distance = rayLength;
            if (target != null)
            {
                distance = hit.distance;
                hand.hitPoint = hit.point;
            }
            SetHovered(hand, target);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logInput && (triggerPressed || gripPressed || secondaryPressed))
            {
                string what = target == null ? "tidak ada target interaktif"
                    : $"{hit.collider.name} [{target.kind}, interactable={target.interactable}]";
                Debug.Log($"[Bite or Betray][VR] {hand.anchorName} {(triggerPressed ? "trigger" : gripPressed ? "grip" : "B/Y")} -> {what}");
            }
#endif
            hand.line.enabled = true;
            hand.line.SetPosition(0, ray.origin);
            hand.line.SetPosition(1, ray.origin + ray.direction * (target != null ? distance : Mathf.Min(distance, .6f)));
            var color = target != null ? rayHoverColor : rayColor;
            hand.line.startColor = hand.line.endColor = color;
            if (lineMaterial != null) hand.line.sharedMaterial.SetColor("_BaseColor", color);

            if (secondaryPressed && director != null && TryAct(hand)) director.Cancel();
            if (holder == hand)
            {
                if (hand.grabbedWithGrip ? gripReleased : triggerReleased) ReleaseCandy(hand);
                return;
            }
            if (!(triggerPressed || gripPressed) || hand.hovered == null) return;
            if (hand.hovered.kind == TableSelectable.Kind.Candy)
            {
                if (holder == null && TryAct(hand)) Grab(hand, gripPressed && !triggerPressed);
                return;
            }
            if (!TryAct(hand)) return;
            var selected = hand.hovered;
            SetHovered(hand, null);
            selected.Select();
            Haptic(hand, .6f, .08f);
        }

        /// <summary>
        /// Paling banyak satu aksi (pilih, ambil/suapi/makan permen, batal) per frame. Tangan yang kalah mendapat
        /// getar pendek dan harus menekan ulang.
        /// </summary>
        bool TryAct(Hand hand)
        {
            if (!actedThisFrame) return actedThisFrame = true;
            Haptic(hand, .1f, .04f);
            if (logInput) Debug.Log($"[Bite or Betray][VR] {hand.anchorName}: diabaikan, tangan lain sudah beraksi di frame ini");
            return false;
        }

        void SetHovered(Hand hand, TableSelectable target)
        {
            if (target == hand.hovered) return;
            if (hand.hovered != null && hand.hovered.kind != TableSelectable.Kind.Bot) hand.hovered.SetHover(false);
            hand.hovered = target;
            if (target == null) return;
            if (target.kind != TableSelectable.Kind.Bot) target.SetHover(true);
            Haptic(hand, .15f, .03f);
        }

        // ------------------------------------------------------------------ permen di tangan

        public void ClearGameplayInteraction()
        {
            // Director sudah mengambil alih permen untuk animasi; jangan jalankan ReturnHome yang menimpanya.
            heldCandy = null;
            holder = null;
            foreach (var hand in hands) SetHovered(hand, null);
            if (tooltip != null) tooltip.gameObject.SetActive(false);
        }

        void Grab(Hand hand, bool withGrip)
        {
            heldCandy = hand.hovered;
            SetHovered(hand, null);
            holder = hand;
            hand.grabbedWithGrip = withGrip;
            candyHome = heldCandy.transform.position;
            Haptic(hand, .5f, .1f);
        }

        void ReleaseCandy(Hand hand)
        {
            var candy = heldCandy;
            var target = hand.hovered;
            heldCandy = null;
            holder = null;
            SetHovered(hand, null);
            if (target != null && target.kind == TableSelectable.Kind.Bot && target.interactable && TryAct(hand))
            {
                target.Select();
                Haptic(hand, .7f, .12f);
            }
            else if (candy != null) StartCoroutine(ReturnHome(candy.transform, candyHome));
        }

        void UpdateHeldCandy()
        {
            if (holder == null) return;
            // Permen diambil alih director (misalnya waktu habis): lepaskan tanpa mengembalikan, juga saat tracking hilang.
            if (heldCandy == null || !heldCandy.interactable)
            {
                heldCandy = null;
                holder = null;
                return;
            }
            if (!HandTracked(holder)) return;
            var candy = heldCandy.transform;
            Vector3 target = holder.pose.position + holder.pose.forward * .06f;
            candy.position = Vector3.Lerp(candy.position, target, 1 - Mathf.Exp(-18 * Time.deltaTime));

            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return;
            Vector3 mouth = head.position - head.up * .08f + head.forward * .05f;
            // Kalau tangan lain sudah beraksi di frame ini, makan ditunda ke frame berikutnya.
            if (actedThisFrame || Vector3.Distance(candy.position, mouth) > mouthDistance) return;
            actedThisFrame = true;
            var hand = holder;
            var eaten = heldCandy;
            heldCandy = null;
            holder = null;
            eaten.Select();
            Haptic(hand, .9f, .2f);
        }

        IEnumerator ReturnHome(Transform candy, Vector3 home)
        {
            Vector3 from = candy.position;
            for (float t = 0; t < .25f; t += Time.deltaTime)
            {
                if (candy == null || heldCandy != null) yield break;
                candy.position = Vector3.Lerp(from, home, Mathf.SmoothStep(0, 1, t / .25f));
                yield return null;
            }
            if (candy != null && heldCandy == null) candy.position = home;
        }

        void UpdateTooltip()
        {
            if (tooltip == null) return;
            string text = null;
            Vector3 position = default;
            if (holder != null && heldCandy != null)
            {
                text = holder.hovered != null ? "Lepas = " + holder.hovered.tooltip.ToLowerInvariant()
                    : "Dekatkan ke mulut = makan\nArahkan ke bot lalu lepas = suapi";
                position = heldCandy.transform.position + Vector3.up * .06f;
            }
            else
                foreach (var hand in hands)
                    if (hand.hovered != null && !string.IsNullOrEmpty(hand.hovered.tooltip))
                    {
                        text = hand.hovered.tooltip;
                        position = hand.hitPoint + Vector3.up * .05f;
                        break;
                    }
            tooltip.gameObject.SetActive(text != null);
            if (text == null) return;
            tooltip.text = text;
            tooltip.transform.position = position;
            var head = Camera.main;
            if (head != null)
            {
                Vector3 away = position - head.transform.position;
                tooltip.transform.rotation = Quaternion.LookRotation(away);
                tooltip.transform.localScale = Vector3.one * Mathf.Clamp(away.magnitude / .8f, .8f, 2.5f);
            }
        }

        // ------------------------------------------------------------------ util

        static bool Pressed(ISDevice device, string control) =>
            device != null && device.TryGetChildControl<ButtonControl>(control) is ButtonControl button && button.isPressed;

        static float Value(ISDevice device, string control) =>
            device != null && device.TryGetChildControl<AxisControl>(control) is AxisControl axis ? axis.ReadValue() : 0;

        static bool Button(LegacyDevice device, InputFeatureUsage<bool> usage) =>
            device.isValid && device.TryGetFeatureValue(usage, out bool value) && value;

        static float Axis(LegacyDevice device, InputFeatureUsage<float> usage) =>
            device.isValid && device.TryGetFeatureValue(usage, out float value) ? value : 0;

        void Haptic(Hand hand, float amplitude, float seconds)
        {
            var touch = Touch(hand.node);
            if (OVRInput.IsControllerConnected(touch))
            {
                OVRInput.SetControllerVibration(1, amplitude, touch);
                StartCoroutine(StopVibration(touch, seconds));
            }
            else if (InputSystemHand(hand.node) is ISXRRumble rumble) rumble.SendImpulse(amplitude, seconds);
            else
            {
                var legacy = InputDevices.GetDeviceAtXRNode(hand.node);
                if (legacy.isValid) legacy.SendHapticImpulse(0, amplitude, seconds);
            }
        }

        static IEnumerator StopVibration(OVRInput.Controller touch, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            OVRInput.SetControllerVibration(0, 0, touch);
        }
    }
}

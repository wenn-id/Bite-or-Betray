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
        public bool logInput = true;

        sealed class Hand
        {
            public XRNode node;
            public string anchorName;
            public Transform pose;
            public LineRenderer line;
            public TableSelectable hovered;
            public Vector3 hitPoint;
            public bool trigger, grip, secondary, grabbedWithGrip;
            public string lastState;
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
        float nextAnchorSearch;
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
            if (logInput) Debug.Log($"[Bite or Betray][VR] Fokus {(Focused ? "kembali" : "hilang")}");
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

        void FindAnchors()
        {
            foreach (var hand in hands)
            {
                if (hand.pose != null) continue;
                foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t.name == hand.anchorName + "ControllerAnchor") { hand.pose = t; break; }
                    if (t.name == hand.anchorName + "HandAnchor") hand.pose = t;
                }
            }
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
            if ((hands[0].pose == null || hands[1].pose == null) && Time.unscaledTime >= nextAnchorSearch)
            {
                nextAnchorSearch = Time.unscaledTime + 1;
                FindAnchors();
            }
            foreach (var hand in hands) UpdateHand(hand);
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

            if (!logInput) return;
            string state = $"OVRInput = {(OVRInput.IsControllerConnected(touch) ? "tersambung" : "tidak ada")}, Input System = {(device != null ? device.layout : "tidak ada")}, InputDevices = {(legacy.isValid ? legacy.name : "tidak ada")}, pose = {(hand.pose != null ? hand.pose.name : "belum ketemu")}";
            if (state == hand.lastState) return;
            hand.lastState = state;
            Debug.Log($"[Bite or Betray][VR] Controller {hand.anchorName}: {state}");
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
                return;
            }

            // Pointer: saat memegang permen, yang bisa dituju hanya bot (untuk menyuapi).
            var ray = new Ray(hand.pose.position, hand.pose.forward);
            TableSelectable target = null;
            float distance = rayLength;
            bool didHit = Physics.Raycast(ray, out var hit, rayLength, ~0, QueryTriggerInteraction.Collide);
            if (didHit)
            {
                distance = hit.distance;
                hand.hitPoint = hit.point;
                target = hit.collider.GetComponentInParent<TableSelectable>();
                if (target != null && (!target.interactable || target == heldCandy)) target = null;
                if (target != null && holder == hand && target.kind != TableSelectable.Kind.Bot) target = null;
            }
            SetHovered(hand, target);
            if (logInput && (triggerPressed || gripPressed || secondaryPressed))
            {
                string what = !didHit ? "tidak kena apa-apa"
                    : hit.collider.name + (hit.collider.GetComponentInParent<TableSelectable>() is TableSelectable s
                        ? $" [{s.kind}, interactable={s.interactable}]" : " [bukan objek meja]");
                Debug.Log($"[Bite or Betray][VR] {hand.anchorName} {(triggerPressed ? "trigger" : gripPressed ? "grip" : "B/Y")} -> {what}");
            }
            hand.line.enabled = true;
            hand.line.SetPosition(0, ray.origin);
            hand.line.SetPosition(1, ray.origin + ray.direction * (target != null ? distance : Mathf.Min(distance, .6f)));
            var color = target != null ? rayHoverColor : rayColor;
            hand.line.startColor = hand.line.endColor = color;
            if (lineMaterial != null) hand.line.sharedMaterial.SetColor("_BaseColor", color);

            if (secondaryPressed && director != null) director.Cancel();
            if (holder == hand)
            {
                if (hand.grabbedWithGrip ? gripReleased : triggerReleased) ReleaseCandy(hand);
                return;
            }
            if (!(triggerPressed || gripPressed) || hand.hovered == null) return;
            if (hand.hovered.kind == TableSelectable.Kind.Candy)
            {
                if (holder == null) Grab(hand, gripPressed && !triggerPressed);
                return;
            }
            var selected = hand.hovered;
            SetHovered(hand, null);
            selected.Select();
            Haptic(hand, .6f, .08f);
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
            if (target != null && target.kind == TableSelectable.Kind.Bot && target.interactable)
            {
                target.Select();
                Haptic(hand, .7f, .12f);
            }
            else if (candy != null) StartCoroutine(ReturnHome(candy.transform, candyHome));
        }

        void UpdateHeldCandy()
        {
            if (holder == null) return;
            if (!HandTracked(holder)) return;
            // Permen diambil alih director (misalnya waktu habis): lepaskan tanpa mengembalikan.
            if (heldCandy == null || !heldCandy.interactable)
            {
                heldCandy = null;
                holder = null;
                return;
            }
            var candy = heldCandy.transform;
            Vector3 target = holder.pose.position + holder.pose.forward * .06f;
            candy.position = Vector3.Lerp(candy.position, target, 1 - Mathf.Exp(-18 * Time.deltaTime));

            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return;
            Vector3 mouth = head.position - head.up * .08f + head.forward * .05f;
            if (Vector3.Distance(candy.position, mouth) > mouthDistance) return;
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

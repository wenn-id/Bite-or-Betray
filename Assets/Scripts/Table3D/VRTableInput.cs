using System.Collections;
using System.Collections.Generic;
using BiteOrBetray.Art3D;
using BiteOrBetray.Core;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR;
using ISDevice = UnityEngine.InputSystem.InputDevice;
using ISXRController = UnityEngine.InputSystem.XR.XRController;
using ISXRRumble = UnityEngine.InputSystem.XR.XRControllerWithRumble;
using LegacyDevice = UnityEngine.XR.InputDevice;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// VR input for the player's seat. Each controller has a pointer; trigger selects objects (bots, items, buttons).
    /// Candy can be picked up (pulled to the hand), brought to the mouth to eat, or released while pointing at a bot
    /// to feed it. Every action calls TableSelectable.Select(), just like a mouse click.
    /// Only active with a running XR headset. Controllers use OVRInput (Input System/InputDevices fallback),
    /// hands use Meta XR pinch through OVRPlugin, including the OpenXR backend; poses come from Meta Camera Rig.
    /// </summary>
    public sealed class VRTableInput : MonoBehaviour
    {
        public float rayLength = 4;
        [Tooltip("Candy mouth snap radius (meters); set from Settings.")]
        public float mouthDistance = .15f;
        [Tooltip("Index finger pinch to select/hold. Requires Meta XR Hands and Controllers support; tracked controllers still take priority.")]
        public bool enableHandTracking = true;
        [Tooltip("Left hand dominant: if both hands act in the same frame, the left action wins. Set from Settings.")]
        public bool leftHanded;
        public Color rayColor = new Color(1, .95f, .85f, .7f);
        public Color rayHoverColor = new Color(.35f, 1, .55f, 1);
        [Tooltip("URP Unlit material asset for pointers; copied per hand for separate hover colors.")]
        public Material pointerMaterial;
        [Tooltip("Log controller status and each button press to the Console (input diagnostics).")]
        public bool logInput;

        sealed class Hand
        {
            public XRNode node;
            public string anchorName;
            public Transform pose;
            public LineRenderer line;
            public Material material;
            public TableSelectable hovered;
            public Vector3 hitPoint;
            public bool trigger, grip, secondary, grabbedWithGrip;
            public bool hasState, ovrConnected, legacyValid;
            public ISDevice inputDevice;
            public LegacyDevice legacyDevice;
            public Transform statusPose;
            public Transform controllerPose;
            public OVRPlugin.HandState handState;
            public bool usesHandTracking, handTracked, pointerValid, waitForRelease;
            public Ray handRay;
        }

        readonly Hand[] hands = {
            new Hand { node = XRNode.LeftHand, anchorName = "Left" },
            new Hand { node = XRNode.RightHand, anchorName = "Right" }
        };
        CandyTableDirector director;
        Hand holder;
        TableSelectable heldCandy;
        Vector3 candyHome;
        TextMesh tooltip;
        bool active;
        public bool IsXRActive => active;
        public bool IsHoldingCandy => heldCandy != null && holder != null;
        OVRCameraRig rig;
        float nextAnchorSearch;
        int anchorSearches;
        const int MaxAnchorSearches = 30;
        bool actedThisFrame;
        bool appFocused = true, appRunning = true, inputFocused = true, suspended;
        int resumeFrame = int.MinValue;
        bool Focused => appFocused && appRunning && inputFocused;

        /// <summary>
        /// False while the application is unfocused, paused, or the system captures input (Quest menu open).
        /// The first frame after focus returns also counts as unfocused: its Time.deltaTime may include
        /// the pause duration, which must not shorten the turn timer.
        /// </summary>
        public bool HasFocus => Focused && Time.frameCount > resumeFrame + 1;

        static readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

        /// <summary>
        /// True if XR is loaded (a display subsystem exists). Deliberately not just XRSettings.isDeviceActive:
        /// with OpenXR (Link/Simulator), that becomes true several frames after scene loading.
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
            // Serialized values from old scenes must also be disabled in release builds.
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

        void OnDestroy()
        {
            foreach (var hand in hands)
                if (hand.material != null) Destroy(hand.material);
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
            if (logInput) Debug.Log($"[Bite or Betray][VR] Focus {(Focused ? "regained" : "lost")}");
#endif
        }

        void TryActivate()
        {
            if (active || !XRActive) return;
            if (pointerMaterial == null)
            {
                Debug.LogError("[Bite or Betray][VR] Pointer material not assigned in the scene.");
                enabled = false;
                return;
            }
            active = true;
            ArtPreviewControls.UseCamera(xr: true);
            StartCoroutine(ConfigureQuestRendering());
            CreateVisuals();
        }

        // Wait for the rig/OVR to be ready so SDK startup settings do not override the Quest budget.
        static IEnumerator ConfigureQuestRendering()
        {
            if (Application.platform != RuntimePlatform.Android) yield break;
            while (OVRManager.instance == null || !OVRManager.isHmdPresent) yield return null;
            yield return null;
            // The rig's default dynamic resolution can raise Quest 3 scale to 1.6.
            OVRManager.instance.enableDynamicResolution = false;
            XRSettings.eyeTextureResolutionScale = 1f;
            XRSettings.renderViewportScale = 1f;
            OVRManager.eyeTrackedFoveatedRenderingEnabled = false;
            OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.High;
            OVRManager.useDynamicFoveatedRendering = true;
            Debug.Log("[Bite or Betray][VR] Quest budget: FFR High + dynamic, XR scale 1.0 (without dynamic resolution).");
        }

        void CreateVisuals()
        {
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
                hand.material = new Material(pointerMaterial) { name = "BoB VR Pointer " + hand.anchorName };
                hand.line.sharedMaterial = hand.material;
            }
            tooltip = WorldText.Create("VR Tooltip", transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                .0035f, new Color(.23f, .15f, .12f));
            tooltip.anchor = TextAnchor.LowerCenter;
        }

        /// <summary>
        /// Read hand poses directly from OVRCameraRig anchors (controller anchors, falling back to hand anchors).
        /// Search for the rig at most once per second and at most MaxAnchorSearches times; once found,
        /// cache it and stop searching.
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
                        Debug.LogWarning("[Bite or Betray][VR] OVRCameraRig not found, VR pointers inactive.");
                    return;
                }
            }
            if (hands[0].controllerPose == null) hands[0].controllerPose = rig.leftControllerAnchor != null ? rig.leftControllerAnchor : rig.leftHandAnchor;
            if (hands[1].controllerPose == null) hands[1].controllerPose = rig.rightControllerAnchor != null ? rig.rightControllerAnchor : rig.rightHandAnchor;
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
            if (hands[0].controllerPose == null || hands[1].controllerPose == null) FindAnchors();
            foreach (var hand in hands) UpdateInputSource(hand);
            // Spectators only use the pointer for voice Settings; heads/hands remain tracked.
            if (director != null && director.SpectatorInputLocked && holder != null) ClearGameplayInteraction();
            if (director != null && director.SpectatorInputLocked && !director.VoiceRoomActive)
            {
                ClearGameplayInteraction();
                foreach (var hand in hands)
                {
                    ReadButtons(hand, out hand.trigger, out hand.grip, out hand.secondary);
                    if (hand.line != null) hand.line.enabled = false;
                }
                return;
            }
            // Process the dominant hand first: if both hands select in the same frame, the dominant action wins.
            actedThisFrame = false;
            int first = leftHanded ? 0 : 1;
            UpdateHand(hands[first]);
            UpdateHand(hands[1 - first]);
            UpdateHeldCandy();
            UpdateTooltip();
        }

        /// <summary>
        /// Focus lost: hide pointers and tooltips, return held candy. Treat buttons as still
        /// pressed, so after focus returns an action requires releasing and pressing again.
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

        // Choose one source per hand. Controller data is never combined with hand pinch.
        void UpdateInputSource(Hand hand)
        {
            bool useHands = enableHandTracking && rig != null && OVRPlugin.GetHandTrackingEnabled()
                && !ControllerTracked(hand);
            bool wasTracked = hand.handTracked;
            hand.handTracked = hand.pointerValid = false;
            if (useHands)
            {
                var side = hand.node == XRNode.LeftHand ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
                if (OVRPlugin.GetHandState(OVRPlugin.Step.Render, side, ref hand.handState))
                {
                    var state = hand.handState;
                    hand.handTracked = (state.Status & OVRPlugin.HandStatus.HandTracked) != 0
                        && (state.Status & OVRPlugin.HandStatus.SystemGestureInProgress) == 0
                        && state.HandConfidence == OVRPlugin.TrackingConfidence.High
                        && state.FingerConfidences != null
                        && state.FingerConfidences[(int)OVRPlugin.HandFinger.Index] == OVRPlugin.TrackingConfidence.High;
                    hand.pointerValid = hand.handTracked && (state.Status & OVRPlugin.HandStatus.InputStateValid) != 0;
                    if (hand.pointerValid)
                        hand.handRay = new Ray(rig.trackingSpace.TransformPoint(state.PointerPose.Position.FromFlippedZVector3f()),
                            rig.trackingSpace.rotation * state.PointerPose.Orientation.FromFlippedZQuatf() * Vector3.forward);
                }
            }
            if (useHands != hand.usesHandTracking || (useHands && wasTracked && !hand.handTracked))
            {
                // Source change/tracking loss: do not interpret release as feeding. Require release first.
                SetHovered(hand, null);
                if (holder == hand)
                {
                    var candy = heldCandy;
                    heldCandy = null;
                    holder = null;
                    if (candy != null && candy.interactable) StartCoroutine(ReturnHome(candy.transform, candyHome));
                }
                hand.waitForRelease = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (logInput) Debug.Log($"[Bite or Betray][VR] {hand.anchorName}: {(useHands ? "hand pinch" : "controller")}, wait for release");
#endif
            }
            hand.usesHandTracking = useHands;
            hand.pose = useHands ? (hand.node == XRNode.LeftHand ? rig.leftHandAnchor : rig.rightHandAnchor) : hand.controllerPose;
        }

        static bool ControllerTracked(Hand hand)
        {
            var touch = Touch(hand.node);
            if (OVRInput.IsControllerConnected(touch) && OVRInput.GetControllerPositionTracked(touch)
                && OVRInput.GetControllerOrientationTracked(touch)) return true;
            if (Pressed(InputSystemHand(hand.node), "isTracked")) return true;
            var device = InputDevices.GetDeviceAtXRNode(hand.node);
            return (device.characteristics & InputDeviceCharacteristics.Controller) != 0
                && (device.characteristics & InputDeviceCharacteristics.HandTracking) == 0
                && Button(device, CommonUsages.isTracked);
        }

        static bool HandTracked(Hand hand)
        {
            if (hand.pose == null || !hand.pose.gameObject.activeInHierarchy) return false;
            if (hand.usesHandTracking) return hand.handTracked;
            var touch = Touch(hand.node);
            if (OVRInput.GetControllerPositionTracked(touch) && OVRInput.GetControllerOrientationTracked(touch)) return true;
            var device = InputDevices.GetDeviceAtXRNode(hand.node);
            return device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
        }

        public bool TryGetTrackedHand(XRNode node, out Transform anchor, out float grip, out float index)
        {
            var hand = hands[node == XRNode.LeftHand ? 0 : 1];
            anchor = hand.pose;
            if (hand.usesHandTracking)
            {
                grip = index = hand.trigger ? 1 : 0;
                return active && HandTracked(hand);
            }
            grip = Mathf.Max(OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, Touch(node)), hand.grip ? 1 : 0);
            index = Mathf.Max(OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, Touch(node)), hand.trigger ? 1 : 0);
            return active && HandTracked(hand);
        }

        static ISDevice InputSystemHand(XRNode node) => node == XRNode.LeftHand ? ISXRController.leftHand : ISXRController.rightHand;

        static OVRInput.Controller Touch(XRNode node) => node == XRNode.LeftHand ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

        // Primary source is OVRInput: with Meta Camera Rig, controllers arrive via OVRPlugin (the same path that drives
        // hand anchors), and in this setup do not appear in Input System or InputDevices. Those two are fallbacks.
        void ReadButtons(Hand hand, out bool trigger, out bool grip, out bool secondary)
        {
            if (hand.usesHandTracking)
            {
                trigger = hand.handTracked && (hand.handState.Pinches & OVRPlugin.HandFingerPinch.Index) != 0;
                grip = secondary = false;
                return;
            }
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
            // Read native names and interpolation only when the input source changes.
            string state = $"OVRInput = {(connected ? "connected" : "none")}, Input System = {(device != null ? device.layout : "none")}, InputDevices = {(valid ? legacy.name : "none")}, pose = {(hand.pose != null ? hand.pose.name : "not found yet")}";
            Debug.Log($"[Bite or Betray][VR] Controller {hand.anchorName}: {state}");
#endif
        }

        void UpdateHand(Hand hand)
        {
            ReadButtons(hand, out bool trigger, out bool grip, out bool secondary);
            ProcessHandInput(hand, HandTracked(hand), trigger, grip, secondary);
        }

        // Separate device sampling from processing so the same path can be tested without a headset.
        void ProcessHandInput(Hand hand, bool tracked, bool trigger, bool grip, bool secondary)
        {
            if (hand.waitForRelease)
            {
                hand.trigger = trigger;
                hand.grip = grip;
                hand.secondary = secondary;
                if (tracked && !trigger && !grip && !secondary) hand.waitForRelease = false;
            }
            bool triggerPressed = trigger && !hand.trigger, triggerReleased = !trigger && hand.trigger;
            bool gripPressed = grip && !hand.grip, gripReleased = !grip && hand.grip;
            bool secondaryPressed = secondary && !hand.secondary;
            hand.trigger = trigger;
            hand.grip = grip;
            hand.secondary = secondary;

            if (!tracked)
            {
                if (hand.line != null) hand.line.enabled = false;
                SetHovered(hand, null);
                // Keep reading buttons during tracking loss so release is not missed. Hand direction is unknown,
                // so candy feeds nobody and returns home.
                if (holder == hand && (hand.grabbedWithGrip ? gripReleased : triggerReleased)) ReleaseCandy(hand);
                return;
            }

            // Pointer: while holding candy, only bots can be targeted (for feeding).
            var ray = hand.usesHandTracking ? hand.handRay : new Ray(hand.pose.position, hand.pose.forward);
            // Aim may be invalid near the face; a still-tracked hand pose can still eat.
            bool pointerValid = !hand.usesHandTracking || hand.pointerValid;
            RaycastHit hit = default;
            var target = pointerValid ? TableSelectable.Raycast(ray, rayLength, out hit, heldCandy, holder == hand) : null;
            if (director != null && director.SpectatorInputLocked && !director.IsVoiceSetting(target)) target = null;
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
                string what = target == null ? "no interactive target"
                    : $"{hit.collider.name} [{target.kind}, interactable={target.interactable}]";
                Debug.Log($"[Bite or Betray][VR] {hand.anchorName} {(triggerPressed ? "trigger" : gripPressed ? "grip" : "B/Y")} -> {what}");
            }
#endif
            hand.line.enabled = pointerValid;
            hand.line.SetPosition(0, ray.origin);
            hand.line.SetPosition(1, ray.origin + ray.direction * (target != null ? distance : Mathf.Min(distance, .6f)));
            var color = target != null ? rayHoverColor : rayColor;
            hand.line.startColor = hand.line.endColor = color;
            hand.material.SetColor("_BaseColor", color);

            if (secondaryPressed && director != null && !director.SpectatorInputLocked && TryAct(hand)) director.Cancel();
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
        /// At most one action (select, pick up/feed/eat candy, cancel) per frame. The losing hand gets
        /// a brief vibration and must press again.
        /// </summary>
        bool TryAct(Hand hand)
        {
            if (!actedThisFrame) return actedThisFrame = true;
            Haptic(hand, .1f, .04f);
            if (logInput) Debug.Log($"[Bite or Betray][VR] {hand.anchorName}: ignored, another hand already acted this frame");
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

        // ------------------------------------------------------------------ candy in hand

        public void ClearGameplayInteraction()
        {
            // The director has taken over the candy for animation; do not run ReturnHome and overwrite it.
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
            director?.NotifyCandyGrabbed();
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
            ProcessHeldCandy(holder != null && HandTracked(holder));
        }

        void ProcessHeldCandy(bool tracked)
        {
            if (holder == null) return;
            // The director takes over candy (e.g. timeout): release without returning it, including during tracking loss.
            if (heldCandy == null || !heldCandy.interactable)
            {
                heldCandy = null;
                holder = null;
                return;
            }
            if (!tracked) return;
            var candy = heldCandy.transform;
            Vector3 target = holder.pose.position + holder.pose.forward * .06f;
            candy.position = Vector3.Lerp(candy.position, target, 1 - Mathf.Exp(-18 * Time.deltaTime));

            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return;
            Vector3 mouth = head.position - head.up * .08f + head.forward * .05f;
            // If the other hand already acted this frame, defer eating to the next frame.
            if (actedThisFrame || Vector3.Distance(candy.position, mouth) > mouthDistance) return;
            actedThisFrame = true;
            candy.position = mouth;
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
                text = holder.hovered != null ? Strings.ReleaseTo.Format(holder.hovered.tooltip.ToLowerInvariant())
                    : Strings.HoldHint.Text;
                position = heldCandy.transform.position + Vector3.up * .06f;
            }
            else
                foreach (var hand in hands)
                    if (hand.hovered != null && !string.IsNullOrEmpty(hand.hovered.tooltip))
                    {
                        text = hand.hovered.tooltip + (hand.usesHandTracking ? Strings.PinchHint.Text : "");
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
            if (hand.usesHandTracking) return;
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

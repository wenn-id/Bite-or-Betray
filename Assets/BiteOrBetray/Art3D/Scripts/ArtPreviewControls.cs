using UnityEngine;
using UnityEngine.InputSystem;

namespace BiteOrBetray.Art3D
{
    public sealed class ArtPreviewControls : MonoBehaviour
    {
        public Transform artRoot;
        public float moveSpeed = 1.5f;
        public float lookSensitivity = 0.12f;
        int view;
        float yaw, pitch;
        int focus = -1;

        static readonly (Key key, CharacterReaction reaction)[] ReactionKeys = {
            (Key.Digit1, CharacterReaction.Eat), (Key.Digit2, CharacterReaction.Yummy), (Key.Digit3, CharacterReaction.Yuck),
            (Key.Digit4, CharacterReaction.Shocked), (Key.Digit5, CharacterReaction.Hurt), (Key.Digit6, CharacterReaction.Give),
            (Key.Digit7, CharacterReaction.Cheer), (Key.Digit8, CharacterReaction.Laugh), (Key.Digit9, CharacterReaction.Wave),
            (Key.Digit0, CharacterReaction.Nod)
        };

        void OnEnable()
        {
            // Project Setup Tool Meta (Fix All) mematikan kamera di luar Camera Rig. Tanpa headset, kamera
            // preview ini satu-satunya kamera di scene, jadi nyalakan lagi saat Play.
            if (!UnityEngine.XR.XRSettings.isDeviceActive)
            {
                if (TryGetComponent(out Camera camera)) camera.enabled = true;
                if (TryGetComponent(out AudioListener listener)) listener.enabled = true;
            }
            SetView(0);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            PreviewAnimations(keyboard);
            if (keyboard.escapeKey.wasPressedThisFrame || mouse.rightButton.wasReleasedThisFrame) Unlock();
            if (mouse.rightButton.wasPressedThisFrame)
            {
                // Kamera bisa dipindah script lain (misalnya ke kursi pemain), jadi ambil arah terkini.
                yaw = transform.eulerAngles.y;
                pitch = transform.eulerAngles.x > 180 ? transform.eulerAngles.x - 360 : transform.eulerAngles.x;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (Cursor.lockState != CursorLockMode.Locked || !mouse.rightButton.isPressed) return;
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -80, 80);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 direction = Vector3.zero;
            if (keyboard.wKey.isPressed) direction.z++;
            if (keyboard.sKey.isPressed) direction.z--;
            if (keyboard.dKey.isPressed) direction.x++;
            if (keyboard.aKey.isPressed) direction.x--;
            if (keyboard.eKey.isPressed) direction.y++;
            if (keyboard.qKey.isPressed) direction.y--;
            Vector3 position = transform.position + transform.TransformDirection(direction.normalized) * moveSpeed * Time.deltaTime;
            transform.position = new Vector3(Mathf.Clamp(position.x, -3.5f, 3.5f),
                Mathf.Clamp(position.y, .4f, 3.2f), Mathf.Clamp(position.z, -3.3f, 3.3f));
        }

        void PreviewAnimations(Keyboard keyboard)
        {
            var characters = CharacterAnimator.All;
            if (keyboard.tabKey.wasPressedThisFrame) focus = focus + 1 >= characters.Count ? -1 : focus + 1;
            if (focus >= characters.Count) focus = -1;
            foreach (var (key, reaction) in ReactionKeys)
                if (keyboard[key].wasPressedThisFrame)
                    ForFocused(c => c.Play(reaction, reaction == CharacterReaction.Give ? transform : null));
            if (keyboard.tKey.wasPressedThisFrame) ForFocused(c => c.thinking = !c.thinking);
            if (keyboard.pKey.wasPressedThisFrame) ForFocused(c => c.paralyzed = !c.paralyzed);
            if (keyboard.kKey.wasPressedThisFrame) ForFocused(c => c.defeated = !c.defeated);
        }

        void ForFocused(System.Action<CharacterAnimator> action)
        {
            var characters = CharacterAnimator.All;
            if (focus >= 0) action(characters[focus]);
            else foreach (var c in characters) action(c);
        }

        void SetView(int next)
        {
            view = next;
            Vector3 position = view == 0 ? new Vector3(2.7f, 2.2f, -3.2f)
                : view == 1 ? new Vector3(0, 1.15f, -1.2f) : new Vector3(0, 1.65f, -.9f);
            Vector3 target = view == 0 ? new Vector3(0, 1.1f, .7f)
                : view == 1 ? new Vector3(0, 1.03f, .3f) : new Vector3(0, .77f, 0);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            yaw = transform.eulerAngles.y;
            pitch = transform.eulerAngles.x;
            if (pitch > 180) pitch -= 360;
        }

        void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
            {
                SetView((view + 1) % 3);
                Event.current.Use();
            }
            if (Event.current.type != EventType.Repaint) return;
            var characters = CharacterAnimator.All;
            string target = focus >= 0 && focus < characters.Count ? characters[focus].name : "Semua karakter";
            GUI.Label(new Rect(10, 10, 900, 60),
                $"Animasi: <b>{target}</b>  (Tab ganti)\n" +
                "1 Eat  2 Yummy  3 Yuck  4 Shocked  5 Hurt  6 Give  7 Cheer  8 Laugh  9 Wave  0 Nod  |  T Mikir  P Lumpuh  K Kalah",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });
        }

        static void Unlock()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnDisable() => Unlock();
        void OnApplicationFocus(bool hasFocus) { if (!hasFocus) Unlock(); }
    }
}

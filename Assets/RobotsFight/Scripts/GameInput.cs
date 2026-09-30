#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
#define RF_NEW_INPUT
#endif
using UnityEngine;
#if RF_NEW_INPUT
using UnityEngine.InputSystem;
#endif

namespace RobotsFight
{
    /// <summary>Обёртка ввода: работает и со старым Input Manager, и с новым Input System.</summary>
    public static class GameInput
    {
#if RF_NEW_INPUT
        static Keyboard Kb => Keyboard.current;
        static Mouse Ms => Mouse.current;
        static bool K(Key k) => Kb != null && Kb[k].isPressed;
        static bool KD(Key k) => Kb != null && Kb[k].wasPressedThisFrame;

        public static Vector2 Move => new(
            (K(Key.D) || K(Key.RightArrow) ? 1 : 0) - (K(Key.A) || K(Key.LeftArrow) ? 1 : 0),
            (K(Key.W) || K(Key.UpArrow) ? 1 : 0) - (K(Key.S) || K(Key.DownArrow) ? 1 : 0));
        public static Vector2 Look => Ms != null ? Ms.delta.ReadValue() * 0.05f : Vector2.zero;
        public static bool Fire => Ms != null && Ms.leftButton.isPressed;
        public static bool Zoom => Ms != null && Ms.rightButton.isPressed;
        public static bool Reload => KD(Key.R);
        public static bool Ability => KD(Key.Q);
        public static bool Jump => KD(Key.Space);
        public static bool Sprint => K(Key.LeftShift) || K(Key.RightShift);
        public static bool Interact => K(Key.E);
        public static bool Scoreboard => K(Key.Tab);
        public static bool Pause => KD(Key.Escape);
        public static bool Confirm => KD(Key.Enter);
        public static bool Number(int n) => KD(Key.Digit1 + (n - 1));
        public static bool Melee => KD(Key.F) || KD(Key.V);
        public static bool Grenade => KD(Key.G);
        public static bool Mute => KD(Key.M);
#else
        static bool K(KeyCode k) => Input.GetKey(k);
        static bool KD(KeyCode k) => Input.GetKeyDown(k);

        public static Vector2 Move => new(
            (K(KeyCode.D) || K(KeyCode.RightArrow) ? 1 : 0) - (K(KeyCode.A) || K(KeyCode.LeftArrow) ? 1 : 0),
            (K(KeyCode.W) || K(KeyCode.UpArrow) ? 1 : 0) - (K(KeyCode.S) || K(KeyCode.DownArrow) ? 1 : 0));
        public static Vector2 Look => new(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        public static bool Fire => Input.GetMouseButton(0);
        public static bool Zoom => Input.GetMouseButton(1);
        public static bool Reload => KD(KeyCode.R);
        public static bool Ability => KD(KeyCode.Q);
        public static bool Jump => KD(KeyCode.Space);
        public static bool Sprint => K(KeyCode.LeftShift) || K(KeyCode.RightShift);
        public static bool Interact => K(KeyCode.E);
        public static bool Scoreboard => K(KeyCode.Tab);
        public static bool Pause => KD(KeyCode.Escape);
        public static bool Confirm => KD(KeyCode.Return) || KD(KeyCode.KeypadEnter);
        public static bool Number(int n) => KD(KeyCode.Alpha1 + (n - 1));
        public static bool Melee => KD(KeyCode.F) || KD(KeyCode.V);
        public static bool Grenade => KD(KeyCode.G);
        public static bool Mute => KD(KeyCode.M);
#endif
    }
}

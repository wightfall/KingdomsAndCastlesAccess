using System.Collections.Generic;
using UnityEngine;

namespace KCAccess
{
    /// <summary>
    /// Keyboard input for the mod. Wraps Unity's Input so keys can also be injected
    /// (debug command file / automated testing) and so the mod can mark keys as consumed.
    /// </summary>
    internal static class KInput
    {
        private struct Injected
        {
            public KeyCode Key;
            public bool Shift, Ctrl, Alt;
        }

        private static readonly List<Injected> pending = new List<Injected>();
        private static Injected? current;
        private static int currentFrame = -1;
        private static readonly HashSet<KeyCode> consumed = new HashSet<KeyCode>();
        private static int consumedFrame = -1;

        /// <summary>Queue a key press to be seen as "pressed" on a later frame.</summary>
        internal static void Inject(KeyCode key, bool shift = false, bool ctrl = false, bool alt = false)
        {
            pending.Add(new Injected { Key = key, Shift = shift, Ctrl = ctrl, Alt = alt });
        }

        /// <summary>Call once per frame before reading keys.</summary>
        internal static void BeginFrame()
        {
            if (currentFrame == Time.frameCount) return;
            currentFrame = Time.frameCount;
            current = null;
            if (pending.Count > 0)
            {
                current = pending[0];
                pending.RemoveAt(0);
            }
        }

        internal static bool Down(KeyCode key)
        {
            if (current.HasValue && current.Value.Key == key) return true;
            return Input.GetKeyDown(key);
        }

        internal static bool Held(KeyCode key) => Input.GetKey(key);

        internal static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || (current.HasValue && current.Value.Shift);

        internal static bool Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || (current.HasValue && current.Value.Ctrl);

        internal static bool Alt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || (current.HasValue && current.Value.Alt);

        internal static bool NoMods => !Shift && !Ctrl && !Alt;

        /// <summary>Pressed with no modifiers.</summary>
        internal static bool Plain(KeyCode key) => Down(key) && NoMods;

        internal static bool WithShift(KeyCode key) => Down(key) && Shift && !Ctrl && !Alt;

        internal static bool WithCtrl(KeyCode key) => Down(key) && Ctrl && !Shift && !Alt;

        /// <summary>Mark a key as used by the mod this frame so the game ignores it (see InputPatches).</summary>
        internal static void Consume(KeyCode key)
        {
            if (consumedFrame != Time.frameCount)
            {
                consumed.Clear();
                consumedFrame = Time.frameCount;
            }
            consumed.Add(key);
        }

        internal static bool IsConsumed(KeyCode key) => consumedFrame == Time.frameCount && consumed.Contains(key);

        /// <summary>Any letter key A-Z pressed this frame (for type-ahead), without Ctrl/Alt.</summary>
        internal static char? LetterDown()
        {
            if (Ctrl || Alt) return null;
            if (current.HasValue && current.Value.Key >= KeyCode.A && current.Value.Key <= KeyCode.Z) return (char)('a' + (current.Value.Key - KeyCode.A));
            for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++)
            {
                if (Input.GetKeyDown(k)) return (char)('a' + (k - KeyCode.A));
            }
            return null;
        }
    }
}

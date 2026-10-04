using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace KCAccess
{
    /// <summary>
    /// Shift / Ctrl / Alt state. Unity keeps its own key state from window messages and can miss a key release
    /// (focus changes, NVDA Remote, Alt+Tab, some Windows 11 machines); a modifier stuck "held" then blocks every
    /// plain key: Escape, Space, 1 2 3 and all mod letters. The operating system's live key state is checked too,
    /// and a modifier only counts as held when both agree.
    /// </summary>
    internal static class Modifiers
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint thread);

        /// <summary>Active keyboard layout as a language id ("0409" = US English, "041E" = Thai), or "unknown".</summary>
        internal static string KeyboardLayout()
        {
            if (!osAvailable) return "unknown";
            try
            {
                return ((long)GetKeyboardLayout(0) & 0xFFFF).ToString("X4");
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        private const int VkShift = 0x10, VkControl = 0x11, VkMenu = 0x12;

        private static bool osAvailable = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
        private static float lastStuckLog = -100f;

        private static bool Os(int vk, bool unity)
        {
            if (!osAvailable) return unity;
            try
            {
                return (GetAsyncKeyState(vk) & 0x8000) != 0;
            }
            catch (Exception)
            {
                osAvailable = false;
                return unity;
            }
        }

        private static bool Check(string name, int vk, bool unity)
        {
            if (!unity) return false;
            bool os = Os(vk, unity);
            if (!os && Time.unscaledTime - lastStuckLog > 10f)
            {
                lastStuckLog = Time.unscaledTime;
                Plugin.Log.LogWarning("Unity reports " + name + " held but Windows does not; ignoring the stuck key.");
            }
            return os;
        }

        internal static bool UnityShift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        internal static bool UnityCtrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        internal static bool UnityAlt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        internal static bool Shift => Check("Shift", VkShift, UnityShift);
        internal static bool Ctrl => Check("Control", VkControl, UnityCtrl);
        internal static bool Alt => Check("Alt", VkMenu, UnityAlt);

        /// <summary>One line for diagnostics: what Unity and Windows each think is held.</summary>
        internal static string Describe() =>
            "Unity: shift=" + UnityShift + " ctrl=" + UnityCtrl + " alt=" + UnityAlt +
            "; Windows: shift=" + Os(VkShift, false) + " ctrl=" + Os(VkControl, false) + " alt=" + Os(VkMenu, false);
    }
}

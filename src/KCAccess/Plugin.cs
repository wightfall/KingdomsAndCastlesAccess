using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KCAccess.Core;
using KCAccess.Speech;
using UnityEngine;

namespace KCAccess
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "kcaccess.screenreader";
        public const string Name = "KCAccess";
        public const string Version = "1.4.3";

        internal static Plugin Instance;

        /// <summary>True once Awake finished. (The plugin's own GameObject does not survive in this game.)</summary>
        internal static bool Loaded;

        internal static PrismSpeech Prism;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> CfgCues;
        internal static ConfigEntry<float> CfgCueVolume;
        internal static ConfigEntry<bool> CfgCoordinates;
        internal static ConfigEntry<bool> CfgCameraFollow;
        internal static ConfigEntry<bool> CfgAnnounceLog;
        internal static ConfigEntry<bool> CfgLogSpeech;
        internal static ConfigEntry<bool> CfgVerboseCells;
        internal static ConfigEntry<bool> CfgDebugCommands;
        internal static ConfigEntry<bool> CfgLogKeys;
        internal static ConfigEntry<bool> CfgKeyFallback;

        private PrismSpeech prism;
        private Harmony harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            CfgCues = Config.Bind("Audio", "SoundCues", true, "Play sound cues for navigation, placement and alerts.");
            CfgCueVolume = Config.Bind("Audio", "CueVolume", 0.5f, new ConfigDescription("Volume of the sound cues.", new AcceptableValueRange<float>(0f, 1f)));
            CfgCoordinates = Config.Bind("Map", "SpeakCoordinates", false, "Speak the X, Z coordinates of the cursor after each cell description.");
            CfgVerboseCells = Config.Bind("Map", "VerboseCells", false, "Speak fertility and road coverage for every cell while moving (otherwise only with I).");
            CfgCameraFollow = Config.Bind("Map", "CameraFollowsCursor", true, "Move the camera to keep the keyboard cursor in view.");
            CfgAnnounceLog = Config.Bind("Speech", "AnnounceNotifications", true, "Automatically speak kingdom notifications (raids, fires, shortages ...).");
            CfgDebugCommands = Config.Bind("Debug", "CommandFile", false, "Developer option: read test commands from BepInEx/kcaccess_commands.txt.");
            CfgKeyFallback = Config.Bind("Keyboard", "WindowsKeyFallback", true, "When Windows reports a key press the game did not receive (seen on some Windows 11 machines, with NVDA Remote or Steam Input), deliver it anyway. Turn off only if keys start acting twice.");
            CfgLogKeys = Config.Bind("Debug", "KeyLog", true, "Write every key press (key name only), with the modifier state seen by the game and by Windows, to the BepInEx log (for keyboard bug reports).");
            CfgLogSpeech = Config.Bind("Speech", "LogSpeech", true, "Write everything spoken to the BepInEx log (useful for bug reports).");

            string pluginDir = Path.GetDirectoryName(Info.Location);
            ISpeechBackend backend;
            prism = new PrismSpeech();
            Prism = prism;
            if (prism.Initialize(pluginDir))
            {
                backend = prism;
                Log.LogInfo("Prism " + prism.Version + " ready, backend: " + prism.Name);
            }
            else
            {
                backend = new LogOnlySpeech();
                Log.LogError("Prism could not start (" + prism.LastError + "). Make sure prism.dll is in " + pluginDir);
            }

            A.Init(new Announcer(backend, () => Time.realtimeSinceStartup), prism);
            A.Announcer.Spoken += text =>
            {
                if (CfgLogSpeech.Value) Log.LogInfo("[speech] " + text);
            };

            harmony = new Harmony(Guid);
            try
            {
                harmony.PatchAll(typeof(Plugin).Assembly);
            }
            catch (Exception e)
            {
                Log.LogError("Harmony patching failed: " + e);
            }

            var root = new GameObject("KCAccess");
            DontDestroyOnLoad(root);
            root.hideFlags = HideFlags.HideAndDontSave;
            root.AddComponent<AudioCues>();
            root.AddComponent<AccessController>();
            Loaded = true;
            Log.LogInfo(Name + " " + Version + " loaded. OS " + Environment.OSVersion + ", keyboard layout " + Modifiers.KeyboardLayout() + ".");
        }

        internal static void Shutdown()
        {
            Prism?.Dispose();
            Prism = null;
        }
    }
}

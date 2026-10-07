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
        public const string Version = "1.11.0";

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

        /// <summary>The config file (the plugin object itself is destroyed by the game after start-up).</summary>
        internal static ConfigFile ConfigRef;
        internal static ConfigEntry<string> CfgBindings;
        internal static ConfigEntry<bool> CfgAnnounceSeasons;
        internal static ConfigEntry<int> CfgWalkSpeed;
        internal static ConfigEntry<bool> CfgHints;
        internal static ConfigEntry<bool> CfgPositions;
        internal static ConfigEntry<bool> CfgController;
        internal static ConfigEntry<bool> CfgTwitchCountdown;
        internal static ConfigEntry<bool> CfgTwitchChat;
        internal static ConfigEntry<bool> CfgCaptions;
        internal static ConfigEntry<bool> CfgStreamFile;
        internal static ConfigEntry<string> CfgLanguage;

        /// <summary>The mod's key bindings (rebindable in the mod settings, Ctrl+Shift+O).</summary>
        internal static readonly KCAccess.Core.Bindings Keys = new KCAccess.Core.Bindings();

        private PrismSpeech prism;
        private Harmony harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ConfigRef = Config;
            CfgCues = Config.Bind("Audio", "SoundCues", true, "Play sound cues for navigation, placement and alerts.");
            CfgCueVolume = Config.Bind("Audio", "CueVolume", 0.5f, new ConfigDescription("Volume of the sound cues.", new AcceptableValueRange<float>(0f, 1f)));
            CfgCoordinates = Config.Bind("Map", "SpeakCoordinates", false, "Speak the X, Z coordinates of the cursor after each cell description.");
            CfgVerboseCells = Config.Bind("Map", "VerboseCells", false, "Speak fertility and road coverage for every cell while moving (otherwise only with I).");
            CfgCameraFollow = Config.Bind("Map", "CameraFollowsCursor", true, "Move the camera to keep the keyboard cursor in view.");
            CfgAnnounceLog = Config.Bind("Speech", "AnnounceNotifications", true, "Automatically speak kingdom notifications (raids, fires, shortages ...).");
            CfgDebugCommands = Config.Bind("Debug", "CommandFile", false, "Developer option: read test commands from BepInEx/kcaccess_commands.txt.");
            CfgKeyFallback = Config.Bind("Keyboard", "WindowsKeyFallback", true, "When Windows reports a key press the game did not receive (seen on some Windows 11 machines, with NVDA Remote or Steam Input), deliver it anyway. Turn off only if keys start acting twice.");
            CfgBindings = Config.Bind("Keyboard", "Bindings", "", "Changed mod keys as Action=Key pairs separated by semicolons, e.g. Survey=Y;Settings=Ctrl+Shift+P. Empty means all defaults. Easier to change in the game: Ctrl+Shift+O.");
            CfgAnnounceSeasons = Config.Bind("Speech", "AnnounceSeasons", true, "Speak the start of every summer, winter and new year.");
            CfgWalkSpeed = Config.Bind("Map", "WalkSpeed", 2, new ConfigDescription("Auto-walk speed: 1 slow, 2 normal, 3 fast.", new AcceptableValueRange<int>(1, 3)));
            CfgHints = Config.Bind("Speech", "Hints", true, "Speak short hints for new players (for example how to use a target after choosing it).");
            CfgPositions = Config.Bind("Speech", "SayPositions", false, "In every menu, list and panel, say the position after each item, for example \"New, 3 of 19\".");
            CfgController = Config.Bind("Keyboard", "ControllerSupport", true, "Play with a gamepad (Xbox, PlayStation and most others): buttons do the same as the mod's keys. Turn off to use the game's own controller mode instead.");
            CfgTwitchCountdown = Config.Bind("Streaming", "AnnounceTwitchCountdown", true, "With the game's Twitch chat voting connected: say once when 10 seconds are left in a vote, with the leading option.");
            CfgTwitchChat = Config.Bind("Streaming", "ReadTwitchChat", false, "With the game's Twitch chat voting connected: read Twitch chat messages aloud (vote messages are skipped, busy chat is summarised).");
            CfgCaptions = Config.Bind("Streaming", "SpeechCaptions", false, "Show the last spoken lines as captions at the bottom of the screen, for people watching your stream.");
            CfgStreamFile = Config.Bind("Streaming", "StatusFile", false, "Every 2 seconds write BepInEx/kcaccess_stream.txt (kingdom, year, population, gold, last notification) for an OBS text source.");
            Keys.Load(CfgBindings.Value);
            KeyHelp.KeyNameOf = Keys.Spoken; // help texts name the player's own keys
            CfgLogKeys = Config.Bind("Debug", "KeyLog", true, "Write every key press (key name only), with the modifier state seen by the game and by Windows, to the BepInEx log (for keyboard bug reports).");
            CfgLogSpeech = Config.Bind("Speech", "LogSpeech", true, "Write everything spoken to the BepInEx log (useful for bug reports).");
            CfgLanguage = Config.Bind("Language", "Language", "auto", "Language the mod speaks: auto follows the game's language (English when there is no file for it), en is English, any other value is the name of a file in BepInEx/plugins/KCAccess/Languages without .txt, for example th. Easier to change in the game: Ctrl+Shift+O.");

            string pluginDir = Path.GetDirectoryName(Info.Location);
            Loc.Use(null);
            ModLanguage.Init(pluginDir);
            prism = new PrismSpeech();
            Prism = prism;
            if (prism.Initialize(pluginDir)) Log.LogInfo("Prism " + prism.Version + " ready, backend: " + prism.Name);
            else Log.LogError("Prism could not start (" + prism.LastError + "). Make sure prism.dll is in " + pluginDir);
            // Speaks through Prism as soon as it has a backend (also one found later by re-detection), else logs only.
            ISpeechBackend backend = new SwitchingSpeech(prism);

            A.Init(new Announcer(backend, () => Time.realtimeSinceStartup), prism);
            A.Announcer.Spoken += text =>
            {
                if (CfgLogSpeech.Value) Log.LogInfo("[speech] " + text);
                Game.CaptionOverlay.OnSpoken(text);
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
            root.AddComponent<Game.CaptionOverlay>();
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

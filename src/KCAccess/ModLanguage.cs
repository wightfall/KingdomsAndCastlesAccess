using System;
using System.Collections.Generic;
using System.IO;
using KCAccess.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KCAccess
{
    /// <summary>
    /// The language the mod speaks: the Language setting ("auto" follows the game's language), the files in
    /// BepInEx/plugins/KCAccess/Languages, hot reload of the active file and the extra entries in the game's language
    /// list for languages only the mod speaks (Thai and languages players add).
    /// </summary>
    internal static class ModLanguage
    {
        private const string ButtonPrefix = "KCAccessLang_";

        private static LanguageFiles files;
        private static List<LanguageInfo> available = new List<LanguageInfo>();
        private static string activeCode;
        private static DateTime activeStamp;
        private static string appliedSetting;
        private static string appliedGameLanguage;
        private static float nextFileCheck;
        private static float nextGameCheck;
        private static bool gameChecked; // the game's language is first read on the first frame, not during plugin start-up

        /// <summary>File code of the active language, or null for English.</summary>
        internal static string ActiveCode => activeCode;

        /// <summary>Name of the active language ("ไทย (Thai)", "English").</summary>
        internal static string ActiveName => NameOf(activeCode);

        /// <summary>The active language is one the game does not have (the game keeps its own language).</summary>
        internal static bool ActiveModOnly
        {
            get
            {
                var info = Find(activeCode);
                return info != null && info.ModOnly;
            }
        }

        internal static IList<LanguageInfo> Available => available;

        internal static string Setting => Plugin.CfgLanguage != null ? (Plugin.CfgLanguage.Value ?? LanguageFiles.Auto).Trim() : LanguageFiles.Auto;

        internal static void Init(string pluginDir)
        {
            files = new LanguageFiles(Path.Combine(pluginDir, "Languages"));
            try
            {
                files.Sync(msg => Plugin.Log.LogInfo(msg));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Language files could not be updated: " + e.Message);
            }
            Refresh();
            Apply(announce: false, gameLanguage: null);
        }

        /// <summary>Reads the list of language files again.</summary>
        internal static void Refresh()
        {
            try
            {
                available = files != null ? files.Available() : new List<LanguageInfo>();
            }
            catch (Exception e)
            {
                available = new List<LanguageInfo>();
                Plugin.Log.LogWarning("Language files could not be listed: " + e.Message);
            }
        }

        internal static LanguageInfo Find(string code)
        {
            if (code == null) return null;
            foreach (var l in available) if (string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) return l;
            return null;
        }

        internal static string NameOf(string code)
        {
            if (code == null || string.Equals(code, LanguageFiles.English, StringComparison.OrdinalIgnoreCase)) return "English";
            var l = Find(code);
            return l != null ? l.Name : code;
        }

        internal static string GameLanguage()
        {
            try
            {
                return I2.Loc.LocalizationManager.CurrentLanguage;
            }
            catch (Exception)
            {
                return null; // localization not ready yet
            }
        }

        /// <summary>Loads the language the setting asks for. Returns true when the language changed.</summary>
        internal static bool Apply(bool announce) => Apply(announce, GameLanguage());

        private static bool Apply(bool announce, string gameLanguage)
        {
            string setting = Setting;
            string game = gameLanguage;
            appliedSetting = setting;
            appliedGameLanguage = game;
            string code = LanguageFiles.Resolve(setting, game, available);
            bool changed = !string.Equals(code, activeCode, StringComparison.OrdinalIgnoreCase);
            Load(code);
            if (changed)
            {
                Plugin.Log.LogInfo("Mod language: " + NameOf(code) + " (setting " + setting + ", game language " + (game ?? "unknown") + ")");
                if (announce) A.Say(Loc.F("Mod language: {0}", ActiveName), force: true);
            }
            return changed;
        }

        private static void Load(string code)
        {
            activeCode = code;
            if (code == null || files == null)
            {
                Loc.Use(null);
                return;
            }
            try
            {
                string path = files.UserPath(code);
                activeStamp = File.GetLastWriteTimeUtc(path);
                var table = files.Load(code);
                table.Warn = msg => Plugin.Log.LogWarning("[" + code + ".txt] " + msg);
                Loc.Use(table);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Language file " + code + ".txt could not be read, using English: " + e.Message);
                Loc.Use(null);
            }
        }

        /// <summary>Chooses a language ("auto", "en" or a file code), saves it and says so in the new language.</summary>
        internal static void Choose(string setting)
        {
            if (Plugin.CfgLanguage == null) return;
            Plugin.CfgLanguage.Value = setting;
            Refresh();
            Apply(announce: false);
            A.Cue(Cue.Placed);
            A.Say(Loc.F("Mod language: {0}", ActiveName), force: true);
        }

        /// <summary>Called every frame: follows the setting and the game's language, reloads the active file when it was saved.</summary>
        internal static void Tick()
        {
            if (files == null) return;
            float now = Time.unscaledTime;
            if (now >= nextGameCheck)
            {
                nextGameCheck = now + 0.5f;
                string setting = Setting;
                string game = GameLanguage();
                if (!gameChecked)
                {
                    gameChecked = true;
                    Apply(announce: false, gameLanguage: game);
                }
                else if (setting != appliedSetting || (game != appliedGameLanguage && string.Equals(setting, LanguageFiles.Auto, StringComparison.OrdinalIgnoreCase)))
                    Apply(announce: true);
                else if (game != appliedGameLanguage) appliedGameLanguage = game;
            }
            if (activeCode == null || now < nextFileCheck) return;
            nextFileCheck = now + 2f;
            try
            {
                var stamp = File.GetLastWriteTimeUtc(files.UserPath(activeCode));
                if (stamp == activeStamp) return;
                Load(activeCode);
                Refresh();
                Plugin.Log.LogInfo("Language file reloaded: " + activeCode + ".txt");
                A.Say(Loc.T("Language file reloaded"), force: true);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Language file check failed: " + e.Message);
            }
        }

        internal static string ButtonName(string code) => ButtonPrefix + code;

        /// <summary>
        /// The game's language list (ChangeLanguage.dropdownList) only has the game's languages. Adds one button per
        /// language only the mod speaks; it sets the mod's language and leaves the game's language alone.
        /// </summary>
        internal static void AddModOnlyButtons(ChangeLanguage picker)
        {
            try
            {
                if (picker == null || picker.dropdownList == null || picker.englishButton == null) return;
                Refresh();
                var template = picker.englishButton;
                var parent = template.transform.parent;
                foreach (var info in available)
                {
                    if (!info.ModOnly) continue;
                    string name = ButtonName(info.Code);
                    if (parent.Find(name) != null) continue;
                    var go = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
                    go.name = name;
                    // The copy keeps the original's text components; I2 would put "English" back on them.
                    foreach (var l in go.GetComponentsInChildren<I2.Loc.Localize>(true)) UnityEngine.Object.Destroy(l);
                    string label = ButtonLabel(info);
                    // The game's entries have two lines (native name, English name): the copy says it all on the first.
                    bool first = true;
                    foreach (var t in go.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    {
                        t.text = first ? label : string.Empty;
                        first = false;
                    }
                    foreach (var t in go.GetComponentsInChildren<Text>(true)) t.text = label;
                    var button = go.GetComponent<Button>();
                    button.onClick = new Button.ButtonClickedEvent(); // drop the copied EnglishMode listener
                    string code = info.Code;
                    var list = picker.dropdownList;
                    var pickerGo = picker.gameObject;
                    button.onClick.AddListener(() =>
                    {
                        list.SetActive(false);
                        Choose(code);
                        if (AccessController.Inst != null) AccessController.Inst.Nav.RequestFocus(g => g == pickerGo);
                    });
                    go.transform.SetAsLastSibling();
                    go.SetActive(true);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not add the mod's languages to the language list: " + e.Message);
            }
        }

        /// <summary>"ไทย (Thai, screen reader only)".</summary>
        internal static string ButtonLabel(LanguageInfo info)
        {
            string extra = Loc.T("screen reader only");
            string name = info.Name ?? info.Code;
            return name.EndsWith(")") ? name.Substring(0, name.Length - 1) + ", " + extra + ")" : name + " (" + extra + ")";
        }
    }
}

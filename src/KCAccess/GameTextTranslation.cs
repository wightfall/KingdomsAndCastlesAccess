using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess
{
    /// <summary>
    /// The game's own texts in the mod's language. The game asks I2 Localization for every menu, window, tooltip,
    /// building, event and dialogue text by term; when Languages/game/&lt;code&gt;.txt of the mod's language has a
    /// translation for the term, it replaces the game's text on the screen and for the screen reader. Terms without
    /// one keep the game's own language. The folder also gets template.txt and the player's copies of the shipped
    /// files, written from the installed game with each English text as a comment (<see cref="GameTexts.Build"/>).
    /// </summary>
    internal static class GameTextTranslation
    {
        private static string dir;
        private static List<GameTerm> terms;
        private static Dictionary<string, string> english;
        private static volatile Dictionary<string, string> active;
        private static string activeCode;
        private static DateTime activeStamp;
        private static float nextCheck;
        private static bool pending;

        /// <summary>How many game texts the active file translates.</summary>
        internal static int Count => active != null ? active.Count : 0;

        /// <summary>Is a game text file in use?</summary>
        internal static bool Active => active != null;

        internal static void Init(string pluginDir)
        {
            dir = Path.Combine(pluginDir, "Languages", GameTexts.Folder);
        }

        internal static bool TryGet(string term, out string text)
        {
            text = null;
            var a = active;
            return a != null && term != null && a.TryGetValue(term, out text);
        }

        /// <summary>The mod's language changed (or the setting): use that language's game text file, if any.</summary>
        internal static void Use(string code)
        {
            activeCode = code;
            pending = true;
            Apply();
        }

        /// <summary>Called every frame: finishes a pending load once the game's localization is ready, reloads a saved file.</summary>
        internal static void Tick()
        {
            float now = Time.unscaledTime;
            if (now < nextCheck) return;
            nextCheck = now + (pending ? 0.5f : 2f);
            if (pending)
            {
                Apply();
                return;
            }
            // Hot reload of the active file; nothing to watch while the setting is off.
            if (activeCode == null || Plugin.CfgGameTexts == null || !Plugin.CfgGameTexts.Value) return;
            try
            {
                string path = PathOf(activeCode);
                var stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default(DateTime);
                if (stamp == activeStamp) return;
                Load();
                Plugin.Log.LogInfo("Game text file reloaded: game/" + activeCode + ".txt");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Game text file check failed: " + e.Message);
            }
        }

        private static string PathOf(string code) => code == null ? null : Path.Combine(dir, code + ".txt");

        private static void Apply()
        {
            if (dir == null) return;
            // The template is written for every language, English too, once the game's localization is ready.
            if (!EnsureTerms()) return; // not ready yet: Tick tries again
            pending = false;
            bool wanted = activeCode != null && Plugin.CfgGameTexts != null && Plugin.CfgGameTexts.Value;
            if (wanted)
            {
                Load();
                return;
            }
            if (active != null)
            {
                active = null;
                activeStamp = default(DateTime);
                Relocalize();
            }
        }

        private static void Load()
        {
            string path = PathOf(activeCode);
            Dictionary<string, string> next = null;
            activeStamp = default(DateTime);
            if (path != null && File.Exists(path) && english != null)
            {
                try
                {
                    activeStamp = File.GetLastWriteTimeUtc(path);
                    var table = LocTable.Parse(File.ReadAllText(path, Encoding.UTF8));
                    string code = activeCode;
                    next = GameTexts.Usable(table, english, msg => Plugin.Log.LogWarning("[game/" + code + ".txt] " + msg));
                    Plugin.Log.LogInfo("Game texts in " + code + ": " + next.Count + " of " + english.Count);
                    if (next.Count == 0) next = null;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Game text file game/" + activeCode + ".txt could not be read: " + e.Message);
                    next = null;
                }
            }
            bool changed = next != null || active != null;
            active = next;
            if (changed) Relocalize();
        }

        /// <summary>Texts already on screen are drawn again with the new translations.</summary>
        private static void Relocalize()
        {
            try
            {
                I2.Loc.LocalizationManager.LocalizeAll(true);
            }
            catch (Exception)
            {
                // the game's localization is not ready yet; texts appearing later are translated anyway
            }
        }

        /// <summary>Reads every term of the game once, then writes the template and the player's files.</summary>
        private static bool EnsureTerms()
        {
            if (terms != null) return true;
            List<I2.Loc.LanguageSourceData> sources;
            try
            {
                I2.Loc.LocalizationManager.InitializeIfNeeded();
                sources = I2.Loc.LocalizationManager.Sources;
            }
            catch (Exception)
            {
                return false;
            }
            if (sources == null || sources.Count == 0) return false;
            var list = new List<GameTerm>();
            var en = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var src in sources)
            {
                if (src == null || src.mTerms == null) continue;
                int idx = src.GetLanguageIndex("English", true, false);
                if (idx < 0) continue;
                foreach (var td in src.mTerms)
                {
                    if (td == null || string.IsNullOrEmpty(td.Term) || td.Languages == null || idx >= td.Languages.Length) continue;
                    string text = td.Languages[idx];
                    if (string.IsNullOrEmpty(text) || en.ContainsKey(td.Term)) continue;
                    en[td.Term] = text;
                    list.Add(new GameTerm { Term = td.Term, English = text });
                }
            }
            if (list.Count == 0) return false;
            terms = list;
            english = en;
            try
            {
                Sync();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Game text files could not be updated: " + e.Message);
            }
            return true;
        }

        /// <summary>
        /// template.txt for new languages; every shipped file (default/&lt;code&gt;.txt) merged into the player's copy;
        /// files the player made themselves get the game's new texts and the English comments.
        /// </summary>
        private static void Sync()
        {
            Directory.CreateDirectory(dir);
            var header = GameTexts.Header("(the name of your language)");
            header.Add("# Copy this file to <code>.txt, the same code as your file in the Languages folder, and translate what you like.");
            WriteIfChanged(Path.Combine(dir, LanguageFiles.TemplateName), GameTexts.Build(terms, header, null, null, null));
            string defaults = Path.Combine(dir, "default");
            string bases = Path.Combine(defaults, ".base");
            var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(defaults))
            {
                foreach (var shippedPath in Directory.GetFiles(defaults, "*.txt"))
                {
                    string code = Path.GetFileNameWithoutExtension(shippedPath);
                    done.Add(code);
                    string shippedText = File.ReadAllText(shippedPath, Encoding.UTF8);
                    var shipped = LocTable.Parse(shippedText);
                    string userPath = PathOf(code);
                    string basePath = Path.Combine(bases, code + ".txt");
                    var user = File.Exists(userPath) ? LocTable.Parse(File.ReadAllText(userPath, Encoding.UTF8)) : null;
                    var previous = File.Exists(basePath) ? LocTable.Parse(File.ReadAllText(basePath, Encoding.UTF8)) : null;
                    string name = user != null && user.LanguageName.Length > 0 ? user.LanguageName : shipped.LanguageName;
                    if (WriteIfChanged(userPath, GameTexts.Build(terms, GameTexts.Header(name), user, shipped, previous)))
                        Plugin.Log.LogInfo("Game text file written: " + userPath);
                    Directory.CreateDirectory(bases);
                    WriteIfChanged(basePath, shippedText);
                }
            }
            foreach (var userPath in Directory.GetFiles(dir, "*.txt"))
            {
                string code = Path.GetFileNameWithoutExtension(userPath);
                if (done.Contains(code) || string.Equals(Path.GetFileName(userPath), LanguageFiles.TemplateName, StringComparison.OrdinalIgnoreCase)) continue;
                var user = LocTable.Parse(File.ReadAllText(userPath, Encoding.UTF8));
                WriteIfChanged(userPath, GameTexts.Build(terms, GameTexts.Header(user.LanguageName), user, null, null));
            }
        }

        private static bool WriteIfChanged(string path, string text)
        {
            if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == text) return false;
            File.WriteAllText(path, text, LanguageFiles.FileEncoding);
            return true;
        }
    }

    /// <summary>Every text the game takes from I2 Localization in its current language passes here.</summary>
    [HarmonyPatch(typeof(I2.Loc.LanguageSourceData), nameof(I2.Loc.LanguageSourceData.TryGetTranslation))]
    internal static class Patch_GameTextTranslation
    {
        private static void Postfix(string term, ref string Translation, string overrideLanguage, ref bool __result)
        {
            // A text asked for in another named language stays; the dialogue names the current language.
            if (overrideLanguage != null && overrideLanguage != I2.Loc.LocalizationManager.CurrentLanguage) return;
            if (GameTextTranslation.TryGet(term, out var t))
            {
                Translation = t;
                __result = true;
            }
        }
    }

    /// <summary>
    /// Dialogue lines (diplomacy) come from the dialogue database in the game's language, not through I2; the game's
    /// texts still have them as terms. An empty text stays empty: the dialogue picks the line or the menu text by it.
    /// </summary>
    [HarmonyPatch(typeof(PixelCrushers.DialogueSystem.DialogueEntry), nameof(PixelCrushers.DialogueSystem.DialogueEntry.currentDialogueText), MethodType.Getter)]
    internal static class Patch_DialogueText
    {
        private static void Postfix(PixelCrushers.DialogueSystem.DialogueEntry __instance, ref string __result)
        {
            if (!string.IsNullOrEmpty(__result) && GameTextTranslation.Active && GameTextTranslation.TryGet(GameTexts.DialogueTerm(__instance.conversationID, __instance.id, "Dialogue Text"), out var t)) __result = t;
        }
    }

    [HarmonyPatch(typeof(PixelCrushers.DialogueSystem.DialogueEntry), nameof(PixelCrushers.DialogueSystem.DialogueEntry.currentMenuText), MethodType.Getter)]
    internal static class Patch_DialogueMenuText
    {
        private static void Postfix(PixelCrushers.DialogueSystem.DialogueEntry __instance, ref string __result)
        {
            if (!string.IsNullOrEmpty(__result) && GameTextTranslation.Active && GameTextTranslation.TryGet(GameTexts.DialogueTerm(__instance.conversationID, __instance.id, "Menu Text"), out var t)) __result = t;
        }
    }
}

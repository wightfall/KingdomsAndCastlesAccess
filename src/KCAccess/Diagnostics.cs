using System;
using System.IO;
using System.Text;
using BepInEx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KCAccess
{
    /// <summary>
    /// Developer tools: dump the live UI hierarchy to a text file (Ctrl+Shift+F12) and run
    /// commands from a text file so the mod can be exercised without a person at the keyboard.
    /// </summary>
    internal static class Diagnostics
    {
        internal static string DumpPath => Path.Combine(Paths.BepInExRootPath, "kcaccess_ui_dump.txt");

        internal static string CommandPath => Path.Combine(Paths.BepInExRootPath, "kcaccess_commands.txt");

        internal static void DumpUI(bool activeOnly)
        {
            var sb = new StringBuilder();
            sb.AppendLine("KCAccess UI dump " + DateTime.Now);
            foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas == null || canvas.transform.parent != null && canvas.transform.parent.GetComponentInParent<Canvas>() != null) continue;
                if (!canvas.gameObject.scene.IsValid()) continue;
                if (activeOnly && !canvas.gameObject.activeInHierarchy) continue;
                Dump(canvas.transform, 0, sb, activeOnly);
            }
            File.WriteAllText(DumpPath, sb.ToString());
            A.Say("UI dumped");
        }

        private static void Dump(Transform t, int depth, StringBuilder sb, bool activeOnly)
        {
            if (activeOnly && !t.gameObject.activeInHierarchy) return;
            sb.Append(' ', depth * 2);
            sb.Append(t.name);
            if (!t.gameObject.activeSelf) sb.Append(" [off]");
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null || c is Transform || c is CanvasRenderer) continue;
                string n = c.GetType().Name;
                sb.Append(" <").Append(n);
                if (c is Text txt) sb.Append(" \"").Append(Short(txt.text)).Append('"');
                else if (c is TMP_Text tmp) sb.Append(" \"").Append(Short(tmp.text)).Append('"');
                else if (c is Selectable sel) sb.Append(sel.interactable ? "" : " disabled");
                else if (c is Behaviour b && !b.enabled) sb.Append(" off");
                sb.Append('>');
            }
            sb.AppendLine();
            for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), depth + 1, sb, activeOnly);
        }

        private static string Short(string s)
        {
            if (s == null) return "";
            s = s.Replace("\n", "\\n").Replace("\r", "");
            return s.Length > 80 ? s.Substring(0, 80) + "…" : s;
        }

        internal static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        private static float nextCheck;

        /// <summary>
        /// Polls the command file. Each line: "key KeyCode [shift] [ctrl] [alt]", "dump", "dumpall", "say text".
        /// The file is deleted after it is read. Disabled unless Debug.CommandFile is true in the config.
        /// </summary>
        internal static void PollCommands()
        {
            if (!Plugin.CfgDebugCommands.Value || Time.realtimeSinceStartup < nextCheck) return;
            nextCheck = Time.realtimeSinceStartup + 0.25f;
            string path = CommandPath;
            if (!File.Exists(path)) return;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
                File.Delete(path);
            }
            catch (IOException)
            {
                return;
            }
            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                string[] parts = line.Split(' ');
                try
                {
                    switch (parts[0].ToLowerInvariant())
                    {
                        case "key":
                            var key = (KeyCode)Enum.Parse(typeof(KeyCode), parts[1], true);
                            bool shift = Array.IndexOf(parts, "shift") > 0, ctrl = Array.IndexOf(parts, "ctrl") > 0, alt = Array.IndexOf(parts, "alt") > 0;
                            KInput.Inject(key, shift, ctrl, alt);
                            break;
                        case "dump":
                            DumpUI(true);
                            break;
                        case "dumpall":
                            DumpUI(false);
                            break;
                        case "state":
                            var gs = GameState.inst;
                            Plugin.Log.LogInfo("[state] gs=" + (gs != null) + " world=" + (World.inst != null) + " mode=" + (gs != null && gs.CurrMode != null ? gs.CurrMode.GetType().Name : "null")
                                + " mm=" + (gs != null && gs.mainMenuMode != null ? gs.mainMenuMode.GetState().ToString() : "-")
                                + " screen=" + (AccessController.Inst.CurrentScreen != null ? AccessController.Inst.CurrentScreen.Id + "/" + AccessController.Inst.CurrentScreen.Title : "none")
                                + " nav=" + AccessController.Inst.Nav.Count + " menu=" + (AccessController.Inst.ActiveMenu != null) + " panel=" + AccessController.Inst.PanelFocus);
                            break;
                        case "place":
                            var pm = GameUI.inst.CurrPlacementMode;
                            var hb = pm.GetHoverBuilding();
                            var vp = Game.VirtualPointer.Inst;
                            Plugin.Log.LogInfo("[place] placing=" + pm.IsPlacing() + " hover=" + (hb != null ? hb.UniqueName + "@" + hb.transform.position + " cell=" + (hb.GetCell() != null ? hb.GetCell().x + "," + hb.GetCell().z : "null") : "none")
                                + " cursor=" + Game.MapController.Inst.CursorPos + " vp=" + (vp != null ? vp.KeyboardActive + "@" + vp.Target : "none") + " pointer=" + PointingSystem.GetPointer().GetType().Name
                                + " valid=" + (hb != null ? World.inst.CanPlace(hb).ToString() : "-"));
                            break;
                        case "navstate":
                            var n2 = AccessController.Inst.Nav;
                            Plugin.Log.LogInfo("[navstate] editing=" + n2.IsEditing + " root=" + (n2.Root != null) + " count=" + n2.Count + " current=" + (n2.Current != null ? n2.Describe(n2.Current) : "null") + " frame=" + Time.frameCount);
                            break;
                        case "menu":
                            GameState.inst.playingMode.OnClickedMenu();
                            break;
                        case "nav":
                            var nav = AccessController.Inst.Nav;
                            Plugin.Log.LogInfo("[nav] root=" + (nav.Root != null ? PathOf(nav.Root) : "null") + " count=" + nav.Count);
                            foreach (var it in nav.AllItems) Plugin.Log.LogInfo("[nav]  " + PathOf(it.Go.transform) + " => " + nav.Describe(it));
                            break;
                        case "say":
                            A.Say(line.Substring(4));
                            break;
                        default:
                            Plugin.Log.LogWarning("Unknown debug command: " + line);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Debug command failed: " + line + " – " + e.Message);
                }
            }
        }
    }
}

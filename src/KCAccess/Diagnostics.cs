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

        private static readonly KeyCode[] AllKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

        /// <summary>LogKeys option: one log line per key pressed this frame.</summary>
        internal static void LogKeys()
        {
            if (!Input.anyKeyDown) return;
            // Never record what is typed into a text field.
            var ac = AccessController.Inst;
            if (ac != null && ac.Nav.IsEditing) return;
            var sel = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            if (sel != null && ((sel.GetComponent<TMP_InputField>() is TMP_InputField t && t.isFocused) || (sel.GetComponent<InputField>() is InputField f && f.isFocused))) return;
            foreach (var k in AllKeys)
            {
                if (k == KeyCode.None || k >= KeyCode.Mouse0) continue;
                if (Input.GetKeyDown(k))
                    Plugin.Log.LogInfo("[key] " + k + " | " + Modifiers.Describe() + " | focused=" + Application.isFocused + " | " + StateLine());
            }
        }

        private static string StateLine()
        {
            var ac = AccessController.Inst;
            var gs = GameState.inst;
            return "mode=" + (gs != null && gs.CurrMode != null ? gs.CurrMode.GetType().Name : "none")
                + " screen=" + (ac != null && ac.CurrentScreen != null ? ac.CurrentScreen.Id : "none")
                + " menu=" + (ac != null && ac.ActiveMenu != null ? ac.ActiveMenu.GetType().Name : "none")
                + " panel=" + (ac != null && ac.PanelFocus)
                + " blockGameKeys=" + InputGate.BlockGameKeys;
        }

        /// <summary>Ctrl+Shift+F11: what the mod thinks owns the keyboard, for bug reports.</summary>
        internal static string KeyboardReport()
        {
            var os = Environment.OSVersion.Version;
            string win = os.Major == 10 && os.Build >= 22000 ? "Windows 11" : "Windows " + os.Major + "." + os.Minor;
            return "KCAccess " + Plugin.Version + ", " + win + " build " + os.Build + ". " + StateLine() + ". " + Modifiers.Describe()
                + ". Speech: " + A.BackendName + ". Keyboard layout " + Modifiers.KeyboardLayout() + ", language " + System.Globalization.CultureInfo.CurrentCulture.Name;
        }

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
            A.Say(KCAccess.Core.Loc.T("UI dumped"));
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

        /// <summary>Debug command devbuild: places a building at the cursor as PlacementMode.AcceptPlacement does.</summary>
        private static string DevBuild(string uniqueName, bool anywhere, bool clear)
        {
            var prefab = GameState.inst != null ? GameState.inst.GetPlaceableByUniqueName(uniqueName) : null;
            if (prefab == null) return "no building named " + uniqueName;
            var start = Game.MapController.Inst.CursorPos;
            var b = GameUI.inst.InstantiateFromPrefab(prefab);
            // "any": the road coverage and territory checks come first and return early, before the checks for other
            // buildings, water and so on, so they are switched off instead of accepting their answer.
            if (anywhere) b.ignoreRoadCoverageForPlacement = true;
            // CanPlace wants the building on the island where the player picked it up, as when holding it.
            var startCell = World.inst.GetCellData(start.X, start.Z);
            if (startCell != null) GameUI.inst.SetLastHeldBuildingLandMass(startCell.landMassIdx);
            var r = PlacementValidationResult.Valid;
            var reasons = new System.Collections.Generic.Dictionary<PlacementValidationResult, int>();
            if (clear)
            {
                // Only at the cursor: trees and rock under the building go first, as with the creative removal tool.
                b.transform.position = new Vector3(start.X, 0f, start.Z);
                b.BakeBoundingFootprint(); // CanPlace reads the cached footprint (the AI bakes it the same way)
                b.RefreshCachedValues();
                b.ForEachTileInBounds(delegate (int x, int z, Cell cell)
                {
                    TreeSystem.inst.DeleteTreesAt(cell);
                    if (cell.Type == ResourceType.UnusableStone)
                    {
                        World.inst.RemoveStone(cell);
                        cell.Type = ResourceType.None;
                        cell.StorePostGenerationType();
                    }
                });
                r = World.inst.CanPlace(b, ignoreLandMassChecks: anywhere);
                if (r == PlacementValidationResult.Valid)
                {
                    World.inst.Place(b);
                    b.SendMessage("OnPlayerPlacement", SendMessageOptions.DontRequireReceiver);
                    return "placed " + uniqueName + " at " + start + " after clearing";
                }
                UnityEngine.Object.Destroy(b.gameObject);
                return "cannot place " + uniqueName + " at " + start + " even cleared: " + r;
            }
            // The nearest spot (in rings around the cursor, up to 40 tiles) where the game would accept it.
            for (int ring = 0; ring <= 40; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    for (int dz = -ring; dz <= ring; dz++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring) continue;
                        int x = start.X + dx, z = start.Z + dz;
                        if (x < 0 || z < 0 || x >= World.inst.GridWidth || z >= World.inst.GridHeight) continue;
                        b.transform.position = new Vector3(x, 0f, z);
                        b.BakeBoundingFootprint();
                        b.RefreshCachedValues();
                        r = World.inst.CanPlace(b, ignoreLandMassChecks: anywhere);
                        reasons[r] = reasons.TryGetValue(r, out int seen) ? seen + 1 : 1;
                        if (r != PlacementValidationResult.Valid) continue;
                        World.inst.Place(b);
                        b.SendMessage("OnPlayerPlacement", SendMessageOptions.DontRequireReceiver);
                        Game.MapController.Inst.JumpTo(new Core.GridPos(x, z));
                        return "placed " + uniqueName + " at " + x + ", " + z;
                    }
                }
            }
            UnityEngine.Object.Destroy(b.gameObject);
            var why = new StringBuilder();
            foreach (var kv in reasons) why.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            return "cannot place " + uniqueName + " near " + start + ":" + why;
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
                        case "deliver":
                            if (parts.Length > 1 && Enum.TryParse(parts[1], true, out KeyCode dk)) OsKeyboard.Deliver(dk);
                            break;
                        case "bubbles":
                        {
                            var tb = ThoughtBubbleSystem.thoughts;
                            var sb = new StringBuilder("[bubbles] ");
                            if (tb != null) for (int i = 0; i < tb.Count; i++) { var b = tb.data[i]; if (b != null) sb.Append(b.thought).Append(b.visible ? "+" : "-").Append(' '); }
                            Plugin.Log.LogInfo(sb.ToString());
                            break;
                        }
                        case "listeners":
                        {
                            var lcur = AccessController.Inst.Nav.Current;
                            var lbtn = lcur != null ? lcur.Go.GetComponent<UnityEngine.UI.Button>() : null;
                            var lstr = new StringBuilder("[listeners] ");
                            if (lbtn != null)
                            {
                                lstr.Append(lbtn.name).Append(": ");
                                for (int i = 0; i < lbtn.onClick.GetPersistentEventCount(); i++)
                                    lstr.Append(lbtn.onClick.GetPersistentTarget(i)?.GetType().Name).Append('.').Append(lbtn.onClick.GetPersistentMethodName(i)).Append(' ');
                                foreach (var c in lbtn.GetComponents<Component>()) lstr.Append('<').Append(c.GetType().Name).Append('>');
                            }
                            Plugin.Log.LogInfo(lstr.ToString());
                            break;
                        }
                        case "pad":
                            // Test only: "pad A", "pad Up lt", "pad Start lt rt"
                            if (parts.Length > 1 && Enum.TryParse(parts[1], true, out KCAccess.Core.PadButton pb))
                                ControllerInput.Simulate(pb, Array.IndexOf(parts, "lt") > 0, Array.IndexOf(parts, "rt") > 0);
                            break;
                        case "alerttest":
                            // Test only: raise the keep's advisor exclamation mark like the game does.
                            if (Player.inst != null && Player.inst.keep != null && Player.inst.keep.issueButton != null) Player.inst.keep.issueButton.gameObject.SetActive(true);
                            break;
                        case "logkeys":
                            Plugin.CfgLogKeys.Value = !Plugin.CfgLogKeys.Value;
                            Plugin.Log.LogInfo("[dbg] LogKeys " + Plugin.CfgLogKeys.Value);
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
                        case "modal":
                            var cs = AccessController.Inst.CurrentScreen;
                            var adv = AdvisorUI.inst;
                            Plugin.Log.LogInfo("[modal] " + (cs != null ? cs.Id + " root=" + (cs.Root != null ? PathOf(cs.Root) + " active=" + cs.Root.gameObject.activeInHierarchy : "null") : "none")
                                + " advContainerActive=" + (adv != null && adv.containerRect != null && adv.containerRect.gameObject.activeInHierarchy)
                                + " detect=" + (Game.GameScreens.Detect() != null ? Game.GameScreens.Detect().Id : "null"));
                            break;
                        case "navstate":
                            var n2 = AccessController.Inst.Nav;
                            Plugin.Log.LogInfo("[navstate] editing=" + n2.IsEditing + " root=" + (n2.Root != null) + " count=" + n2.Count + " current=" + (n2.Current != null ? n2.Describe(n2.Current) : "null") + " frame=" + Time.frameCount);
                            break;
                        case "groups":
                            var ui2 = GameUI.inst;
                            Plugin.Log.LogInfo("[groups] " + string.Join(" | ", Game.GameScreens.PanelGroups().ConvertAll(g => g.Key + ":" + g.Title).ToArray())
                                + " island=" + (ui2.islandInfoUI != null ? ui2.islandInfoUI.gameObject.activeInHierarchy + "/" + UI.UIText.IsVisible(ui2.islandInfoUI.gameObject) + "/" + UI.UIText.IsOnScreen(ui2.islandInfoUI.transform) : "null")
                                + " creative=" + (ui2.creativeModeOptions != null ? ui2.creativeModeOptions.activeInHierarchy + "/" + UI.UIText.IsVisible(ui2.creativeModeOptions) + "/" + UI.UIText.IsOnScreen(ui2.creativeModeOptions.transform) : "null"));
                            break;
                        case "finishbuild":
                            // Developer only: instantly complete the selected building (to test its panels).
                            var fb = GameUI.inst.GetBuildingSelected();
                            if (fb != null && !fb.IsBuilt())
                            {
                                fb.constructionProgress = 1f;
                                fb.CompleteBuild();
                                Plugin.Log.LogInfo("[dbg] completed " + fb.UniqueName);
                            }
                            break;
                        case "buildnow":
                            // Developer only: every construction site finishes on its next construction update, through
                            // the game's own path (as with "build for free"), so the finished announcement can be tested.
                            int sites = 0;
                            foreach (var site in Building.ConstructionList)
                            {
                                if (site == null || site.IsBuilt()) continue;
                                site.BuildTimeAtMax = -1f;
                                sites++;
                            }
                            Plugin.Log.LogInfo("[dbg] buildnow " + sites + " sites");
                            break;
                        case "devbuild":
                            // Developer only: "devbuild <unique name> [any] [clear]" places that building at the nearest spot
                            // to the cursor the game accepts (as a placement does); "any" also allows spots outside road
                            // coverage or territory, "clear" removes trees and rock at the cursor first and places it there.
                            // buildnow then finishes it.
                            if (parts.Length > 1) Plugin.Log.LogInfo("[dbg] " + DevBuild(parts[1], Array.IndexOf(parts, "any") > 1, Array.IndexOf(parts, "clear") > 1));
                            break;
                        case "dragon":
                        {
                            // Developer only: "dragon" / "dragon mama" spawns a young / adult dragon at the cursor the way
                            // the game spawns them; with an open dragon nest of the player it settles there (taming).
                            var dp = Game.MapController.Inst.CursorPos;
                            var at = new Vector3(dp.X, DragonController.dragonFlyHeight, dp.Z);
                            if (parts.Length > 1 && parts[1] == "mama") DragonSpawn.inst.SpawnMamaDragon(at);
                            else DragonSpawn.inst.SpawnBabyDragon(at);
                            Plugin.Log.LogInfo("[dbg] dragon spawned at " + dp);
                            break;
                        }
                        case "nest":
                        {
                            // Developer only: "nest egg|hatch|mature|happy|bond" on the selected dragon nest (breeding stages, taming).
                            var nb = GameUI.inst.GetBuildingSelected();
                            var nest = nb != null ? nb.GetComponent<DragonNest>() : null;
                            if (nest == null || parts.Length < 2) { Plugin.Log.LogInfo("[dbg] select a dragon nest first"); break; }
                            if (parts[1] == "egg") nest.LayEgg();
                            else if (parts[1] == "hatch") nest.HatchEgg();
                            else if (parts[1] == "mature") nest.MatureBaby();
                            else if (parts[1] == "happy")
                            {
                                foreach (var s in nest.slots) if (s.hasDragon) { s.happiness = 100; s.age = Math.Max(s.age, 50); }
                            }
                            else if (parts[1] == "bond")
                            {
                                foreach (var s in nest.slots) if (s.hasDragon) { s.hasStartedBonding = true; s.bondProgress = 100f; s.isBonded = true; s.happiness = 100; }
                            }
                            Plugin.Log.LogInfo("[dbg] nest " + parts[1] + ": egg=" + nest.HasEgg() + " infant=" + nest.HasInfant() + " dragons=" + nest.GetDragonCount());
                            break;
                        }
                        case "ai":
                            var abc = AIBrainsContainer.inst;
                            var lsb = new StringBuilder("[ai] kingdoms=" + (abc != null ? abc.kingdoms.Count : -1));
                            if (abc != null) foreach (var k in abc.kingdoms) lsb.Append(" | team=" + k.LandmassOwner?.teamId + " lms=" + (k.LandmassOwner?.ownedLandMasses != null ? k.LandmassOwner.ownedLandMasses.Count : -1));
                            lsb.Append(" start=" + (abc?.aiStartInfo?.startData != null ? abc.aiStartInfo.startData.Length : -1));
                            for (int lmi = 0; lmi < World.inst.NumLandMasses; lmi++) lsb.Append(" [" + lmi + ":" + (lmi < Player.inst.LandMassNames.Count ? Player.inst.LandMassNames[lmi] : "?") + " owner=" + (World.GetLandmassOwner(lmi)?.teamId.ToString() ?? "none") + " keeps=" + Player.inst.GetBuildingListForLandMass(lmi, World.keepHash).Count + "]");
                            Plugin.Log.LogInfo(lsb.ToString());
                            break;
                        case "invoke":
                            var cur = AccessController.Inst.Nav.Current;
                            var btn = cur != null ? cur.Go.GetComponent<UnityEngine.UI.Button>() : null;
                            Plugin.Log.LogInfo("[dbg] invoke " + (btn != null ? btn.name + " listeners=" + btn.onClick.GetPersistentEventCount() + " interactable=" + btn.interactable : "no button"));
                            if (btn != null) btn.onClick.Invoke();
                            break;
                        case "navinfo":
                            var mc = Game.MapController.Inst;
                            var tgt = mc.Nav.Target;
                            if (tgt.HasValue)
                            {
                                var bs = Core.BeaconMath.Compute(tgt.Value.X - mc.CursorPos.X, tgt.Value.Z - mc.CursorPos.Z);
                                Plugin.Log.LogInfo("[navinfo] cursor=" + mc.CursorPos + " target=" + tgt.Value + " (" + mc.Nav.TargetLabel + ") pan=" + bs.Pan.ToString("0.00") + " pitch=" + bs.Pitch.ToString("0.00") + " interval=" + bs.Interval.ToString("0.00") + " dist=" + bs.Distance + " walking=" + mc.Nav.Walking + " beacon=" + mc.Nav.BeaconOn);
                            }
                            else Plugin.Log.LogInfo("[navinfo] cursor=" + mc.CursorPos + " no target");
                            break;
                        case "visit":
                            Plugin.Log.LogInfo("[dbg] " + Game.Diplomacy.DebugVisit());
                            break;
                        case "convo":
                            // Developer only: start a diplomacy conversation by name (TradeMenuAI, AIGift, GoldDemand ...).
                            if (GameUI.inst.diplomacyUI.Visible() && parts.Length > 1) GameUI.inst.diplomacyUI.StartConvo(parts[1]);
                            break;
                        case "standing":
                            // Developer only: raise (or lower) the visited kingdom's opinion of the player.
                            if (GameUI.inst.diplomacyUI.Visible() && parts.Length > 1) GameUI.inst.diplomacyUI.ModifyStanding(int.Parse(parts[1]));
                            break;
                        case "feast":
                            // Developer only: the feast menu and, with "feast eat", the meal (as the dialogue opens them).
                            if (GameUI.inst.diplomacyUI.Visible())
                            {
                                if (parts.Length > 1 && parts[1] == "eat") { GameUI.inst.diplomacyUI.feastType = parts.Length > 2 ? parts[2] : "apples"; GameUI.inst.diplomacyUI.DoFoodAnim(); }
                                else GameUI.inst.diplomacyUI.ShowFoodMenu();
                            }
                            break;
                        case "lowerprices":
                            // Developer only: "Can you do better?" a few times, then the price list refresh the dialogue does.
                            if (GameUI.inst.diplomacyUI.Visible())
                            {
                                for (int i = 0; i < 6; i++) GameUI.inst.diplomacyUI.LowerPrices();
                                GameUI.inst.diplomacyUI.UpdateAISellPriceUI();
                            }
                            break;
                        case "negotiate":
                            // Developer only: the trade price editor as the dialogue opens it (needs an open visit).
                            if (GameUI.inst.diplomacyUI.Visible()) GameUI.inst.diplomacyUI.DisplayNegotiationUI();
                            break;
                        case "gold":
                            Player.inst.PlayerLandmassOwner.Gold += 5000;
                            break;
                        case "fail":
                            GameState.inst.SetNewMode(GameState.inst.mainMenuMode);
                            GameState.inst.mainMenuMode.TransitionTo(parts.Length > 1 && parts[1] == "keep" ? MainMenuMode.State.KeepDestroyed : MainMenuMode.State.Failure);
                            break;
                        case "merchant":
                            ShipSystem.inst.merchantSpawnTimer = 0f;
                            break;
                        case "threatyears":
                            // Developer only: years until the next viking raid and dragon attack (the last-year warnings).
                            if (parts.Length > 1)
                            {
                                int years = int.Parse(parts[1]);
                                if (RaiderSystem.inst != null) RaiderSystem.inst.yearsUntilNextAttack = years;
                                if (DragonSpawn.inst != null) DragonSpawn.inst.yearsUntilNextAttack = years;
                                Plugin.Log.LogInfo("[dbg] threat years " + years + " timers=" + (Assets.Settings.inst != null && Assets.Settings.inst.VikingDragonTimers)
                                    + " vikingsAllowed=" + (RaiderSystem.inst != null && RaiderSystem.inst.AllowSpawningVikings()) + " dragonsAllowed=" + (DragonSpawn.inst != null && DragonSpawn.inst.AllowSpawning())
                                    + " raid=" + (RaiderSystem.inst != null && RaiderSystem.inst.IsRaidInProgress()) + " dragonAttack=" + (DragonSpawn.inst != null && DragonSpawn.inst.IsAttackInProgress())
                                    + " difficulty=" + (Player.inst != null ? Player.inst.difficulty.ToString() : "-"));
                            }
                            break;
                        case "menu":
                            GameState.inst.playingMode.OnClickedMenu();
                            break;
                        case "nav":
                            var nav = AccessController.Inst.Nav;
                            Plugin.Log.LogInfo("[nav] root=" + (nav.Root != null ? PathOf(nav.Root) : "null") + " count=" + nav.Count);
                            foreach (var it in nav.AllItems) Plugin.Log.LogInfo("[nav]  " + PathOf(it.Go.transform) + " => " + nav.Describe(it));
                            break;
                        case "tracespeech":
                            // Developer only: log where texts starting with the given words are spoken from.
                            A.TracePrefix = parts.Length > 1 ? line.Substring(12) : null;
                            Plugin.Log.LogInfo("[dbg] tracing speech: " + (A.TracePrefix ?? "off"));
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

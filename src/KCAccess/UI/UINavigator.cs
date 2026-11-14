using System;
using System.Collections.Generic;
using KCAccess.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KCAccess.UI
{
    /// <summary>One entry the player can move to: an interactive control or a block of text.</summary>
    internal sealed class UIItem
    {
        public Selectable Control;
        public List<Component> Texts;
        public GameObject Go;
        public Vector2 Center;
        /// <summary>Group caption used when several controls share the same label ("Standard Mode" for "Accept").</summary>
        public string Context;
        /// <summary>An icon whose meaning is only in its tooltip (no text, not a control): the item reads the tooltip.</summary>
        public TooltipHook Hook;

        public bool IsControl => Control != null;

        public override bool Equals(object obj) => obj is UIItem o && o.Go == Go;

        public override int GetHashCode() => Go != null ? Go.GetHashCode() : 0;
    }

    /// <summary>
    /// Keyboard navigation for any uGUI panel:
    /// Up/Down = every item (controls and text), Tab/Shift+Tab = controls only, Home/End,
    /// Enter/Space = activate, Left/Right = change sliders, combo boxes and option rows.
    /// </summary>
    internal sealed class UINavigator
    {
        private readonly NavList<UIItem> list;
        private readonly TypeAhead typeAhead = new TypeAhead(() => Time.realtimeSinceStartup);
        private float nextRefresh;
        private TooltipHook hoveredHook;
        private InputEditSession editSession;

        public Transform Root { get; private set; }

        /// <summary>All panels navigated together (Root is the first one).</summary>
        public List<Transform> Roots { get; } = new List<Transform>();
        public string Title { get; private set; }

        /// <summary>The same screen under a new name: the mod language changed while it was open (Ctrl+R read the old title).</summary>
        internal void Retitle(string title)
        {
            if (!string.IsNullOrEmpty(title)) Title = title;
        }

        /// <summary>Extra filter set by screens, e.g. to hide decorative buttons.</summary>
        public Func<GameObject, bool> Exclude;

        /// <summary>Allow first-letter navigation (only where letters are not game hotkeys).</summary>
        public bool TypeAheadEnabled { get; set; }

        public bool IncludeTexts { get; set; } = true;

        public bool IsEditing => editSession != null;

        public int Count => list.Count;

        public UINavigator()
        {
            list = new NavList<UIItem>(Describe, wrap: true);
        }

        public void SetRoot(Transform root, string title, bool announce, string intro = null, string initialFocus = null)
        {
            SetRoots(new List<Transform> { root }, title, announce, intro, initialFocus);
        }

        public void SetRoots(List<Transform> roots, string title, bool announce, string intro = null, string initialFocus = null)
        {
            // Drop panels nested inside another panel of the group (they are walked anyway).
            roots = roots.FindAll(r => r != null && !roots.Exists(o => o != null && o != r && r.IsChildOf(o)));
            Transform root = roots.Count > 0 ? roots[0] : null;
            bool changed = root != Root || roots.Count != Roots.Count;
            for (int i = 0; !changed && i < roots.Count; i++) changed = roots[i] != Roots[i];
            EndEdit(false);
            if (changed)
            {
                textSnapshot = null;
                clickedGo = null; // the new screen announces itself
            }
            Roots.Clear();
            Roots.AddRange(roots);
            Root = root;
            Title = title;
            if (changed)
            {
                SetHover(null);
                list.Clear();
            }
            bool requested = focusRequest != null;
            Refresh(force: true);
            // A pending focus request (e.g. "start on Cancel") that matched wins over the default first item.
            if (requested && focusRequest == null) changed = false;
            if (changed && initialFocus == "")
            {
                int firstText = 0;
                for (int i = 0; i < list.Count; i++) if (!list.Items[i].IsControl) { firstText = i; break; }
                list.SelectIndex(firstText);
            }
            else if (changed && initialFocus != null && FocusControlNamed(initialFocus))
            {
            }
            else if (changed && list.Count > 0)
            {
                // Start on the first interactive control so Enter does something useful.
                int firstControl = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (!list.Items[i].IsControl) continue;
                    if (firstControl < 0) firstControl = i;
                    if (IsBackButton(list.Items[i].Control)) continue;
                    if (list.Items[i].Control is Slider rs && UIText.IsReadOnlySlider(rs)) continue;
                    firstControl = i;
                    break;
                }
                if (firstControl >= 0) list.SelectIndex(firstControl);
            }
            if (announce) AnnounceScreen(intro);
        }

        /// <summary>
        /// Back / Close / Cancel buttons are not a good first stop. Recognised by object and handler names too: their
        /// captions are in the game's language.
        /// </summary>
        private static bool IsBackButton(Selectable s)
        {
            string n = s.gameObject.name.ToLowerInvariant();
            if (n == "x" || n.Contains("back") || n.Contains("close") || n.Contains("cancel")) return true;
            if (s is Button b)
            {
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                {
                    string m = (b.onClick.GetPersistentMethodName(i) ?? string.Empty).ToLowerInvariant();
                    if (m.Contains("back") || m.Contains("close") || m.Contains("cancel")) return true;
                }
            }
            string l = UIText.LabelOf(s).ToLowerInvariant();
            return l == "back" || l == "close" || l == "x" || l == "cancel" || l == Loc.T("Back").ToLowerInvariant() || l == Loc.T("Close").ToLowerInvariant() || l == Loc.T("Cancel").ToLowerInvariant();
        }

        public void Clear()
        {
            EndEdit(false);
            SetHover(null);
            textSnapshot = null;
            Root = null;
            Roots.Clear();
            Title = null;
            list.Clear();
        }

        public void AnnounceScreen(string intro = null)
        {
            // After a conversation's first line: only the name of the window, as the focus goes on to the first reply.
            if (Patch_DialogueSubtitle.ConversationSpeaking)
            {
                A.SayQueued(TextUtil.Sentences(Title, intro));
                return;
            }
            string current = list.HasCurrent ? Describe(list.Current) : Loc.T("no items");
            // A panel named after its first text (a construction site's "Cottage") says the name once.
            if (string.IsNullOrEmpty(intro) && string.Equals(TextUtil.Clean(current).TrimEnd('.', '!', '?', ':'), TextUtil.Clean(Title).TrimEnd('.', '!', '?', ':'), StringComparison.OrdinalIgnoreCase))
                current = null;
            A.Say(TextUtil.Sentences(Title, intro, current));
        }

        private Func<GameObject, bool> focusRequest;
        private bool focusRequestSpeaks;
        private float focusRequestUntil;
        /// <summary>
        /// How often the request was looked for among the items. On a slow computer a window can take longer to appear
        /// than the request's three seconds (an envoy's replies were then never focused or said): it is given up only
        /// after it was really looked for a number of times as well.
        /// </summary>
        private int focusRequestTries;

        /// <summary>Move focus to the first item matching the predicate as soon as it appears (within 3 seconds).</summary>
        /// <param name="match">The item to move to, as soon as it is in the list.</param>
        /// <param name="speak">
        /// Say the item when it is reached. For a list that opens after instructions: Down would go to the second item and
        /// Enter would choose an item that was never said.
        /// </param>
        public void RequestFocus(Func<GameObject, bool> match, bool speak = false)
        {
            focusRequest = match;
            focusRequestSpeaks = speak;
            focusRequestUntil = Time.unscaledTime + 3f;
            focusRequestTries = 0;
            nextRefresh = 0f;
            // The new focus speaks for the click; texts that appeared with it are not read on top.
            textSnapshot = null;
        }

        public void Refresh(bool force = false)
        {
            if (Root == null) return;
            if (focusRequest != null && Time.unscaledTime > focusRequestUntil && focusRequestTries >= 12) focusRequest = null;
            if (!force && Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + (focusRequest != null ? 0.05f : 0.25f);
            var all = new List<UIItem>();
            foreach (var r in Roots) if (r != null && r.gameObject.activeInHierarchy) all.AddRange(Collect(r));
            list.SetItems(all, keepFocus: true);
            if (focusRequest != null)
            {
                focusRequestTries++;
                for (int i = 0; i < list.Count; i++)
                {
                    var go = list.Items[i].Go;
                    if (go != null && list.Items[i].IsControl && focusRequest(go))
                    {
                        list.SelectIndex(i);
                        focusRequest = null;
                        SetHover(go);
                        if (focusRequestSpeaks) A.SayQueued(Describe(list.Items[i]));
                        clickedGo = null;
                        break;
                    }
                }
            }
            SayWhereTheKeyboardWent();
        }

        private List<UIItem> Collect(Transform root)
        {
            // Depth-first walk in hierarchy order: layout groups in this game follow the hierarchy,
            // so this keeps columns and groups together better than sorting by screen position.
            var items = new List<UIItem>();
            Walk(root, root, items);
            // Merge neighbouring texts on the same row ("Population:" + "25").
            var merged = new List<UIItem>(items.Count);
            foreach (var item in items)
            {
                var last = merged.Count > 0 ? merged[merged.Count - 1] : null;
                // ... and a title with its number under it (the totals below the job priorities).
                if (last != null && !last.IsControl && !item.IsControl && last.Hook == null && item.Hook == null && last.Go.transform.parent == item.Go.transform.parent
                    && (Mathf.Abs(last.Center.y - item.Center.y) <= 14f || last.Go.transform.parent.name.StartsWith("SubSectionItem", StringComparison.Ordinal)))
                {
                    last.Texts.AddRange(item.Texts);
                    continue;
                }
                merged.Add(item);
            }
            // Drop texts that are just the label of the control next to them (toggle / slider captions).
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var it = merged[i];
                if (it.IsControl) continue;
                string txt = UIText.JoinTexts(it.Texts);
                bool dup = (i > 0 && merged[i - 1].IsControl && SameLabel(UIText.LabelOf(merged[i - 1].Control), txt) && Says(merged[i - 1], txt))
                    || (i + 1 < merged.Count && merged[i + 1].IsControl && SameLabel(UIText.LabelOf(merged[i + 1].Control), txt) && Says(merged[i + 1], txt));
                if (dup) merged.RemoveAt(i);
            }
            // The language list has an invisible full-screen button behind it that closes it on a click outside
            // ("1pixelwhite"): Escape does that already.
            merged.RemoveAll(it => it.IsControl && it.Go != null && it.Go.transform.parent != null
                && it.Go.transform.parent.name == "LanguageSelector" && it.Go.name == "GameObject");
            // The food pictures in a row of resource amounts are clickable but do nothing (a ship's cargo amounts).
            merged.RemoveAll(it => it.IsControl && it.Go != null && it.Go.transform.parent != null && it.Go.name.EndsWith("icon", StringComparison.Ordinal)
                && it.Go.transform.parent.name.StartsWith("resourceamt", StringComparison.Ordinal));
            // Kingdom share: "Packaging up your kingdom" stays behind the kingdom's summary once it is done.
            var share = GameState.inst != null && GameState.inst.mainMenuMode != null ? GameState.inst.mainMenuMode.kingdomShareUI : null;
            if (share != null && share.U_saveLoadOption != null && share.U_saveLoadOption.gameObject.activeSelf)
                merged.RemoveAll(it => !it.IsControl && it.Go != null && it.Go.name == "LoadingText" && it.Go.transform.IsChildOf(share.transform));
            AddContextToDuplicates(merged);
            return merged;
        }

        /// <summary>
        /// The control's spoken description contains the text. A control the mod describes itself may not use its caption:
        /// the moat's panel has only its description next to the name field, which would otherwise vanish as that label.
        /// </summary>
        private bool Says(UIItem control, string text) => Describe(control).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The text is the control's label, or the first part of it ("Bryce" vs "Bryce, Find Villager").</summary>
        private static bool SameLabel(string label, string text)
        {
            if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(text)) return false;
            return label == text || label.StartsWith(text + ",");
        }

        private static void AddContextToDuplicates(List<UIItem> items)
        {
            var counts = new Dictionary<string, int>();
            var labels = new Dictionary<UIItem, string>();
            foreach (var it in items)
            {
                if (!it.IsControl) continue;
                string l = UIText.LabelOf(it.Control);
                labels[it] = l;
                counts[l] = counts.TryGetValue(l, out var n) ? n + 1 : 1;
            }
            foreach (var it in items)
            {
                if (!it.IsControl || counts[labels[it]] < 2) continue;
                // Caption: first visible text in the closest ancestor group that is not part of the control.
                for (var p = it.Go.transform.parent; p != null && p.parent != null; p = p.parent)
                {
                    string caption = null;
                    foreach (var txt in UIText.VisibleTexts(p))
                    {
                        if (txt.transform.IsChildOf(it.Go.transform)) continue;
                        if (txt.GetComponentInParent<Selectable>() != null && txt.GetComponentInParent<Selectable>().transform.IsChildOf(p)) continue;
                        caption = TextUtil.Clean(UIText.TextOf(txt));
                        break;
                    }
                    if (!string.IsNullOrEmpty(caption))
                    {
                        if (caption.Length > 60) caption = caption.Substring(0, 60);
                        it.Context = caption;
                        break;
                    }
                }
            }
        }

        private void Walk(Transform t, Transform root, List<UIItem> items)
        {
            if (!t.gameObject.activeInHierarchy) return;
            if (t != root && (Special.Exclude(t.gameObject) || (Exclude != null && Exclude(t.gameObject)))) return;
            var s = t.GetComponent<Selectable>();
            if (s != null && s.enabled && IsUsableControl(s))
            {
                // A button that is only a picture around another control: the control inside is the item.
                if (Special.WrapperOnly(s))
                {
                    for (int i = 0; i < t.childCount; i++) WalkNested(t.GetChild(i), root, items);
                    return;
                }
                items.Add(new UIItem { Control = s, Go = s.gameObject, Center = UIText.ScreenCenter(s.transform) });
                // Texts inside a control are its label; nested controls (e.g. toggles inside a list row) are still visited.
                for (int i = 0; i < t.childCount; i++) WalkNested(t.GetChild(i), root, items);
                return;
            }
            if (IncludeTexts)
            {
                bool added = false;
                foreach (var c in t.GetComponents<Component>())
                {
                    if (!UIText.IsTextComponent(c)) continue;
                    if (!UIText.IsTextVisible(c) || !TextUtil.HasContent(UIText.TextOf(c))) continue;
                    if (c.GetComponentInParent<InputField>() != null || c.GetComponentInParent<TMP_InputField>() != null) continue;
                    items.Add(new UIItem { Texts = new List<Component> { c }, Go = c.gameObject, Center = UIText.ScreenCenter(c.transform) });
                    added = true;
                    break;
                }
                // Status icons explained only by their tooltip (the dragon nest's needs, a troop ship's cargo ...).
                if (!added && s == null && UIText.IconTooltip(t.gameObject) is TooltipHook hook)
                    items.Add(new UIItem { Texts = new List<Component>(), Go = t.gameObject, Center = UIText.ScreenCenter(t), Hook = hook });
            }
            var ordered = Special.OrderedChildren(t);
            if (ordered != null) foreach (var c in ordered) Walk(c, root, items);
            else for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), root, items);
        }

        private void WalkNested(Transform t, Transform root, List<UIItem> items)
        {
            if (!t.gameObject.activeInHierarchy) return;
            var s = t.GetComponent<Selectable>();
            if (s != null && s.enabled && !(s is Button) && IsUsableControl(s))
            {
                items.Add(new UIItem { Control = s, Go = s.gameObject, Center = UIText.ScreenCenter(s.transform) });
            }
            for (int i = 0; i < t.childCount; i++) WalkNested(t.GetChild(i), root, items);
        }

        private bool IsUsableControl(Selectable s)
        {
            if (!UIText.IsVisible(s.gameObject)) return false;
            if (s is Scrollbar && s.GetComponentInParent<ScrollRect>() != null) return false;
            if (Exclude != null && Exclude(s.gameObject)) return false;
            return true;
        }

        private static bool RecentlySpoken(string text)
        {
            if (A.Announcer == null) return false;
            int n = 0;
            var hist = new List<string>(A.Announcer.History);
            for (int i = hist.Count - 1; i >= 0 && n < 6; i--, n++)
            {
                if (hist[i] == text || hist[i].EndsWith(": " + text)) return true;
            }
            return false;
        }

        private static bool IsNumberish(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (!(char.IsDigit(c) || c == ' ' || c == '/' || c == '%' || c == '.' || c == ',' || c == '-' || c == '+')) return false;
            return true;
        }

        public string Describe(UIItem item)
        {
            if (item == null) return string.Empty;
            if (item.Hook != null) return Special.TwitchLight(item) ?? UIText.TooltipOf(item.Go) ?? string.Empty;
            string special = Special.Describe(item);
            if (special != null) return special;
            if (!item.IsControl)
            {
                string t = UIText.JoinTexts(item.Texts);
                // A very long text (the main menu's news: the whole patch history) is not read in one go: its first
                // line and how to read it line by line.
                if (t.Length > LongText.SummaryLength && LongText.Summary(RawText(item)) is string summary) return summary;
                if (IsNumberish(t))
                {
                    string res = Special.ResourceIconName(item.Go.transform);
                    if (res != null) return res + " " + t;
                }
                return t;
            }
            var s = item.Control;
            // Read-only fields and bars are really just text.
            // (No caption is added: the nearest text is often not the field's caption, e.g. the town size next to the town name.)
            if (!s.interactable && (s is InputField || s is TMP_InputField)) return UIText.ValueOf(s) == UIText.Blank ? UIText.LabelOf(s) : UIText.ValueOf(s);
            if (s is Slider ro && UIText.IsReadOnlySlider(ro)) return TextUtil.Join(", ", UIText.LabelOf(ro), UIText.ValueOf(ro));
            string label = UIText.LabelOf(s);
            if (!string.IsNullOrEmpty(item.Context) && item.Context != label) label = item.Context + ", " + label;
            // Icon-only rows ("20" next to a wood icon): add the resource name.
            if (IsNumberish(label) || label == UIText.Unlabelled)
            {
                string res = Special.ResourceIconName(s.transform);
                if (res != null) label = res + (label == UIText.Unlabelled ? string.Empty : " " + label);
            }
            string role = UIText.RoleOf(s);
            string value = UIText.ValueOf(s);
            var parts = new List<string> { label };
            if (role != UIText.ButtonRole || !s.interactable) parts.Add(role);
            if (!string.IsNullOrEmpty(value) && value != label) parts.Add(value);
            if (!s.interactable) parts.Add(Loc.T("unavailable"));
            return TextUtil.Join(", ", parts);
        }

        public UIItem Current => list.Current;

        /// <summary>The text of a text item as shown, line breaks kept (for the long text reader).</summary>
        internal static string RawText(UIItem item)
        {
            if (item == null || item.Texts == null) return string.Empty;
            var parts = new List<string>();
            foreach (var c in item.Texts)
            {
                string t = UIText.TextOf(c);
                if (TextUtil.HasContent(t)) parts.Add(t);
            }
            return string.Join("\n", parts.ToArray());
        }

        public IReadOnlyList<UIItem> AllItems => list.Items;

        /// <summary>Handle navigation keys. Returns true when a key was used.</summary>
        public bool HandleInput()
        {
            if (Root == null) return false;
            if (editSession != null)
            {
                return editSession.Update(this);
            }
            CheckForChanges();
            Refresh();
            if (list.Count == 0) return false;
            bool shift = KInput.Shift;

            if (KInput.Down(KeyCode.DownArrow) && KInput.NoMods) return Move(1, controlsOnly: false);
            if (KInput.Down(KeyCode.UpArrow) && KInput.NoMods) return Move(-1, controlsOnly: false);
            if (KInput.Down(KeyCode.Tab) && !KInput.Ctrl && !KInput.Alt) return Move(shift ? -1 : 1, controlsOnly: true);
            if (KInput.Plain(KeyCode.Home))
            {
                list.First();
                Focus(NavResult.Moved);
                return true;
            }
            if (KInput.Plain(KeyCode.End))
            {
                list.Last();
                Focus(NavResult.Moved);
                return true;
            }
            var cur = list.Current;
            if (Special.HandleKey(this, cur)) return true;
            if ((KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.KeypadEnter) || KInput.Plain(KeyCode.Space)) && cur != null)
            {
                KInput.Consume(KeyCode.Space);
                KInput.Consume(KeyCode.Return);
                Activate(cur);
                return true;
            }
            if (cur != null && cur.IsControl && KInput.NoMods)
            {
                if (KInput.Down(KeyCode.LeftArrow)) return Adjust(cur.Control, -1);
                if (KInput.Down(KeyCode.RightArrow)) return Adjust(cur.Control, 1);
                if (KInput.Down(KeyCode.PageDown)) return Adjust(cur.Control, -10);
                if (KInput.Down(KeyCode.PageUp)) return Adjust(cur.Control, 10);
            }
            if (TypeAheadEnabled)
            {
                char? c = KInput.LetterDown();
                if (c.HasValue)
                {
                    string prefix = typeAhead.Add(c.Value);
                    if (list.FindNext(prefix)) Focus(NavResult.Moved);
                    else A.Cue(Cue.Edge);
                    return true;
                }
            }
            return false;
        }

        private bool Move(int delta, bool controlsOnly)
        {
            if (!controlsOnly)
            {
                Focus(list.Step(delta));
                return true;
            }
            int start = list.Index;
            NavResult res = NavResult.Moved;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list.Step(delta);
                if (r == NavResult.Wrapped) res = NavResult.Wrapped;
                if (list.Current != null && list.Current.IsControl) break;
                if (list.Index == start) break;
            }
            Focus(res);
            return true;
        }

        private void Focus(NavResult result)
        {
            var cur = list.Current;
            if (cur == null)
            {
                A.Cue(Cue.Edge);
                return;
            }
            A.Cue(result == NavResult.Wrapped ? Cue.Wrap : (result == NavResult.HitEdge ? Cue.Edge : Cue.Navigate));
            EnsureVisible(cur.Go);
            SetHover(cur.Go);
            string tip = Special.ExtraTooltip(cur) ?? (cur.IsControl ? UIText.TooltipOf(cur.Go) : null);
            string desc = Describe(cur);
            if (tip != null && !desc.Contains(tip)) desc = TextUtil.Sentences(desc, tip);
            if (Plugin.CfgPositions.Value && list.Count > 1) desc += ", " + list.PositionText;
            A.Say(desc);
        }

        /// <summary>What this screen contains, so F1 names only the keys that work here.</summary>
        internal ScreenFeatures Features(bool inPanel, bool playing)
        {
            Refresh(force: true);
            var f = new ScreenFeatures { Items = list.Count, TypeAhead = TypeAheadEnabled, InPanel = inPanel, Playing = playing };
            foreach (var item in list.Items)
            {
                if (item.Go != null && item.Go.GetComponentInParent<Assets.Code.UI.SaveLoadOption>() != null) f.SaveSlots = true;
                if (!item.IsControl)
                {
                    f.Texts = true;
                    continue;
                }
                f.Controls = true;
                switch (item.Control)
                {
                    case Slider sl when !UIText.IsReadOnlySlider(sl):
                        f.Adjustables = true;
                        f.Sliders = true;
                        break;
                    case Scrollbar _:
                    case Dropdown _:
                    case TMP_Dropdown _:
                        f.Adjustables = true;
                        break;
                    case Toggle t when t.group != null:
                        f.Adjustables = true;
                        break;
                    case InputField _:
                    case TMP_InputField _:
                        if (!UIText.IsReadOnlyField(item.Control)) f.TextFields = true;
                        break;
                }
            }
            return f;
        }

        /// <summary>Speak the current item again.</summary>
        public void SpeakCurrent()
        {
            Refresh(force: true);
            var cur = list.Current;
            if (cur == null)
            {
                A.Say(Loc.F("{0}, empty", Title));
                return;
            }
            string tip = Special.ExtraTooltip(cur) ?? (cur.IsControl ? UIText.TooltipOf(cur.Go) : null);
            string desc = Describe(cur);
            if (tip != null && desc.Contains(tip)) tip = null;
            A.Say(TextUtil.Sentences(desc, tip, list.PositionText), force: true);
        }

        /// <summary>Control C: the current item to the clipboard. A field or a share code gives its bare text.</summary>
        public void CopyCurrent()
        {
            var cur = Current;
            string text = string.Empty;
            if (cur != null)
            {
                if (cur.Control is InputField inf) text = inf.contentType == InputField.ContentType.Password ? string.Empty : inf.text;
                else if (cur.Control is TMP_InputField tinf) text = tinf.contentType == TMP_InputField.ContentType.Password ? string.Empty : tinf.text;
                else text = Describe(cur);
            }
            CopyToClipboard(text);
        }

        /// <summary>Control Shift C: everything on the screen, one item to a line.</summary>
        public void CopyAll()
        {
            var parts = AllTexts();
            CopyToClipboard(string.Join("\r\n", parts.ToArray()));
        }

        /// <summary>What Enter or a paste says on a field that only shows a value; the copy hint once only.</summary>
        internal static string ReadOnlyNote(string described)
        {
            string hint = Loc.T("Control C copies it");
            return TextUtil.Sentences(described, Loc.T("This field cannot be changed"), described != null && described.Contains(hint) ? null : hint);
        }

        internal static void CopyToClipboard(string text)
        {
            text = text ?? string.Empty;
            if (text.Length > 0) GUIUtility.systemCopyBuffer = text;
            A.Cue(text.Length > 0 ? Cue.Activate : Cue.Error);
            A.Say(ClipText.Announce(text), force: true);
        }

        /// <summary>Read every item of the panel in order.</summary>
        public void ReadAll() => A.Say(TextUtil.Sentences(AllTexts().ToArray()), force: true);

        private List<string> AllTexts()
        {
            Refresh(force: true);
            string Plain(string t) => TextUtil.Clean(t).TrimEnd('.', '!', '?', ':');
            var parts = new List<string>();
            foreach (var item in list.Items)
            {
                // A text shown twice in a row (an effect banner's ribbon and its heading) is said once.
                string d = Describe(item);
                if (parts.Count > 0 && string.Equals(Plain(parts[parts.Count - 1]), Plain(d), System.StringComparison.OrdinalIgnoreCase)) continue;
                parts.Add(d);
            }
            // A window titled by one of its own texts ("A Burgeoning Town" in "Longvale is now / A Burgeoning Town",
            // "Survival Success!") says it in its place, not twice.
            if (!string.IsNullOrEmpty(Title) && !parts.Exists(p => string.Equals(Plain(p), Plain(Title), System.StringComparison.OrdinalIgnoreCase)))
                parts.Insert(0, Title);
            return parts;
        }

        /// <summary>Focus the first control whose label contains the text (used by screens).</summary>
        public bool FocusControlNamed(string objectName)
        {
            Refresh(force: true);
            for (int i = 0; i < list.Count; i++)
            {
                if (list.Items[i].Go.name == objectName)
                {
                    list.SelectIndex(i);
                    return true;
                }
            }
            return false;
        }

        private void SetHover(GameObject go)
        {
            if (hoveredHook != null) hoveredHook.isOver = false;
            hoveredHook = null;
            if (go == null) return;
            var hook = go.GetComponent<TooltipHook>();
            if (hook != null)
            {
                hook.isOver = true;
                hoveredHook = hook;
            }
        }

        /// <summary>
        /// Texts that react to a click without being a Button (the main menu's "DLC Available" and "Wishlist our next
        /// game!" callouts): sends the click. False when nothing on or above the text takes clicks.
        /// </summary>
        private static bool ClickText(GameObject go)
        {
            if (go == null) return false;
            var dlc = go.GetComponentInParent<DLCCallout>();
            if (dlc != null) { A.Cue(Cue.Activate); dlc.OnClicked(); return true; }
            var rome = go.GetComponentInParent<RomeButtonCallout>();
            if (rome != null) { A.Cue(Cue.Activate); rome.OnClicked(); return true; }
            var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
            if (target == null || target.GetComponentInParent<Canvas>() == null || target.GetComponent<Selectable>() != null) return false;
            if (EventSystem.current == null) return false;
            A.Cue(Cue.Activate);
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            return true;
        }

        internal void Activate(UIItem item)
        {
            if (!item.IsControl)
            {
                // A long text opens in the reader, line by line.
                string raw = RawText(item);
                if (LongText.WorthReading(raw) && Game.TextReader.OpenText(Loc.T("Text reader"), raw)) return;
                // Some texts are clickable without a Button (DLC Available, Wishlist our next game): click them.
                if (ClickText(item.Go)) return;
                A.Cue(Cue.Edge);
                A.Say(Describe(item) + ", " + Loc.T("text, not a button"), force: true);
                return;
            }
            var s = item.Control;
            if (!s.interactable)
            {
                A.Cue(Cue.Error);
                // With the reason where the mod knows it (a bar has no label of its own), and the hover text, which is
                // where a train button says what a squad costs and what is missing.
                string why = Describe(item);
                string hover = Special.ExtraTooltip(item) ?? UIText.TooltipOf(item.Go);
                if (hover != null && !why.Contains(hover)) why = TextUtil.Sentences(why, hover);
                A.Say(why, force: true);
                return;
            }
            if (Game.Diplomacy.HoldBack(s)) return; // declaring war or breaking an alliance asks for a second Enter
            switch (s)
            {
                case Toggle t:
                    if (t.group != null && t.isOn && !t.group.allowSwitchOff)
                    {
                        A.Say(UIText.LabelOf(t) + ", " + Loc.T("already selected"));
                        return;
                    }
                    // A box that listens to the click itself (a ship's pick up and drop off open their amounts that way)
                    // gets a real click: changing its value would leave the game's own reaction out.
                    if (t.GetComponents<IPointerClickHandler>().Length > 1) Click(t.gameObject);
                    else t.isOn = !t.isOn;
                    A.Cue(t.isOn ? Cue.ToggleOn : Cue.ToggleOff);
                    A.Say(UIText.ValueOf(t));
                    return;
                case InputField _:
                case TMP_InputField _:
                    if (UIText.IsReadOnlyField(s))
                    {
                        // Typing would do nothing, in silence.
                        A.Cue(Cue.Edge);
                        A.Say(ReadOnlyNote(Describe(item)), force: true);
                        return;
                    }
                    BeginEdit(s);
                    return;
                case Dropdown d:
                    A.Say(Loc.F("{0}, use left and right arrows to change, {1}", UIText.LabelOf(d), UIText.ValueOf(d)));
                    return;
                case TMP_Dropdown td:
                    A.Say(Loc.F("{0}, use left and right arrows to change, {1}", UIText.LabelOf(td), UIText.ValueOf(td)));
                    return;
                case Slider sl:
                    if (UIText.IsReadOnlySlider(sl)) A.Say(Describe(item), force: true);
                    else A.Say(Loc.F("{0}, use left and right arrows to change, {1}", UIText.LabelOf(sl), UIText.ValueOf(sl)));
                    return;
            }
            A.Cue(Cue.Activate);
            SnapshotTexts();
            lastClickGroup = s.transform.parent;
            clickedGo = s.gameObject;
            clickedUntil = Time.unscaledTime + 3f;
            Click(s.gameObject);
            nextRefresh = 0f; // the click may open or close parts of the panel
        }

        private HashSet<string> textSnapshot;
        private Transform lastClickGroup;
        private GameObject clickedGo;
        private float clickedUntil;

        /// <summary>
        /// The pressed button went away with its click, also a moment later (the end of a conversation): the keyboard is on
        /// something else now, which could be anything, so it is said.
        /// </summary>
        private void SayWhereTheKeyboardWent()
        {
            if ((object)clickedGo == null) return;
            if (Time.unscaledTime > clickedUntil) clickedGo = null;
            else if (focusRequest == null && list.HasCurrent)
            {
                for (int i = 0; i < list.Count; i++) if (list.Items[i].Go == clickedGo) return;
                clickedGo = null;
                A.SayQueued(Describe(list.Current));
            }
        }
        private float changeCheckAt;

        /// <summary>Remember the panel's texts so that changes caused by a click can be spoken.</summary>
        private void SnapshotTexts()
        {
            textSnapshot = new HashSet<string>();
            if (Root == null) return;
            foreach (var r in Roots) if (r != null) foreach (var t in UIText.VisibleTexts(r)) textSnapshot.Add(TextUtil.Clean(UIText.TextOf(t)));
            changeCheckAt = Time.unscaledTime + 0.2f;
        }

        /// <summary>Called every frame: speaks texts that appeared after the last activation (e.g. new description).</summary>
        public void CheckForChanges()
        {
            if (textSnapshot == null || Time.unscaledTime < changeCheckAt) return;
            var before = textSnapshot;
            textSnapshot = null;
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            var changed = new List<string>();
            var texts = new List<Component>();
            foreach (var r in Roots) if (r != null && r.gameObject.activeInHierarchy) texts.AddRange(UIText.VisibleTexts(r));
            foreach (var t in texts)
            {
                if (t.GetComponentInParent<Selectable>() is Selectable sel && sel.gameObject == (list.Current != null ? list.Current.Go : null)) continue;
                string txt = TextUtil.Clean(UIText.TextOf(t));
                if (txt == Patch_DialogueSubtitle.LastLine || RecentlySpoken(txt)) continue; // already spoken (dialogue hook etc.)
                // Ticking counters elsewhere in the panel ("51", "273/1000") mean nothing alone; a number next to the
                // pressed control (the tax rate beside its + / - buttons) is the result of the press and is spoken.
                if (TextUtil.IsNumberOnly(txt) && !(lastClickGroup != null && t.transform.IsChildOf(lastClickGroup))) continue;
                if (!before.Contains(txt) && !changed.Contains(txt)) changed.Add(txt);
                if (changed.Count >= 4) break;
            }
            if (changed.Count > 0) A.SayQueued(TextUtil.Sentences(changed.ToArray()));
        }

        /// <summary>Simulates a full mouse click (down, up, click, submit) on a UI object.</summary>
        internal static void Click(GameObject go)
        {
            if (go == null) return;
            var es = EventSystem.current;
            var data = new PointerEventData(es)
            {
                button = PointerEventData.InputButton.Left,
                position = UIText.ScreenCenter(go.transform),
                clickCount = 1
            };
            ExecuteEvents.Execute(go, data, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(go, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go, data, ExecuteEvents.pointerUpHandler);
            bool clicked = ExecuteEvents.Execute(go, data, ExecuteEvents.pointerClickHandler);
            if (!clicked && go.GetComponent<Button>() == null) ExecuteEvents.Execute(go, new BaseEventData(es), ExecuteEvents.submitHandler);
            if (go != null) ExecuteEvents.Execute(go, data, ExecuteEvents.pointerExitHandler);
        }

        private bool Adjust(Selectable s, int steps)
        {
            if (!s.interactable) return false;
            switch (s)
            {
                case Slider sl when !UIText.IsReadOnlySlider(sl):
                {
                    float range = sl.maxValue - sl.minValue;
                    float step = sl.wholeNumbers ? Mathf.Max(1f, Mathf.Round(range / 20f)) : range / 20f;
                    if (sl.wholeNumbers && range <= 20) step = 1f;
                    float before = sl.value;
                    sl.value = Mathf.Clamp(sl.value + step * steps, sl.minValue, sl.maxValue);
                    if (Mathf.Approximately(before, sl.value)) A.Cue(Cue.Edge);
                    else A.Cue(Cue.Value, ToneSynth.PitchFor(range > 0 ? (sl.value - sl.minValue) / range : 0f) / 600f);
                    A.Say(Special.SliderValue(sl) ?? UIText.ValueOf(sl));
                    return true;
                }
                case Scrollbar sb:
                    sb.value = Mathf.Clamp01(sb.value + 0.1f * Mathf.Sign(steps));
                    A.Say(UIText.ValueOf(sb));
                    return true;
                case Dropdown d:
                {
                    int v = Mathf.Clamp(d.value + Math.Sign(steps), 0, d.options.Count - 1);
                    if (v == d.value) A.Cue(Cue.Edge);
                    else
                    {
                        d.value = v;
                        A.Cue(Cue.Navigate);
                    }
                    A.Say(UIText.ValueOf(d) + ", " + Loc.F("{0} of {1}", d.value + 1, d.options.Count));
                    return true;
                }
                case TMP_Dropdown td:
                {
                    int v = Mathf.Clamp(td.value + Math.Sign(steps), 0, td.options.Count - 1);
                    if (v == td.value) A.Cue(Cue.Edge);
                    else
                    {
                        td.value = v;
                        A.Cue(Cue.Navigate);
                    }
                    A.Say(UIText.ValueOf(td) + ", " + Loc.F("{0} of {1}", td.value + 1, td.options.Count));
                    return true;
                }
                case Toggle t when t.group != null:
                {
                    // Move selection within the radio group.
                    var group = new List<Toggle>();
                    foreach (var other in t.group.GetComponentsInChildren<Toggle>(false)) if (other.group == t.group) group.Add(other);
                    if (group.Count == 0)
                    {
                        foreach (var other in Root.GetComponentsInChildren<Toggle>(false)) if (other.group == t.group) group.Add(other);
                    }
                    group = ReadingOrder.Sort(group, g => UIText.ScreenCenter(g.transform).x, g => UIText.ScreenCenter(g.transform).y);
                    int i = group.IndexOf(t) + Math.Sign(steps);
                    if (i < 0 || i >= group.Count)
                    {
                        A.Cue(Cue.Edge);
                        return true;
                    }
                    group[i].isOn = true;
                    foreach (var it in list.Items) if (it.Go == group[i].gameObject) { list.Select(it); break; }
                    Focus(NavResult.Moved);
                    return true;
                }
            }
            return false;
        }

        private void EnsureVisible(GameObject go)
        {
            var scroll = go.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content == null || scroll.viewport == null) return;
            var target = go.transform as RectTransform;
            if (target == null) return;
            Canvas.ForceUpdateCanvases();
            Vector3 targetLocal = scroll.content.InverseTransformPoint(target.position);
            float contentH = scroll.content.rect.height, viewH = scroll.viewport.rect.height;
            if (scroll.vertical && contentH > viewH)
            {
                float y = -targetLocal.y - target.rect.height * 0.5f;
                float norm = 1f - Mathf.Clamp01((y - viewH * 0.3f) / (contentH - viewH));
                scroll.verticalNormalizedPosition = norm;
            }
        }

        private void BeginEdit(Selectable s)
        {
            editSession = new InputEditSession(s);
            A.Cue(Cue.Open);
            string label = Special.FieldName(s) ?? UIText.LabelOf(s);
            if (label.IndexOf("Input Field", StringComparison.OrdinalIgnoreCase) >= 0 || label == UIText.Unlabelled) label = Loc.T("text");
            A.Say(Loc.F("Editing {0}. {1}. Type, then press Enter to confirm or Escape to cancel.", label, editSession.Text.Length > 0 ? editSession.Text : UIText.Blank)
                + (Plugin.CfgHints.Value ? " " + Loc.T("Control V pastes.") : string.Empty));
        }

        /// <summary>
        /// A text field of this screen was focused by the game itself (e.g. the kingdom name "Edit" button):
        /// take it over as a normal edit so typing is echoed and Enter / Escape work. False if it is not ours.
        /// </summary>
        internal bool AdoptFocusedField(Selectable s)
        {
            if (s == null || editSession != null || Root == null || UIText.IsReadOnlyField(s)) return false;
            bool inside = false;
            foreach (var r in Roots) if (r != null && s.transform.IsChildOf(r)) inside = true;
            if (!inside) return false;
            BeginEdit(s);
            return true;
        }

        internal void EndEdit(bool announce)
        {
            if (editSession == null) return;
            // Forget the session first: if finishing it throws (its window was destroyed meanwhile), the mod must not
            // stay "editing" for ever with every key swallowed.
            var session = editSession;
            editSession = null;
            session.Finish();
            if (announce) A.Cue(Cue.Close);
        }
    }

    /// <summary>Lets the player type into an input field, echoing typed and deleted characters.</summary>
    internal sealed class InputEditSession
    {
        private readonly Selectable field;
        private readonly string original;
        private string lastText;
        private readonly int startFrame;

        public InputEditSession(Selectable s)
        {
            field = s;
            startFrame = Time.frameCount;
            original = Text;
            lastText = original;
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(s.gameObject);
            if (s is InputField inf) inf.ActivateInputField();
            if (s is TMP_InputField tinf) tinf.ActivateInputField();
            GameState.inst.AlphaNumericHotkeysEnabled = false;
        }

        public string Text
        {
            get
            {
                if (field is InputField inf) return inf.text ?? string.Empty;
                if (field is TMP_InputField tinf) return tinf.text ?? string.Empty;
                return string.Empty;
            }
            set
            {
                if (field is InputField inf) inf.text = value;
                if (field is TMP_InputField tinf) tinf.text = value;
            }
        }

        private int selStart, selEnd, holdFrames;
        private string holdText, holdCopy;

        private bool Password => (field is InputField inf && inf.contentType == InputField.ContentType.Password)
            || (field is TMP_InputField tinf && tinf.contentType == TMP_InputField.ContentType.Password);

        private bool DigitsOnly => (field is InputField inf && inf.contentType == InputField.ContentType.IntegerNumber)
            || (field is TMP_InputField tinf && tinf.contentType == TMP_InputField.ContentType.IntegerNumber);

        private int Limit => field is InputField inf ? inf.characterLimit : field is TMP_InputField tinf ? tinf.characterLimit : 0;

        private void ReadSelection()
        {
            if (field is InputField inf) { selStart = inf.selectionAnchorPosition; selEnd = inf.selectionFocusPosition; }
            else if (field is TMP_InputField tinf) { selStart = tinf.selectionStringAnchorPosition; selEnd = tinf.selectionStringFocusPosition; }
        }

        private void SetCaret(int at)
        {
            if (field is InputField inf) { inf.caretPosition = at; inf.selectionAnchorPosition = at; inf.selectionFocusPosition = at; }
            else if (field is TMP_InputField tinf) { tinf.stringPosition = at; tinf.selectionStringAnchorPosition = at; tinf.selectionStringFocusPosition = at; }
        }

        /// <summary>
        /// Control V and Control C. Unity's fields handle these keys themselves, before or after the mod depending on
        /// the frame, and their copy empties the clipboard when nothing is selected: so the mod works from the text
        /// and selection of the frame before, and holds its result for a few frames.
        /// </summary>
        private bool Clipboard()
        {
            if (holdFrames > 0)
            {
                holdFrames--;
                if (holdText != null && Text != holdText) { Text = holdText; SetCaret(selEnd); }
                if (holdCopy != null && GUIUtility.systemCopyBuffer != holdCopy) GUIUtility.systemCopyBuffer = holdCopy;
                if (holdFrames == 0) { holdText = null; holdCopy = null; }
            }
            if (!KInput.Ctrl || KInput.Alt || KInput.Shift) return false;
            if (KInput.Down(KeyCode.V) && UIText.IsReadOnlyField(field))
            {
                // No session starts on such a field; should one be adopted all the same, it stays as the game has it.
                KInput.Consume(KeyCode.V);
                A.Cue(Cue.Error);
                A.Say(UINavigator.ReadOnlyNote(null), force: true);
                return true;
            }
            if (KInput.Down(KeyCode.V))
            {
                KInput.Consume(KeyCode.V);
                string clip = holdCopy ?? GUIUtility.systemCopyBuffer;
                string text = ClipText.Insert(lastText, selStart, selEnd, clip, Limit, DigitsOnly, out string pasted, out int caret);
                Text = text;
                SetCaret(caret);
                selStart = selEnd = caret;
                lastText = holdText = text;
                holdFrames = 3;
                A.Cue(pasted.Length > 0 ? Cue.Activate : Cue.Error);
                A.Say(ClipText.AnnouncePaste(pasted, text, string.IsNullOrEmpty(clip)), force: true);
                return true;
            }
            if (KInput.Down(KeyCode.C))
            {
                KInput.Consume(KeyCode.C);
                string copy = Password ? string.Empty : lastText;
                if (copy.Length > 0)
                {
                    GUIUtility.systemCopyBuffer = holdCopy = copy;
                    holdFrames = 3;
                }
                A.Cue(copy.Length > 0 ? Cue.Activate : Cue.Error);
                A.Say(ClipText.Announce(copy), force: true);
                return true;
            }
            return false;
        }

        /// <summary>Returns true while the session wants all keys.</summary>
        public bool Update(UINavigator nav)
        {
            if (field == null || !field.gameObject.activeInHierarchy)
            {
                nav.EndEdit(true);
                return false;
            }
            if (Time.frameCount != startFrame && Clipboard()) return true;
            if (holdFrames == 0) ReadSelection();
            string now = Text;
            if (now != lastText)
            {
                if (now.Length > lastText.Length && now.StartsWith(lastText)) A.Say(now.Substring(lastText.Length), force: true);
                else if (now.Length < lastText.Length && lastText.StartsWith(now)) A.Say(Loc.F("{0} deleted", lastText.Substring(now.Length)), force: true);
                else A.Say(now.Length > 0 ? now : UIText.Blank, force: true);
                lastText = now;
            }
            if (Time.frameCount == startFrame) return true;
            if (KInput.Down(KeyCode.Return) || KInput.Down(KeyCode.KeypadEnter))
            {
                KInput.Consume(KeyCode.Return);
                string committed = Text;
                nav.EndEdit(true);
                A.Say(Loc.F("Confirmed {0}", committed.Length > 0 ? committed : UIText.Blank));
                return true;
            }
            if (KInput.Down(KeyCode.Escape))
            {
                KInput.Consume(KeyCode.Escape);
                Text = original;
                nav.EndEdit(true);
                A.Say(Loc.F("Cancelled, {0}", original.Length > 0 ? original : UIText.Blank));
                return true;
            }
            if (KInput.Down(KeyCode.F2) || (KInput.Down(KeyCode.UpArrow) || KInput.Down(KeyCode.DownArrow)))
            {
                A.Say(Text.Length > 0 ? Text : UIText.Blank, force: true);
            }
            return true;
        }

        public void Finish()
        {
            if (field == null)
            {
                // The field (or its whole window) was destroyed while typing: only give the game its hotkeys back.
                if (GameState.inst != null) GameState.inst.AlphaNumericHotkeysEnabled = true;
                return;
            }
            if (field is InputField inf)
            {
                inf.DeactivateInputField();
            }
            if (field is TMP_InputField tinf)
            {
                tinf.DeactivateInputField();
            }
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == field.gameObject) es.SetSelectedGameObject(null);
            if (GameState.inst != null) GameState.inst.AlphaNumericHotkeysEnabled = true;
        }
    }
}

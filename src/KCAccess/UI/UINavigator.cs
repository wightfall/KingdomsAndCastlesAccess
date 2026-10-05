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
            if (changed) textSnapshot = null;
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
                    string l = UIText.LabelOf(list.Items[i].Control).ToLowerInvariant();
                    if (l == "back" || l == "close" || l == "x" || l == "cancel") continue;
                    if (list.Items[i].Control is Slider rs && UIText.IsReadOnlySlider(rs)) continue;
                    firstControl = i;
                    break;
                }
                if (firstControl >= 0) list.SelectIndex(firstControl);
            }
            if (announce) AnnounceScreen(intro);
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
            string current = list.HasCurrent ? Describe(list.Current) : "no items";
            A.Say(TextUtil.Sentences(Title, intro, current));
        }

        private Func<GameObject, bool> focusRequest;
        private float focusRequestUntil;

        /// <summary>Move focus to the first item matching the predicate as soon as it appears (within 3 seconds).</summary>
        public void RequestFocus(Func<GameObject, bool> match)
        {
            focusRequest = match;
            focusRequestUntil = Time.unscaledTime + 3f;
            nextRefresh = 0f;
        }

        public void Refresh(bool force = false)
        {
            if (Root == null) return;
            if (focusRequest != null && Time.unscaledTime > focusRequestUntil) focusRequest = null;
            if (!force && Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + (focusRequest != null ? 0.05f : 0.25f);
            var all = new List<UIItem>();
            foreach (var r in Roots) if (r != null && r.gameObject.activeInHierarchy) all.AddRange(Collect(r));
            list.SetItems(all, keepFocus: true);
            if (focusRequest != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var go = list.Items[i].Go;
                    if (go != null && list.Items[i].IsControl && focusRequest(go))
                    {
                        list.SelectIndex(i);
                        focusRequest = null;
                        SetHover(go);
                        break;
                    }
                }
            }
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
                if (last != null && !last.IsControl && !item.IsControl && Mathf.Abs(last.Center.y - item.Center.y) <= 14f
                    && last.Go.transform.parent == item.Go.transform.parent)
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
                bool dup = (i > 0 && merged[i - 1].IsControl && SameLabel(UIText.LabelOf(merged[i - 1].Control), txt))
                    || (i + 1 < merged.Count && merged[i + 1].IsControl && SameLabel(UIText.LabelOf(merged[i + 1].Control), txt));
                if (dup) merged.RemoveAt(i);
            }
            AddContextToDuplicates(merged);
            return merged;
        }

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
                items.Add(new UIItem { Control = s, Go = s.gameObject, Center = UIText.ScreenCenter(s.transform) });
                // Texts inside a control are its label; nested controls (e.g. toggles inside a list row) are still visited.
                for (int i = 0; i < t.childCount; i++) WalkNested(t.GetChild(i), root, items);
                return;
            }
            if (IncludeTexts)
            {
                foreach (var c in t.GetComponents<Component>())
                {
                    if (!UIText.IsTextComponent(c)) continue;
                    if (!UIText.IsTextVisible(c) || !TextUtil.HasContent(UIText.TextOf(c))) continue;
                    if (c.GetComponentInParent<InputField>() != null || c.GetComponentInParent<TMP_InputField>() != null) continue;
                    items.Add(new UIItem { Texts = new List<Component> { c }, Go = c.gameObject, Center = UIText.ScreenCenter(c.transform) });
                    break;
                }
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
            string special = Special.Describe(item);
            if (special != null) return special;
            if (!item.IsControl)
            {
                string t = UIText.JoinTexts(item.Texts);
                if (IsNumberish(t))
                {
                    string res = Special.ResourceIconName(item.Go.transform);
                    if (res != null) return res + " " + t;
                }
                return t;
            }
            var s = item.Control;
            // Read-only fields and bars are really just text.
            if (!s.interactable && (s is InputField || s is TMP_InputField)) return UIText.ValueOf(s) == "blank" ? UIText.LabelOf(s) : UIText.ValueOf(s);
            if (s is Slider ro && UIText.IsReadOnlySlider(ro)) return TextUtil.Join(", ", UIText.LabelOf(ro), UIText.ValueOf(ro));
            string label = UIText.LabelOf(s);
            if (!string.IsNullOrEmpty(item.Context) && item.Context != label) label = item.Context + ", " + label;
            // Icon-only rows ("20" next to a wood icon): add the resource name.
            if (IsNumberish(label) || label == "unlabelled")
            {
                string res = Special.ResourceIconName(s.transform);
                if (res != null) label = res + (label == "unlabelled" ? string.Empty : " " + label);
            }
            string role = UIText.RoleOf(s);
            string value = UIText.ValueOf(s);
            var parts = new List<string> { label };
            if (role != "button" || !s.interactable) parts.Add(role);
            if (!string.IsNullOrEmpty(value) && value != label) parts.Add(value);
            if (!s.interactable) parts.Add("unavailable");
            return TextUtil.Join(", ", parts);
        }

        public UIItem Current => list.Current;

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
            string tip = cur.IsControl ? UIText.TooltipOf(cur.Go) : null;
            string desc = Describe(cur);
            if (tip != null && !desc.Contains(tip)) desc = TextUtil.Sentences(desc, tip);
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
                        f.TextFields = true;
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
                A.Say(Title + ", empty");
                return;
            }
            string tip = cur.IsControl ? UIText.TooltipOf(cur.Go) : null;
            string desc = Describe(cur);
            if (tip != null && desc.Contains(tip)) tip = null;
            A.Say(TextUtil.Sentences(desc, tip, list.PositionText), force: true);
        }

        /// <summary>Read every item of the panel in order.</summary>
        public void ReadAll()
        {
            Refresh(force: true);
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Title)) parts.Add(Title);
            foreach (var item in list.Items) parts.Add(Describe(item));
            A.Say(TextUtil.Sentences(parts.ToArray()), force: true);
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

        internal void Activate(UIItem item)
        {
            if (!item.IsControl)
            {
                A.Cue(Cue.Edge);
                A.Say(Describe(item) + ", text, not a button", force: true);
                return;
            }
            var s = item.Control;
            if (!s.interactable)
            {
                A.Cue(Cue.Error);
                A.Say(UIText.LabelOf(s) + ", unavailable");
                return;
            }
            switch (s)
            {
                case Toggle t:
                    if (t.group != null && t.isOn && !t.group.allowSwitchOff)
                    {
                        A.Say(UIText.LabelOf(t) + ", already selected");
                        return;
                    }
                    t.isOn = !t.isOn;
                    A.Cue(t.isOn ? Cue.ToggleOn : Cue.ToggleOff);
                    A.Say(UIText.ValueOf(t));
                    return;
                case InputField _:
                case TMP_InputField _:
                    BeginEdit(s);
                    return;
                case Dropdown d:
                    A.Say(UIText.LabelOf(d) + ", use left and right arrows to change, " + UIText.ValueOf(d));
                    return;
                case TMP_Dropdown td:
                    A.Say(UIText.LabelOf(td) + ", use left and right arrows to change, " + UIText.ValueOf(td));
                    return;
                case Slider sl:
                    if (UIText.IsReadOnlySlider(sl)) A.Say(Describe(item), force: true);
                    else A.Say(UIText.LabelOf(sl) + ", use left and right arrows to change, " + UIText.ValueOf(sl));
                    return;
            }
            A.Cue(Cue.Activate);
            SnapshotTexts();
            lastClickGroup = s.transform.parent;
            Click(s.gameObject);
            nextRefresh = 0f; // the click may open or close parts of the panel
        }

        private HashSet<string> textSnapshot;
        private Transform lastClickGroup;
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
                    A.Say(UIText.ValueOf(sl));
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
                    A.Say(UIText.ValueOf(d) + ", " + (d.value + 1) + " of " + d.options.Count);
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
                    A.Say(UIText.ValueOf(td) + ", " + (td.value + 1) + " of " + td.options.Count);
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
            string label = UIText.LabelOf(s);
            if (label.IndexOf("Input Field", StringComparison.OrdinalIgnoreCase) >= 0 || label == "unlabelled") label = "text";
            A.Say("Editing " + label + ". " + (editSession.Text.Length > 0 ? editSession.Text : "blank") + ". Type, then press Enter to confirm or Escape to cancel.");
        }

        /// <summary>
        /// A text field of this screen was focused by the game itself (e.g. the kingdom name "Edit" button):
        /// take it over as a normal edit so typing is echoed and Enter / Escape work. False if it is not ours.
        /// </summary>
        internal bool AdoptFocusedField(Selectable s)
        {
            if (s == null || editSession != null || Root == null) return false;
            bool inside = false;
            foreach (var r in Roots) if (r != null && s.transform.IsChildOf(r)) inside = true;
            if (!inside) return false;
            BeginEdit(s);
            return true;
        }

        internal void EndEdit(bool announce)
        {
            if (editSession == null) return;
            editSession.Finish();
            editSession = null;
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

        /// <summary>Returns true while the session wants all keys.</summary>
        public bool Update(UINavigator nav)
        {
            if (field == null || !field.gameObject.activeInHierarchy)
            {
                nav.EndEdit(true);
                return false;
            }
            string now = Text;
            if (now != lastText)
            {
                if (now.Length > lastText.Length && now.StartsWith(lastText)) A.Say(now.Substring(lastText.Length), force: true);
                else if (now.Length < lastText.Length && lastText.StartsWith(now)) A.Say(lastText.Substring(now.Length) + " deleted", force: true);
                else A.Say(now.Length > 0 ? now : "blank", force: true);
                lastText = now;
            }
            if (Time.frameCount == startFrame) return true;
            if (KInput.Down(KeyCode.Return) || KInput.Down(KeyCode.KeypadEnter))
            {
                KInput.Consume(KeyCode.Return);
                string committed = Text;
                nav.EndEdit(true);
                A.Say("Confirmed " + (committed.Length > 0 ? committed : "blank"));
                return true;
            }
            if (KInput.Down(KeyCode.Escape))
            {
                KInput.Consume(KeyCode.Escape);
                Text = original;
                nav.EndEdit(true);
                A.Say("Cancelled, " + (original.Length > 0 ? original : "blank"));
                return true;
            }
            if (KInput.Down(KeyCode.F2) || (KInput.Down(KeyCode.UpArrow) || KInput.Down(KeyCode.DownArrow)))
            {
                A.Say(Text.Length > 0 ? Text : "blank", force: true);
            }
            return true;
        }

        public void Finish()
        {
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

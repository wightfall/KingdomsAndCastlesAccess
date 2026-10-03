using System.Collections.Generic;
using I2.Loc;
using KCAccess.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KCAccess.UI
{
    /// <summary>Reads labels, values and visibility of uGUI / TextMeshPro elements.</summary>
    internal static class UIText
    {
        private static readonly List<Component> scratch = new List<Component>();
        private static readonly Vector3[] corners = new Vector3[4];

        /// <summary>Raw string of a Text or TMP_Text component (null for other components).</summary>
        internal static string TextOf(Component c)
        {
            if (c is TMP_Text tmp)
            {
                // SetText(format, number) – used for most counters in this game – does not update .text,
                // so prefer what is actually rendered unless the text contains inline sprites (lost when parsed).
                string raw = tmp.text;
                if (raw == null || raw.IndexOf("<sprite", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    string parsed = null;
                    try
                    {
                        parsed = tmp.GetParsedText();
                    }
                    catch
                    {
                        // Not laid out yet.
                    }
                    if (!string.IsNullOrEmpty(parsed)) return parsed;
                }
                return raw;
            }
            if (c is Text t) return t.text;
            return null;
        }

        internal static bool IsTextComponent(Component c) => c is TMP_Text || c is Text;

        /// <summary>Is the text actually drawn (enabled, not transparent)?</summary>
        internal static bool IsTextVisible(Component c)
        {
            if (c is Graphic g)
            {
                if (!g.enabled || !g.gameObject.activeInHierarchy) return false;
                if (g.color.a < 0.05f) return false;
                if (c is TMP_Text tmp && tmp.alpha < 0.05f) return false;
                return IsVisible(g.gameObject);
            }
            return false;
        }

        /// <summary>All visible text components below root, skipping input-field placeholders.</summary>
        internal static List<Component> VisibleTexts(Transform root)
        {
            var result = new List<Component>();
            if (root == null) return result;
            scratch.Clear();
            root.GetComponentsInChildren(false, scratch);
            foreach (var c in scratch)
            {
                if (!IsTextComponent(c)) continue;
                if (!IsTextVisible(c)) continue;
                if (IsPlaceholder(c)) continue;
                if (!TextUtil.HasContent(TextOf(c))) continue;
                result.Add(c);
            }
            return result;
        }

        private static bool IsPlaceholder(Component c)
        {
            var input = c.GetComponentInParent<InputField>();
            if (input != null && input.placeholder == c as Graphic) return true;
            var tinput = c.GetComponentInParent<TMP_InputField>();
            if (tinput != null && tinput.placeholder == c as Graphic) return true;
            return false;
        }

        /// <summary>Checks active state, CanvasGroup alpha along the parent chain and a non-zero size.</summary>
        internal static bool IsVisible(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return false;
            Transform t = go.transform;
            while (t != null)
            {
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null && cg.enabled)
                {
                    if (cg.alpha < 0.05f) return false;
                    if (cg.ignoreParentGroups) break;
                }
                var canvas = t.GetComponent<Canvas>();
                if (canvas != null && !canvas.enabled) return false;
                t = t.parent;
            }
            var rt = go.transform as RectTransform;
            if (rt != null)
            {
                var r = rt.rect;
                if (Mathf.Abs(r.width) < 0.5f || Mathf.Abs(r.height) < 0.5f) return false;
                if (Mathf.Abs(rt.lossyScale.x) < 1e-6f || Mathf.Abs(rt.lossyScale.y) < 1e-6f) return false;
            }
            return true;
        }

        /// <summary>True when at least part of the element is inside the screen (slid-out panels are not).</summary>
        internal static bool IsOnScreen(Transform t)
        {
            var rt = t as RectTransform;
            if (rt == null) return true;
            rt.GetWorldCorners(corners);
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = canvas.rootCanvas.worldCamera != null ? canvas.rootCanvas.worldCamera : Camera.main;
                if (canvas.rootCanvas.renderMode == RenderMode.WorldSpace) return true;
            }
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = cam != null ? RectTransformUtility.WorldToScreenPoint(cam, corners[i]) : (Vector2)corners[i];
                minX = Mathf.Min(minX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x);
                maxY = Mathf.Max(maxY, p.y);
            }
            return maxX > 2f && maxY > 2f && minX < Screen.width - 2f && minY < Screen.height - 2f;
        }

        /// <summary>Centre of the element in screen pixels (Y up).</summary>
        internal static Vector2 ScreenCenter(Transform t)
        {
            var rt = t as RectTransform;
            if (rt == null) return Vector2.zero;
            rt.GetWorldCorners(corners);
            Vector3 world = (corners[0] + corners[2]) * 0.5f;
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas = canvas.rootCanvas;
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                    if (cam != null) return RectTransformUtility.WorldToScreenPoint(cam, world);
                }
            }
            return world;
        }

        /// <summary>Spoken label for a selectable.</summary>
        private static readonly HashSet<string> GenericWords = new HashSet<string> { "accept", "on", "off", "yes", "no", "enabled", "disabled", "toggle", "label", "new text", "text" };

        /// <summary>Text drawn on the control's direct parent (e.g. "Music Volume" on the object holding the slider).</summary>
        private static string ParentOwnText(Transform t)
        {
            var parent = t.parent;
            if (parent == null || parent.GetComponent<Selectable>() != null) return null;
            foreach (var c in parent.GetComponents<Component>())
            {
                if (!IsTextComponent(c) || !IsTextVisible(c)) continue;
                string txt = TextUtil.Clean(TextOf(c));
                if (TextUtil.HasContent(txt) && txt.Length < 120) return txt;
            }
            return null;
        }

        internal static string LabelOf(Selectable s)
        {
            if (s == null) return string.Empty;
            if (!(s is Button))
            {
                string own = ParentOwnText(s.transform);
                if (!string.IsNullOrEmpty(own)) return own;
            }
            // 1. Text inside the control (excluding dropdown captions, which are the value).
            var parts = new List<string>();
            scratch.Clear();
            s.GetComponentsInChildren(false, scratch);
            Graphic dropdownCaption = CaptionOf(s);
            foreach (var c in scratch)
            {
                if (!IsTextComponent(c)) continue;
                if (c == dropdownCaption) continue;
                if (IsPlaceholder(c) && !(s is InputField || s is TMP_InputField)) continue;
                if (s is InputField inf && inf.textComponent == c as Graphic) continue;
                if (s is TMP_InputField tinf && tinf.textComponent == c as Graphic) continue;
                if (!IsTextVisible(c)) continue;
                string txt = TextUtil.Clean(TextOf(c));
                if (TextUtil.HasContent(txt) && !parts.Contains(txt)) parts.Add(txt);
            }
            if (parts.Count == 1 && !(s is Button) && GenericWords.Contains(parts[0].ToLowerInvariant()))
            {
                string sib = SiblingLabel(s.transform);
                if (!string.IsNullOrEmpty(sib)) return sib;
            }
            if (parts.Count > 0) return string.Join(", ", parts.ToArray());

            // 2. A control nested inside another control takes the outer control's label.
            var outer = s.transform.parent != null ? s.transform.parent.GetComponentInParent<Selectable>() : null;
            if (outer != null && outer.enabled)
            {
                string outerLabel = LabelOf(outer);
                if (!string.IsNullOrEmpty(outerLabel) && outerLabel != "unlabelled") return outerLabel;
            }

            // 3. A text right next to the control (toggle / slider / dropdown / input labels).
            if (!(s is Button))
            {
                string sibling = SiblingLabel(s.transform);
                if (!string.IsNullOrEmpty(sibling)) return sibling;
            }

            // 3. Tooltip text.
            var hook = s.GetComponent<TooltipHook>();
            if (hook != null && TextUtil.HasContent(hook.toolTipText) && hook.toolTipText != "missing tip") return TextUtil.Clean(hook.toolTipText);

            // 4. Name of the method the button calls (OnClickedDiscord -> "Discord").
            if (s is Button b)
            {
                string m = MethodLabel(b.onClick);
                if (!string.IsNullOrEmpty(m)) return m;
            }
            if (s is Toggle tg)
            {
                string m = MethodLabel(tg.onValueChanged);
                if (!string.IsNullOrEmpty(m)) return m;
            }

            // 5. Icon sprite name (social media buttons etc.).
            var img = s.targetGraphic as Image ?? s.GetComponent<Image>();
            if (img != null && img.sprite != null)
            {
                string sprite = SpriteLabel(img.sprite.name);
                if (!string.IsNullOrEmpty(sprite)) return sprite;
            }
            foreach (var childImg in s.GetComponentsInChildren<Image>(false))
            {
                if (childImg == img || childImg.sprite == null) continue;
                string sprite = SpriteLabel(childImg.sprite.name);
                if (!string.IsNullOrEmpty(sprite)) return sprite;
            }

            // 6. Object name.
            return NameLabel(s.gameObject.name);
        }

        /// <summary>Turns sprite names like "icon_discord" into a label; ignores generic UI art.</summary>
        internal static string SpriteLabel(string sprite)
        {
            if (string.IsNullOrEmpty(sprite)) return null;
            string lower = sprite.ToLowerInvariant();
            string[] generic = { "uisprite", "background", "knob", "checkmark", "dropdownarrow", "inputfield", "panel", "button", "frame", "border", "white", "square", "round", "gradient", "slice", "box", "bg" };
            foreach (var g in generic) if (lower.StartsWith(g) || lower == g) return null;
            string n = sprite;
            foreach (var prefix in new[] { "icon_", "Icon_", "ico_", "ui_", "UI_" }) if (n.StartsWith(prefix)) n = n.Substring(prefix.Length);
            foreach (var suffix in new[] { "_icon", "Icon", "_logo", "Logo" }) if (n.EndsWith(suffix) && n.Length > suffix.Length) n = n.Substring(0, n.Length - suffix.Length);
            return TextUtil.Humanize(n);
        }

        internal static string NameLabel(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            string n = name.Replace("(Clone)", "");
            int paren = n.IndexOf(" (");
            if (paren > 0) n = n.Substring(0, paren);
            n = n.Replace("Button", "").Replace("Btn", "").Trim();
            if (n.Length == 0) return "unlabelled";
            return TextUtil.Humanize(n);
        }

        private static string MethodLabel(UnityEventBase ev)
        {
            if (ev == null) return null;
            for (int i = 0; i < ev.GetPersistentEventCount(); i++)
            {
                string m = ev.GetPersistentMethodName(i);
                if (string.IsNullOrEmpty(m) || m == "SetActive" || m == "PlayUiSelect") continue;
                string words = MethodToWords(m);
                if (words == "Click" || words == "Press" || words == "Select" || words == "Clicked" || words.Length == 0) continue;
                return TextUtil.Humanize(words);
            }
            return null;
        }

        /// <summary>"OnClickedBackToTopLevel" → "BackToTopLevel", "PrevClicked" → "Previous".</summary>
        internal static string MethodToWords(string m)
        {
            foreach (var prefix in new[] { "OnClicked", "OnClick", "Clicked", "On" })
            {
                if (m.StartsWith(prefix) && m.Length > prefix.Length)
                {
                    m = m.Substring(prefix.Length);
                    break;
                }
            }
            foreach (var suffix in new[] { "Clicked", "Click", "Pressed", "Button", "Btn" })
            {
                if (m.EndsWith(suffix) && m.Length > suffix.Length)
                {
                    m = m.Substring(0, m.Length - suffix.Length);
                    break;
                }
            }
            if (m == "Prev") m = "Previous";
            return m;
        }

        private static string SiblingLabel(Transform t)
        {
            Transform parent = t.parent;
            if (parent == null) return null;
            // Look at siblings first, then the parent's siblings, for a short text.
            for (int level = 0; level < 2 && parent != null; level++)
            {
                Transform self = level == 0 ? t : t.parent;
                int idx = self.GetSiblingIndex();
                string best = null;
                int bestDist = int.MaxValue;
                for (int i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    if (child == self || !child.gameObject.activeInHierarchy) continue;
                    if (child.GetComponentInChildren<Selectable>() != null) continue;
                    var texts = VisibleTexts(child);
                    if (texts.Count == 0) continue;
                    int dist = Mathf.Abs(i - idx);
                    if (dist < bestDist)
                    {
                        string joined = JoinTexts(texts);
                        if (joined.Length > 0 && joined.Length < 120)
                        {
                            best = joined;
                            bestDist = dist;
                        }
                    }
                }
                if (best != null) return best;
                parent = parent.parent;
            }
            return null;
        }

        internal static string JoinTexts(List<Component> texts)
        {
            var parts = new List<string>();
            foreach (var c in texts)
            {
                string txt = TextUtil.Clean(TextOf(c));
                if (TextUtil.HasContent(txt) && !parts.Contains(txt)) parts.Add(txt);
            }
            return string.Join(" ", parts.ToArray());
        }

        private static Graphic CaptionOf(Selectable s)
        {
            if (s is Dropdown d) return d.captionText;
            if (s is TMP_Dropdown td) return td.captionText;
            return null;
        }

        /// <summary>Current value / state, e.g. "checked", "50 percent", "Normal".</summary>
        internal static string ValueOf(Selectable s)
        {
            switch (s)
            {
                case Toggle t:
                    if (t.group != null) return t.isOn ? "selected" : "not selected";
                    return t.isOn ? "checked" : "not checked";
                case Slider sl:
                    return SliderValue(sl);
                case Scrollbar sb:
                    return TextUtil.Percent(sb.value);
                case Dropdown d:
                    return d.options.Count > d.value && d.value >= 0 ? TextUtil.Clean(d.options[d.value].text) : string.Empty;
                case TMP_Dropdown td:
                    return td.options.Count > td.value && td.value >= 0 ? TextUtil.Clean(td.options[td.value].text) : string.Empty;
                case InputField inf:
                    return string.IsNullOrEmpty(inf.text) ? "blank" : (inf.contentType == InputField.ContentType.Password ? "password" : inf.text);
                case TMP_InputField tinf:
                    return string.IsNullOrEmpty(tinf.text) ? "blank" : (tinf.contentType == TMP_InputField.ContentType.Password ? "password" : tinf.text);
            }
            return string.Empty;
        }

        internal static string SliderValue(Slider sl)
        {
            // Prefer a number shown next to the slider (e.g. "75%" or "Large").
            var parent = sl.transform.parent;
            if (sl.wholeNumbers && sl.maxValue - sl.minValue <= 100)
            {
                return ((int)sl.value).ToString();
            }
            float range = sl.maxValue - sl.minValue;
            return range > 0 ? TextUtil.Percent((sl.value - sl.minValue) / range) : sl.value.ToString("0.##");
        }

        internal static string RoleOf(Selectable s)
        {
            switch (s)
            {
                case Toggle t: return t.group != null ? "radio button" : "check box";
                case Slider sl: return sl.interactable ? "slider" : "progress bar";
                case Scrollbar _: return "scroll bar";
                case Dropdown _: return "combo box";
                case TMP_Dropdown _: return "combo box";
                case InputField _: return "edit";
                case TMP_InputField _: return "edit";
                default: return "button";
            }
        }

        /// <summary>Tooltip text for an element (or its parent), used as extra description.</summary>
        internal static string TooltipOf(GameObject go)
        {
            var hook = go.GetComponent<TooltipHook>() ?? go.GetComponentInParent<TooltipHook>();
            if (hook == null) return null;
            string tip = hook.toolTipText;
            if (!TextUtil.HasContent(tip) || tip == "missing tip" || tip.StartsWith("<localization")) return null;
            return TextUtil.Clean(tip);
        }

        /// <summary>Localization term of a text, used to recognise windows independent of language.</summary>
        internal static string TermOf(Component c)
        {
            var loc = c.GetComponent<Localize>();
            return loc != null ? loc.Term : null;
        }
    }
}

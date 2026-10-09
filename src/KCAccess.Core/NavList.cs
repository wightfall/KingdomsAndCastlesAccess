using System;
using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>Result of a navigation step so the caller can play the right sound cue.</summary>
    public enum NavResult
    {
        Moved,
        Wrapped,
        HitEdge,
        Empty
    }

    /// <summary>
    /// A keyboard-navigable list: next/previous with optional wrapping, Home/End,
    /// and first-letter type-ahead. Pure logic; the items are labelled by a delegate.
    /// </summary>
    public sealed class NavList<T>
    {
        private readonly List<T> items = new List<T>();
        private readonly Func<T, string> label;

        public NavList(Func<T, string> label, bool wrap = true)
        {
            this.label = label ?? throw new ArgumentNullException(nameof(label));
            Wrap = wrap;
        }

        public bool Wrap { get; set; }

        public int Index { get; private set; } = -1;

        public int Count => items.Count;

        public IReadOnlyList<T> Items => items;

        public T Current => Index >= 0 && Index < items.Count ? items[Index] : default(T);

        public bool HasCurrent => Index >= 0 && Index < items.Count;

        public string CurrentLabel => HasCurrent ? label(Current) : string.Empty;

        /// <summary>Replace the items, trying to keep the same item focused.</summary>
        public void SetItems(IEnumerable<T> newItems, bool keepFocus = true)
        {
            T previous = Current;
            bool had = HasCurrent;
            items.Clear();
            items.AddRange(newItems);
            if (items.Count == 0)
            {
                Index = -1;
                return;
            }
            if (keepFocus && had)
            {
                int idx = items.IndexOf(previous);
                if (idx >= 0)
                {
                    Index = idx;
                    return;
                }
                Index = Math.Min(Math.Max(Index, 0), items.Count - 1);
                return;
            }
            Index = 0;
        }

        public void Clear()
        {
            items.Clear();
            Index = -1;
        }

        public bool Select(T item)
        {
            int idx = items.IndexOf(item);
            if (idx < 0) return false;
            Index = idx;
            return true;
        }

        public void SelectIndex(int index)
        {
            if (items.Count == 0)
            {
                Index = -1;
                return;
            }
            Index = Math.Min(Math.Max(index, 0), items.Count - 1);
        }

        public NavResult Next() => Step(1);

        public NavResult Previous() => Step(-1);

        public NavResult Step(int delta)
        {
            if (items.Count == 0)
            {
                Index = -1;
                return NavResult.Empty;
            }
            if (Index < 0)
            {
                Index = delta >= 0 ? 0 : items.Count - 1;
                return NavResult.Moved;
            }
            int target = Index + delta;
            if (target >= items.Count)
            {
                if (!Wrap)
                {
                    Index = items.Count - 1;
                    return NavResult.HitEdge;
                }
                Index = target % items.Count;
                return NavResult.Wrapped;
            }
            if (target < 0)
            {
                if (!Wrap)
                {
                    Index = 0;
                    return NavResult.HitEdge;
                }
                Index = ((target % items.Count) + items.Count) % items.Count;
                return NavResult.Wrapped;
            }
            Index = target;
            return NavResult.Moved;
        }

        public NavResult First()
        {
            if (items.Count == 0) return NavResult.Empty;
            Index = 0;
            return NavResult.Moved;
        }

        public NavResult Last()
        {
            if (items.Count == 0) return NavResult.Empty;
            Index = items.Count - 1;
            return NavResult.Moved;
        }

        /// <summary>
        /// Move to the next item (after the current one, wrapping) whose label starts with the prefix.
        /// Returns false when nothing matches.
        /// </summary>
        public bool FindNext(string prefix) => FindNext(prefix, prefix != null && prefix.Length > 1);

        /// <summary>Type-ahead search; a multi-letter prefix may keep the current item if it still matches.</summary>
        public bool FindNext(string prefix, bool includeCurrent)
        {
            if (items.Count == 0 || string.IsNullOrEmpty(prefix)) return false;
            int start = Index < 0 ? 0 : (includeCurrent ? Index : Index + 1);
            for (int n = 0; n < items.Count; n++)
            {
                int i = (start + n) % items.Count;
                if (TextUtil.StartsWithIgnoreCase(label(items[i]), prefix))
                {
                    Index = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>"3 of 7"</summary>
        public string PositionText => HasCurrent ? Loc.F("{0} of {1}", Index + 1, items.Count) : string.Empty;
    }
}

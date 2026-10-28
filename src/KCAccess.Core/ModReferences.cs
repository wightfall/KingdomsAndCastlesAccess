using System;
using System.Collections.Generic;
using System.IO;

namespace KCAccess.Core
{
    /// <summary>
    /// The game compiles its mods (local ones and Steam Workshop subscriptions) with Roslyn against every DLL of its
    /// Managed folder, by path, and a few more by bare name, among them "0Harmony.dll". With BepInEx the bare name
    /// resolves to BepInEx's own Harmony: a second assembly called 0Harmony, so every mod fails to compile ("An assembly
    /// with the same simple name '0Harmony' has already been imported"). A bare name is dropped when the same file is
    /// referenced by its path.
    /// </summary>
    public static class ModReferences
    {
        /// <summary>Removes bare file names that are also referenced by a full path; returns how many were removed.</summary>
        public static int DropBareDuplicates(IList<string> references)
        {
            if (references == null || references.Count == 0) return 0;
            var byPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string r in references)
                if (IsPath(r)) byPath.Add(Path.GetFileName(r.Replace('\\', '/')));
            int removed = 0;
            for (int i = references.Count - 1; i >= 0; i--)
            {
                string r = references[i];
                if (string.IsNullOrEmpty(r) || IsPath(r) || !byPath.Contains(r)) continue;
                references.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        private static bool IsPath(string r) => !string.IsNullOrEmpty(r) && (r.IndexOf('/') >= 0 || r.IndexOf('\\') >= 0);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace KCAccess.Installer
{
    [DataContract]
    public sealed class ReleaseAsset
    {
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "browser_download_url")] public string Url;
        [DataMember(Name = "size")] public long Size;
    }

    [DataContract]
    public sealed class Release
    {
        [DataMember(Name = "tag_name")] public string Tag;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "body")] public string Body;
        [DataMember(Name = "draft")] public bool Draft;
        [DataMember(Name = "prerelease")] public bool Prerelease;
        [DataMember(Name = "assets")] public List<ReleaseAsset> Assets = new List<ReleaseAsset>();

        public Version Version => Versions.Parse(Tag);

        /// <summary>The mod-only zip, the zip bundled with BepInEx, or the setup exe.</summary>
        public ReleaseAsset FindAsset(AssetKind kind)
        {
            foreach (var a in Assets)
            {
                if (a?.Name == null) continue;
                string n = a.Name.ToLowerInvariant();
                switch (kind)
                {
                    case AssetKind.Bundle when n.EndsWith("-with-bepinex.zip"):
                    case AssetKind.ModOnly when n.EndsWith(".zip") && !n.EndsWith("-with-bepinex.zip"):
                    case AssetKind.Setup when n.EndsWith(".exe"):
                        return a;
                }
            }
            return null;
        }
    }

    public enum AssetKind
    {
        ModOnly,
        Bundle,
        Setup
    }

    public static class Versions
    {
        /// <summary>Parses "v1.2.3", "1.2", "KCAccess 1.2.0" … into a Version (null when there is none).</summary>
        public static Version Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var sb = new StringBuilder();
            bool started = false;
            foreach (char c in text)
            {
                if (char.IsDigit(c) || (started && c == '.'))
                {
                    sb.Append(c);
                    started = true;
                }
                else if (started) break;
            }
            string s = sb.ToString().Trim('.');
            if (s.Length == 0) return null;
            if (!s.Contains(".")) s += ".0";
            return Version.TryParse(s, out var v) ? Normalize(v) : null;
        }

        /// <summary>1.2 and 1.2.0.0 compare equal.</summary>
        public static Version Normalize(Version v)
        {
            if (v == null) return null;
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
        }

        public static bool IsNewer(Version candidate, Version current)
        {
            if (candidate == null) return false;
            if (current == null) return true;
            return Normalize(candidate) > Normalize(current);
        }

        public static string Show(Version v)
        {
            if (v == null) return KCAccess.Core.Loc.T("not installed");
            v = Normalize(v);
            return v.Revision > 0 ? v.ToString(4) : v.ToString(3);
        }
    }

    public static class ReleaseJson
    {
        public static Release ParseRelease(string json) => Parse<Release>(json);

        public static List<Release> ParseReleases(string json) => Parse<List<Release>>(json) ?? new List<Release>();

        /// <summary>Newest non-draft, non-prerelease release by version number.</summary>
        public static Release Newest(IEnumerable<Release> releases)
        {
            Release best = null;
            foreach (var r in releases)
            {
                if (r == null || r.Draft || r.Prerelease || r.Version == null) continue;
                if (best == null || r.Version > best.Version) best = r;
            }
            return best;
        }

        private static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            var serializer = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                try
                {
                    return serializer.ReadObject(ms) as T;
                }
                catch (SerializationException)
                {
                    return null;
                }
            }
        }
    }
}

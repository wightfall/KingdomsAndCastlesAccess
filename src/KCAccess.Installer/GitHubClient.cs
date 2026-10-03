using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace KCAccess.Installer
{
    /// <summary>Talks to the GitHub releases API of the mod repository.</summary>
    public sealed class GitHubClient
    {
        public const string Owner = "wightfall";
        public const string Repo = "KingdomsAndCastlesAccess";

        public static string ReleasesPage => "https://github.com/" + Owner + "/" + Repo + "/releases";

        static GitHubClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        private static WebClient NewClient()
        {
            var wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "KCAccessSetup/" + Assembly.GetExecutingAssembly().GetName().Version;
            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return wc;
        }

        /// <summary>Newest published release, or null when offline / nothing published.</summary>
        public async Task<Release> GetLatestAsync()
        {
            using (var wc = NewClient())
            {
                string json = await wc.DownloadStringTaskAsync("https://api.github.com/repos/" + Owner + "/" + Repo + "/releases?per_page=20").ConfigureAwait(false);
                return ReleaseJson.Newest(ReleaseJson.ParseReleases(json));
            }
        }

        /// <summary>Downloads an asset to a temporary file and returns its path.</summary>
        public async Task<string> DownloadAsync(ReleaseAsset asset, IProgress<int> progress)
        {
            string path = Path.Combine(Path.GetTempPath(), "KCAccess-" + Guid.NewGuid().ToString("N") + "-" + asset.Name);
            using (var wc = NewClient())
            {
                wc.Headers[HttpRequestHeader.Accept] = "application/octet-stream";
                if (progress != null) wc.DownloadProgressChanged += (s, e) => progress.Report(e.ProgressPercentage);
                await wc.DownloadFileTaskAsync(new Uri(asset.Url), path).ConfigureAwait(false);
            }
            return path;
        }
    }
}

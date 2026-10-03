using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using KCAccess.Installer;
using Xunit;

namespace KCAccess.Tests
{
    public class SteamLibraryTests
    {
        private const string LibraryVdf = @"""libraryfolders""
{
	""0""
	{
		""path""		""C:\\Program Files (x86)\\Steam""
		""label""		""""
		""apps"" { ""228980"" ""1"" }
	}
	""1""
	{
		""path""		""D:\\SteamLibrary""
		""apps"" { ""569480"" ""2000"" }
	}
}";

        [Fact]
        public void ParsesLibraryFolders()
        {
            var libs = SteamLibrary.ParseLibraryFolders(LibraryVdf);
            Assert.Equal(new[] { @"C:\Program Files (x86)\Steam", @"D:\SteamLibrary" }, libs.ToArray());
        }

        [Fact]
        public void ParsesInstallDir()
        {
            string acf = "\"AppState\"\n{\n\t\"appid\"\t\t\"569480\"\n\t\"installdir\"\t\t\"Kingdoms and Castles\"\n}";
            Assert.Equal("Kingdoms and Castles", SteamLibrary.ParseInstallDir(acf));
            Assert.Null(SteamLibrary.ParseInstallDir("\"AppState\" { }"));
            Assert.Null(SteamLibrary.ParseInstallDir(null));
        }

        [Fact]
        public void EmptyVdfHasNoLibraries()
        {
            Assert.Empty(SteamLibrary.ParseLibraryFolders(""));
            Assert.Empty(SteamLibrary.ParseLibraryFolders(null));
        }

        [Fact]
        public void FindsGameInSecondaryLibrary()
        {
            using (var tmp = new TempDir())
            {
                string steam = tmp.Sub("Steam");
                string lib = tmp.Sub("Lib");
                Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
                File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), "\"libraryfolders\" { \"1\" { \"path\" \"" + lib.Replace("\\", "\\\\") + "\" } }");
                string apps = Path.Combine(lib, "steamapps");
                string game = Path.Combine(apps, "common", "KC Custom");
                Directory.CreateDirectory(game);
                File.WriteAllText(Path.Combine(apps, "appmanifest_569480.acf"), "\"AppState\" { \"installdir\" \"KC Custom\" }");
                File.WriteAllText(Path.Combine(game, SteamLibrary.GameExe), "x");
                Assert.Equal(game, SteamLibrary.FindGame(new[] { steam }));
            }
        }

        [Fact]
        public void ReturnsNullWhenGameMissing()
        {
            using (var tmp = new TempDir())
            {
                Assert.Null(SteamLibrary.FindGame(new[] { tmp.Sub("nothing"), null, "" }));
            }
        }
    }

    public class VersionsTests
    {
        [Theory]
        [InlineData("v1.2.3", "1.2.3.0")]
        [InlineData("1.2", "1.2.0.0")]
        [InlineData("KCAccess 1.10.0", "1.10.0.0")]
        [InlineData("1.1.0+abcdef", "1.1.0.0")]
        [InlineData("2", "2.0.0.0")]
        public void Parses(string text, string expected)
        {
            Assert.Equal(Version.Parse(expected), Versions.Parse(text));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("latest")]
        public void NoVersionIsNull(string text)
        {
            Assert.Null(Versions.Parse(text));
        }

        [Fact]
        public void ComparesNormalized()
        {
            Assert.True(Versions.IsNewer(new Version(1, 2), new Version(1, 1, 9)));
            Assert.False(Versions.IsNewer(new Version(1, 2), new Version(1, 2, 0, 0)));
            Assert.False(Versions.IsNewer(new Version(1, 0), new Version(1, 1)));
            Assert.True(Versions.IsNewer(new Version(1, 0), null));
            Assert.False(Versions.IsNewer(null, new Version(1, 0)));
        }

        [Fact]
        public void ShowsShortVersion()
        {
            Assert.Equal("1.1.0", Versions.Show(new Version(1, 1, 0, 0)));
            Assert.Equal("1.1.0.5", Versions.Show(new Version(1, 1, 0, 5)));
            Assert.Equal("not installed", Versions.Show(null));
        }
    }

    public class ReleaseJsonTests
    {
        private const string Json = @"[
 {""tag_name"":""v1.3.0-beta"",""name"":""beta"",""draft"":false,""prerelease"":true,""assets"":[]},
 {""tag_name"":""v1.1.0"",""name"":""KCAccess 1.1.0"",""body"":""notes"",""draft"":false,""prerelease"":false,
  ""assets"":[{""name"":""KCAccess-v1.1.0.zip"",""browser_download_url"":""https://x/mod.zip"",""size"":10},
              {""name"":""KCAccess-v1.1.0-with-BepInEx.zip"",""browser_download_url"":""https://x/bundle.zip"",""size"":20},
              {""name"":""KCAccess-Setup-v1.1.0.exe"",""browser_download_url"":""https://x/setup.exe"",""size"":30}]},
 {""tag_name"":""v1.2.0"",""draft"":true,""prerelease"":false,""assets"":[]},
 {""tag_name"":""v1.0.0"",""draft"":false,""prerelease"":false,""assets"":[]}
]";

        [Fact]
        public void PicksNewestPublishedRelease()
        {
            var r = ReleaseJson.Newest(ReleaseJson.ParseReleases(Json));
            Assert.Equal("v1.1.0", r.Tag);
            Assert.Equal(new Version(1, 1, 0, 0), r.Version);
            Assert.Equal("notes", r.Body);
        }

        [Fact]
        public void FindsAssetsByKind()
        {
            var r = ReleaseJson.Newest(ReleaseJson.ParseReleases(Json));
            Assert.Equal("https://x/mod.zip", r.FindAsset(AssetKind.ModOnly).Url);
            Assert.Equal("https://x/bundle.zip", r.FindAsset(AssetKind.Bundle).Url);
            Assert.Equal("https://x/setup.exe", r.FindAsset(AssetKind.Setup).Url);
        }

        [Fact]
        public void BadJsonGivesEmptyList()
        {
            Assert.Empty(ReleaseJson.ParseReleases("not json"));
            Assert.Null(ReleaseJson.Newest(ReleaseJson.ParseReleases("[]")));
        }
    }

    public class ModInstallerTests
    {
        [Theory]
        [InlineData("BepInEx/plugins/KCAccess/KCAccess.dll", false, true)]
        [InlineData("BepInEx\\plugins\\KCAccess\\prism.dll", false, true)]
        [InlineData("winhttp.dll", false, false)]
        [InlineData("winhttp.dll", true, true)]
        [InlineData("BepInEx/core/BepInEx.dll", true, true)]
        [InlineData("BepInEx/config/BepInEx.cfg", true, false)]
        [InlineData("BepInEx/plugins/KCAccess/", true, false)]
        [InlineData("KCAccess-README.md", false, true)]
        [InlineData("", true, false)]
        public void ChoosesEntries(string entry, bool loader, bool expected)
        {
            Assert.Equal(expected, ModInstaller.ShouldExtract(entry, loader));
        }

        [Fact]
        public void RejectsPathTraversal()
        {
            using (var tmp = new TempDir())
            {
                Assert.Throws<InvalidDataException>(() => ModInstaller.SafeCombine(tmp.Path, "../evil.dll"));
                Assert.Throws<InvalidDataException>(() => ModInstaller.SafeCombine(tmp.Path, "BepInEx/../../evil.dll"));
                Assert.StartsWith(tmp.Path, ModInstaller.SafeCombine(tmp.Path, "BepInEx/plugins/a.dll"));
            }
        }

        private static MemoryStream MakeZip(bool withLoader, string version = "1.0")
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                void Add(string name, string content)
                {
                    var e = zip.CreateEntry(name);
                    using (var w = new StreamWriter(e.Open(), Encoding.UTF8)) w.Write(content);
                }
                Add("BepInEx/plugins/KCAccess/KCAccess.dll", "mod " + version);
                Add("BepInEx/plugins/KCAccess/README.md", "readme");
                Add("BepInEx/config/should-not-be-written.cfg", "x");
                if (withLoader)
                {
                    Add("winhttp.dll", "loader");
                    Add("doorstop_config.ini", "ini");
                    Add("BepInEx/core/BepInEx.dll", "core");
                }
            }
            ms.Position = 0;
            return ms;
        }

        private static string FakeGame(TempDir tmp)
        {
            string game = tmp.Sub("game");
            Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, SteamLibrary.GameExe), "x");
            return game;
        }

        [Fact]
        public void FreshInstallAddsBepInExAndMod()
        {
            using (var tmp = new TempDir())
            {
                string game = FakeGame(tmp);
                var inst = new ModInstaller(game);
                Assert.False(inst.BepInExInstalled);
                int n = inst.Install(MakeZip(withLoader: true));
                Assert.Equal(5, n);
                Assert.True(inst.BepInExInstalled);
                Assert.True(File.Exists(Path.Combine(game, @"BepInEx\plugins\KCAccess\KCAccess.dll")));
                Assert.False(File.Exists(Path.Combine(game, @"BepInEx\config\should-not-be-written.cfg")));
            }
        }

        [Fact]
        public void UpdateKeepsExistingBepInExAndSettings()
        {
            using (var tmp = new TempDir())
            {
                string game = FakeGame(tmp);
                var inst = new ModInstaller(game);
                inst.Install(MakeZip(withLoader: true));
                File.WriteAllText(Path.Combine(game, "winhttp.dll"), "user's own loader");
                Directory.CreateDirectory(Path.Combine(game, @"BepInEx\config"));
                File.WriteAllText(Path.Combine(game, @"BepInEx\config\kcaccess.screenreader.cfg"), "settings");
                File.WriteAllText(Path.Combine(game, @"BepInEx\plugins\KCAccess\stale.dll"), "old file");

                inst.Install(MakeZip(withLoader: true, version: "2.0"));

                Assert.Equal("user's own loader", File.ReadAllText(Path.Combine(game, "winhttp.dll")));
                Assert.Equal("settings", File.ReadAllText(Path.Combine(game, @"BepInEx\config\kcaccess.screenreader.cfg")));
                Assert.Equal("mod 2.0", File.ReadAllText(Path.Combine(game, @"BepInEx\plugins\KCAccess\KCAccess.dll")));
                Assert.False(File.Exists(Path.Combine(game, @"BepInEx\plugins\KCAccess\stale.dll")));
            }
        }

        [Fact]
        public void ModOnlyPackageNeedsBepInEx()
        {
            using (var tmp = new TempDir())
            {
                var inst = new ModInstaller(FakeGame(tmp));
                Assert.Throws<InvalidDataException>(() => inst.Install(MakeZip(withLoader: false)));
            }
        }

        [Fact]
        public void RejectsPackageWithoutMod()
        {
            using (var tmp = new TempDir())
            {
                var inst = new ModInstaller(FakeGame(tmp));
                var ms = new MemoryStream();
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true)) zip.CreateEntry("other.txt");
                ms.Position = 0;
                Assert.Throws<InvalidDataException>(() => inst.Install(ms));
            }
        }

        [Fact]
        public void UninstallRemovesModAndOptionallyLoader()
        {
            using (var tmp = new TempDir())
            {
                string game = FakeGame(tmp);
                var inst = new ModInstaller(game);
                inst.Install(MakeZip(withLoader: true));
                inst.Uninstall(removeLoader: false);
                Assert.False(Directory.Exists(inst.PluginDir));
                Assert.True(inst.BepInExInstalled);
                inst.Uninstall(removeLoader: true);
                Assert.False(File.Exists(Path.Combine(game, "winhttp.dll")));
                Assert.False(Directory.Exists(Path.Combine(game, "BepInEx")));
                Assert.True(File.Exists(Path.Combine(game, SteamLibrary.GameExe)));
            }
        }

        [Fact]
        public void ReplaceFileOverwritesTarget()
        {
            using (var tmp = new TempDir())
            {
                string src = tmp.Sub("new.exe");
                string dst = tmp.Sub("KCAccess-Updater.exe");
                File.WriteAllText(src, "new");
                File.WriteAllText(dst, "old");
                ModInstaller.ReplaceFile(src, dst);
                Assert.Equal("new", File.ReadAllText(dst));
            }
        }

        [Fact]
        public void InstalledVersionIsNullWithoutMod()
        {
            using (var tmp = new TempDir())
            {
                Assert.Null(new ModInstaller(FakeGame(tmp)).InstalledVersion);
            }
        }

        [Fact]
        public void OptionsParseCommandLine()
        {
            var o = Options.Parse(new[] { "--install", "--game", "\"C:\\Games\\KC\"" });
            Assert.True(o.InstallNow);
            Assert.Equal(@"C:\Games\KC", o.GameDir);
            Assert.False(Options.Parse(new string[0]).InstallNow);
        }
    }

    internal sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kcaccess-test-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public string Sub(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch
            {
                // Best effort.
            }
        }
    }
}

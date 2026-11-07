using System.Collections.Generic;
using System.IO;
using KCAccess.Core;
using KCAccess.Installer;
using Xunit;

namespace KCAccess.Tests
{
    public class GameTextFilesTests
    {
        private static readonly List<GameTerm> Terms = new List<GameTerm>
        {
            new GameTerm { Term = "Settle", English = "Settle" },
            new GameTerm { Term = "GiftReady", English = "A gift is ready.\nPick it up." },
            new GameTerm { Term = "Cost", English = "Costs {0} gold <sprite name=icon_gold>" },
        };

        /// <summary>
        /// Fails when a game update brought a text Thai does not have. The template comes from the installed game, written
        /// by the mod at its last start. Names, fonts, thanks and bare numbers stay as they are on purpose.
        /// </summary>
        [Fact]
        public void ThaiHasEveryTextOfTheGame()
        {
            string game = Path.Combine(System.Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? "", "Steam", "steamapps", "common", "Kingdoms and Castles",
                "BepInEx", "plugins", "KCAccess", "Languages", "game", "template.txt");
            if (!File.Exists(game)) return;
            string thai = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile()), "..", "..", "src", "KCAccess", "Languages", "game", "th.txt"));
            var template = LocTable.Parse(File.ReadAllText(game));
            var shipped = LocTable.Parse(File.ReadAllText(thai));
            var missing = new List<string>();
            foreach (string term in template.Keys)
            {
                if (term.StartsWith("AVoteOption") || term.StartsWith("CityNamePreset") || term.Contains(" SDF") || term.StartsWith("Fundamental Brigade")
                    || term.StartsWith("Dialogue System/") || term == "SSAO" || term == "kclogo" || term == "GameTitle" || term == "FigThanks" || term.Contains(" and ")) continue;
                if (string.IsNullOrEmpty(shipped.Raw(term))) missing.Add(term);
            }
            Assert.True(missing.Count == 0, string.Join(", ", missing));
        }

        /// <summary>
        /// The game's update of November 2026 added these texts in English only. Every language the mod speaks must have them:
        /// the game's own 17 languages through their small game text files, Thai through its full one.
        /// </summary>
        [Fact]
        public void EveryLanguageHasTheTextsTheGameLeftInEnglish()
        {
            string languages = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile()), "..", "..", "src", "KCAccess", "Languages"));
            var missing = new List<string>();
            foreach (string path in Directory.GetFiles(languages, "*.txt"))
            {
                string code = Path.GetFileNameWithoutExtension(path);
                if (code == "template") continue;
                string file = Path.Combine(languages, "game", code + ".txt");
                if (!File.Exists(file)) { missing.Add(code + ": no game text file"); continue; }
                var table = LocTable.Parse(File.ReadAllText(file));
                if (table.LanguageName.Length == 0) missing.Add(code + ": no language name");
                foreach (string term in new[] { "envoys", "catapults", "AMapSelectExtraLarge" })
                    if (string.IsNullOrEmpty(table.Raw(term))) missing.Add(code + ": " + term);
            }
            Assert.True(missing.Count == 0, string.Join(", ", missing));
        }

        /// <summary>The game's Cargo Ship and Transport Ship are different ships; the mod once called the first by the second's name.</summary>
        [Fact]
        public void TheCargoShipKeepsTheGamesName()
        {
            string languages = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile()), "..", "..", "src", "KCAccess", "Languages"));
            string template = File.ReadAllText(Path.Combine(languages, "template.txt"));
            Assert.Contains("cargo ship =", template);
            Assert.DoesNotContain("transport ship =", template);
        }

        private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string here = "") => here;

        [Fact]
        public void TemplateListsEveryTermWithItsEnglish()
        {
            string t = GameTexts.Build(Terms, GameTexts.Header(null), null, null, null);
            var table = LocTable.Parse(t);
            Assert.Equal(new[] { "Settle", "GiftReady", "Cost" }, table.Keys);
            Assert.All(table.Keys, k => Assert.Equal(string.Empty, table.Raw(k)));
            Assert.Contains(GameTexts.EnglishMark + "A gift is ready.\\nPick it up.", t);
        }

        [Fact]
        public void TokensMustSurviveTranslation()
        {
            Assert.True(GameTexts.Matches("Costs {0} gold <sprite name=icon_gold>", "ราคา <sprite name=icon_gold> {0} ทอง"));
            Assert.False(GameTexts.Matches("Costs {0} gold", "ราคา ทอง"));
            Assert.False(GameTexts.Matches("Hello {[NAME]}", "สวัสดี {[NAME]} {0}"));
            Assert.True(GameTexts.Matches("Hello {[NAME]}", "{[NAME]} สวัสดี"));
        }

        [Fact]
        public void UsableSkipsBlankBrokenAndUnknownTerms()
        {
            var table = LocTable.Parse("Settle = ตั้งถิ่นฐาน\nCost = ราคาแพง\nGiftReady =\nGone = หาย");
            var english = new Dictionary<string, string>();
            foreach (var t in Terms) english[t.Term] = t.English;
            var warnings = new List<string>();
            var usable = GameTexts.Usable(table, english, warnings.Add);
            Assert.Single(usable);
            Assert.Equal("ตั้งถิ่นฐาน", usable["Settle"]);
            Assert.Equal("ตั้งถิ่นฐาน", usable["settle"]); // a window may ask for the term in another case
            Assert.Single(warnings); // Cost lost its {0} and sprite
        }

        [Fact]
        public void DialogueLinesAreNamedAsTheGameNamesThem()
        {
            Assert.Equal("Dialogue System/Conversation/D1010/Entry/D1001/Dialogue Text", GameTexts.DialogueTerm(10, 1, "Dialogue Text"));
            Assert.Equal("Dialogue System/Conversation/D1040/Entry/D1012/Menu Text", GameTexts.DialogueTerm(40, 12, "Menu Text"));
        }

        [Fact]
        public void UpdateKeepsThePlayersChangesAndTakesNewShippedTexts()
        {
            var previous = LocTable.Parse("Settle = old\nGiftReady = gift old");
            var shipped = LocTable.Parse("# Language: ไทย (Thai)\nSettle = new\nGiftReady = gift new");
            var user = LocTable.Parse("Settle = old\nGiftReady = my gift\nRemovedTerm = kept");
            var built = LocTable.Parse(GameTexts.Build(Terms, GameTexts.Header("ไทย (Thai)"), user, shipped, previous));
            Assert.Equal("new", built.Raw("Settle"));          // not changed by the player: the update wins
            Assert.Equal("my gift", built.Raw("GiftReady"));   // changed by the player: kept
            Assert.Equal(string.Empty, built.Raw("Cost"));     // new game text, untranslated
            Assert.Equal("kept", built.Raw("RemovedTerm"));    // not in this game version: kept at the end
            Assert.Equal("ไทย (Thai)", built.LanguageName);
        }

        [Fact]
        public void InstallerKeepsThePlayersGameTextFiles()
        {
            var inst = new ModInstaller(Path.Combine(Path.GetTempPath(), "kcgame"));
            string lang = Path.Combine(inst.PluginDir, "Languages");
            Assert.True(inst.IsPlayerLanguageFile(Path.Combine(lang, "game", "th.txt")));
            Assert.True(inst.IsPlayerLanguageFile(Path.Combine(lang, "game", "default", ".base", "th.txt")));
            Assert.False(inst.IsPlayerLanguageFile(Path.Combine(lang, "game", "template.txt")));
            Assert.False(inst.IsPlayerLanguageFile(Path.Combine(lang, "game", "default", "th.txt")));
            Assert.True(inst.IsPlayerLanguageFile(Path.Combine(lang, "th.txt")));
        }
    }
}

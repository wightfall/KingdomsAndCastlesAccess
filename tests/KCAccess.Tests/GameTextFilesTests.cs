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

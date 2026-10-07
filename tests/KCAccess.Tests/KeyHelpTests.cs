using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class KeyHelpTests
    {
        [Fact]
        public void MenuWithButtonsOnlyDoesNotMentionSlidersOrTyping()
        {
            // Like the main menu: buttons and a few texts, letters jump.
            var f = new ScreenFeatures { Items = 19, Controls = true, Texts = true, TypeAhead = true };
            string t = KeyHelp.NavKeys(f);
            Assert.Contains("Enter or Space activates", t);
            Assert.Contains("Letters jump", t);
            Assert.DoesNotContain("Left and Right", t);
            Assert.DoesNotContain("Page Up", t);
            Assert.DoesNotContain("text field", t);
            Assert.DoesNotContain("Delete", t);
            Assert.DoesNotContain("F6", t);
            Assert.Contains("Escape goes back", t);
        }

        [Fact]
        public void SettingsWithSlidersMentionsAdjustKeys()
        {
            var f = new ScreenFeatures { Items = 19, Controls = true, Adjustables = true, Sliders = true, TextFields = true };
            string t = KeyHelp.NavKeys(f);
            Assert.Contains("Left and Right arrows change", t);
            Assert.Contains("Page Up and Page Down", t);
            Assert.Contains("Enter starts typing", t);
        }

        [Fact]
        public void PanelMentionsF6AndSaveListMentionsDelete()
        {
            Assert.Contains("F6", KeyHelp.NavKeys(new ScreenFeatures { Items = 5, Controls = true, InPanel = true }));
            Assert.Contains("Delete deletes", KeyHelp.NavKeys(new ScreenFeatures { Items = 5, Controls = true, SaveSlots = true }));
        }

        [Fact]
        public void SingleItemDialogStaysShort()
        {
            string t = KeyHelp.NavKeys(new ScreenFeatures { Items = 1, Controls = true });
            Assert.DoesNotContain("Up and Down", t);
            Assert.DoesNotContain("Home and End", t);
            Assert.DoesNotContain("Control R", t);
        }

        [Fact]
        public void MapHelpFollowsState()
        {
            string placing = KeyHelp.MapHelp(new MapState { Placing = true, LinePlacement = true });
            Assert.Contains("R rotates", placing);
            Assert.Contains("Shift Enter marks the start", placing);
            Assert.DoesNotContain("build menu", placing);
            Assert.DoesNotContain("stack", placing);
            Assert.Contains("Shift Page Up and Shift Page Down set how many levels", KeyHelp.MapHelp(new MapState { Placing = true, CastleBlock = true }));

            string plain = KeyHelp.MapHelp(new MapState());
            Assert.DoesNotContain("Delete", plain);   // nothing selected
            Assert.DoesNotContain("M sends", plain);  // no soldiers
            Assert.DoesNotContain("Backslash", plain); // no target

            string selected = KeyHelp.MapHelp(new MapState { HasSelection = true, BuildingSelected = true, SoldiersSelected = true, HasTarget = true });
            Assert.Contains("Delete demolishes", selected);
            Assert.Contains("M sends", selected);
            Assert.Contains("Backslash jumps", selected);

            Assert.StartsWith("Walking", KeyHelp.MapHelp(new MapState { Walking = true }));
            string route = KeyHelp.MapHelp(new MapState { RouteMode = true });
            Assert.StartsWith("Editing a ship or cart route", route);
            Assert.DoesNotContain("Shift Enter marks", route);
            Assert.Contains("Control Shift Enter adds", KeyHelp.MapHelp(new MapState { SoldiersSelected = true }));
            Assert.Contains("no keep yet", KeyHelp.MapHelp(new MapState { HasKeep = false }));
            Assert.DoesNotContain("Space pauses", KeyHelp.MapHelp(new MapState { MenuMap = true }));
        }

        [Fact]
        public void KeyListHasEverySectionAndCueMeanings()
        {
            string all = KeyHelp.AllText();
            foreach (var s in KeyHelp.Sections)
            {
                Assert.Contains(s.Title, all);
                Assert.NotEmpty(s.Lines);
            }
            foreach (Cue c in CueLibrary.All) Assert.False(string.IsNullOrEmpty(CueLibrary.Meaning(c)));
        }
    }
}

namespace KCAccess.Tests
{
    public class KeyHelpBackTests
    {
        [Fact]
        public void MainMenuHasNoEscape() => Xunit.Assert.DoesNotContain("Escape", KeyHelp.NavKeys(new ScreenFeatures { Items = 5, Controls = true, CanGoBack = false }));
    }
}

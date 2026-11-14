using System;
using System.Collections.Generic;
using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class GuidedGameTests
    {
        private static string[] Join(string[] a, params string[] b)
        {
            var l = new List<string>(a);
            l.AddRange(b);
            return l.ToArray();
        }

        private static Func<string, int> Have(params string[] built)
        {
            var counts = new Dictionary<string, int>();
            foreach (var b in built) counts[b] = counts.TryGetValue(b, out var n) ? n + 1 : 1;
            return name => counts.TryGetValue(name, out var c) ? c : 0;
        }

        [Fact]
        public void StartsAtTheKeepAndFollowsWhatIsBuilt()
        {
            Assert.Equal(0, GuidedGame.Current(Have()));
            Assert.Equal(1, GuidedGame.Current(Have("keep")));
            // A new kingdom has no road coverage yet: roads come before houses.
            Assert.Equal(1, GuidedGame.Current(Have("keep", "smallhouse", "smallhouse", "road", "road")));
            string[] roads = { "road", "road", "road", "stoneroad", "road" };
            Assert.Equal(2, GuidedGame.Current(Have(Join(roads, "keep", "smallhouse"))));
            Assert.Equal(3, GuidedGame.Current(Have(Join(roads, "keep", "smallhouse", "largehouse"))));
            // Four farm tiles, or four orchard tiles, make the food step.
            Assert.Equal(3, GuidedGame.Current(Have(Join(roads, "keep", "smallhouse", "smallhouse", "farm", "farm", "farm"))));
            Assert.Equal(4, GuidedGame.Current(Have(Join(roads, "keep", "smallhouse", "smallhouse", "farm", "farm", "farm", "farm"))));
            Assert.Equal(4, GuidedGame.Current(Have(Join(roads, "keep", "smallhouse", "smallhouse", "orchard", "orchard", "orchard", "orchard"))));
        }

        [Fact]
        public void AKingdomWithEverythingIsComplete()
        {
            var all = new List<string>();
            foreach (var s in GuidedGame.Steps)
                for (int i = 0; i < s.Needed; i++) all.Add(s.Buildings[0]);
            Assert.Equal(GuidedGame.Count, GuidedGame.Current(Have(all.ToArray())));
            Assert.Contains("complete", GuidedGame.Describe(GuidedGame.Count));
        }

        [Fact]
        public void ProgressNamesWhatWasDoneAndTheNextStep()
        {
            string s = GuidedGame.Progress(2, 4);
            Assert.Contains("Houses, Farms done", s);
            Assert.Contains("Step 5 of " + GuidedGame.Count, s);
            Assert.Null(GuidedGame.Progress(3, 3));
            Assert.Null(GuidedGame.Progress(3, 2)); // a building demolished: no announcement
        }

        [Fact]
        public void EveryStepNamesAKeyOrACategoryAndUsesKnownBindings()
        {
            foreach (var s in GuidedGame.Steps)
            {
                string text = KeyHelp.Resolve(Loc.T(s.Text));
                Assert.DoesNotContain("{", text);
                Assert.True(s.Needed >= 1 && s.Buildings.Length >= 1);
            }
        }

        [Fact]
        public void WhatsNewIsAnnouncedOnlyAfterAnUpdate()
        {
            Assert.False(WhatsNew.ShouldAnnounce(null, "1.29.1"));      // first install
            Assert.False(WhatsNew.ShouldAnnounce("1.29.1\r\n", "1.29.1")); // same version again
            Assert.True(WhatsNew.ShouldAnnounce("1.29.0", "1.29.1"));
            string text = KeyHelp.Resolve(WhatsNew.Text("1.29.1"));
            Assert.StartsWith("What's new in KCAccess 1.29.1", text);
            Assert.DoesNotContain("{", text);
        }

        [Fact]
        public void ControllerNamesForBindings()
        {
            Assert.Equal("Y", ControllerMap.NameOf("TileInfo", onMap: true));
            Assert.Equal("hold LT and press A", ControllerMap.NameOf("Walk"));
            Assert.Equal("hold LT and press B", ControllerMap.NameOf("JumpTarget"));
            Assert.Equal("hold RT and press A", ControllerMap.NameOf("BuildMenu"));
            Assert.Equal("hold both triggers and press Start button", ControllerMap.NameOf("Settings"));
            Assert.Null(ControllerMap.NameOf("Report")); // no button: the keyboard key is named
            Assert.Null(ControllerMap.NameOf(null));
        }

        [Fact]
        public void ClipboardKeysCannotBeGivenToAnAction()
        {
            var b = new Bindings();
            foreach (var key in new[] { "Ctrl+C", "Ctrl+Shift+C", "Ctrl+V" })
            {
                Assert.True(Chord.TryParse(key, out var c));
                Assert.NotNull(b.Conflict("TileInfo", c));
            }
        }

        [Fact]
        public void SelfRepairNoticeSaysOneThing()
        {
            Assert.Contains("1 thing it reads from the game is not available",
                Loc.T("Kingdoms and Castles was updated after this version of KCAccess came out. KCAccess adapted itself so you can keep playing, but 1 thing it reads from the game is not available until KCAccess is updated. To look for a new version, run KCAccess-Updater.exe in the game folder."));
        }
    }
}

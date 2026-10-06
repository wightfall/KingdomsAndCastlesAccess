using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class BindingsTests
    {
        [Theory]
        [InlineData("Ctrl+Shift+O", "O", true, true, false)]
        [InlineData("shift+F1", "F1", false, true, false)]
        [InlineData("Backslash", "Backslash", false, false, false)]
        [InlineData("Alt+Alpha3", "Alpha3", false, false, true)]
        public void ParsesChords(string text, string key, bool ctrl, bool shift, bool alt)
        {
            Assert.True(Chord.TryParse(text, out var c));
            Assert.Equal(key, c.Key);
            Assert.Equal(ctrl, c.Ctrl);
            Assert.Equal(shift, c.Shift);
            Assert.Equal(alt, c.Alt);
        }

        [Theory]
        [InlineData("")]
        [InlineData("Ctrl+Shift")]
        [InlineData("A+B")]
        [InlineData("Ctrl++A")]
        public void RejectsBrokenChords(string text) => Assert.False(Chord.TryParse(text, out _));

        [Fact]
        public void SpeaksChordsNaturally()
        {
            Chord.TryParse("Ctrl+Shift+RightBracket", out var c);
            Assert.Equal("Control Shift close bracket", c.Spoken());
            Chord.TryParse("Alt+Alpha2", out c);
            Assert.Equal("Alt 2", c.Spoken());
            Chord.TryParse("PageDown", out c);
            Assert.Equal("Page Down", c.Spoken());
        }

        [Fact]
        public void DefaultsHaveNoConflictsAmongThemselves()
        {
            var b = new Bindings();
            foreach (var d in Bindings.Defs) Assert.Null(b.Conflict(d.Id, b.Get(d.Id)));
        }

        [Fact]
        public void DetectsModFixedAndGameConflicts()
        {
            var b = new Bindings();
            Chord.TryParse("O", out var o);
            Assert.Contains("Survey the area", b.Conflict("TileInfo", o));
            Chord.TryParse("Ctrl+Alpha1", out var bm);
            Assert.Contains("bookmark 1", b.Conflict("Survey", bm));
            Chord.TryParse("Escape", out var esc);
            Assert.Contains("going back", b.Conflict("Survey", esc));
            Chord.TryParse("J", out var j);
            Assert.Contains("job priority", b.Conflict("Survey", j, c => c.Key == "J" ? "job priority" : null));
            Chord.TryParse("Y", out var y);
            Assert.Null(b.Conflict("Survey", y, c => null));
        }

        [Fact]
        public void GlobalKeysClashWithMapKeys()
        {
            var b = new Bindings();
            Chord.TryParse("O", out var o);
            Assert.NotNull(b.Conflict("Settings", o)); // O is the map's survey key, a global key would steal it
        }

        [Fact]
        public void SerializesOnlyChangesAndLoadsThem()
        {
            var b = new Bindings();
            Assert.Equal("", b.Serialize());
            Chord.TryParse("Y", out var y);
            b.Set("Survey", y);
            string saved = b.Serialize();
            Assert.Equal("Survey=Y", saved);
            var c = new Bindings();
            c.Load(saved + ";Bogus=X;Walk=;Coordinates");
            Assert.Equal(y, c.Get("Survey"));
            Assert.Equal(Bindings.Def("Walk").Default, c.Get("Walk"));
            c.Reset();
            Assert.Equal(Bindings.Def("Survey").Default, c.Get("Survey"));
        }
    }
}

namespace KCAccess.Tests
{
    public class ReboundHelpTests
    {
        [Fact]
        public void HelpNamesTheCurrentKeys()
        {
            var b = new Bindings();
            Chord.TryParse("Y", out var y);
            b.Set("Survey", y);
            var saved = KeyHelp.KeyNameOf;
            try
            {
                KeyHelp.KeyNameOf = b.Spoken;
                Xunit.Assert.Contains("Y surveys the area", KeyHelp.MapHelp(new MapState()));
                Xunit.Assert.DoesNotContain("O surveys", KeyHelp.MapHelp(new MapState()));
                Xunit.Assert.Contains("Y: survey the area", KeyHelp.AllText());
            }
            finally
            {
                KeyHelp.KeyNameOf = saved;
            }
        }
    }
}

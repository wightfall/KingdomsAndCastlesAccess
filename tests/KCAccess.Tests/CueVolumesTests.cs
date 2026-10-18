using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class CueVolumesTests
    {
        [Fact]
        public void EveryCueStartsAtFullOverallVolume()
        {
            var v = new CueVolumes();
            foreach (Cue cue in CueLibrary.All)
            {
                Assert.Equal(100, v.Get(cue));
                Assert.Equal(1f, v.Scale(cue));
                Assert.True(v.IsDefault(cue));
            }
            Assert.True(v.AllDefault);
            Assert.Equal(string.Empty, v.Serialize());
        }

        [Fact]
        public void LoadsAndSavesChangedCuesOnly()
        {
            var v = new CueVolumes();
            v.Load("alert=150; Navigate = 50;Edge=100");
            Assert.Equal(150, v.Get(Cue.Alert));
            Assert.Equal(50, v.Get(Cue.Navigate));
            Assert.True(v.IsDefault(Cue.Edge));
            Assert.Equal("Navigate=50;Alert=150", v.Serialize());
            var again = new CueVolumes();
            again.Load(v.Serialize());
            Assert.Equal(v.Serialize(), again.Serialize());
        }

        [Theory]
        [InlineData("Unknown=50")]
        [InlineData("3=50")]
        [InlineData("Navigate=loud")]
        [InlineData("Navigate")]
        [InlineData("=50")]
        [InlineData(";;")]
        public void IgnoresBrokenParts(string text)
        {
            var v = new CueVolumes();
            v.Load(text + ";Error=30");
            Assert.Equal(30, v.Get(Cue.Error));
            Assert.Equal("Error=30", v.Serialize());
        }

        [Fact]
        public void KeepsVolumesBetweenSilentAndDouble()
        {
            var v = new CueVolumes();
            v.Load("Step=-20;Beacon=900");
            Assert.Equal(0, v.Get(Cue.Step));
            Assert.Equal(CueVolumes.Max, v.Get(Cue.Beacon));
            Assert.Equal(0, v.Change(Cue.Step, -1));
            Assert.Equal(CueVolumes.Max, v.Change(Cue.Beacon, 1));
            Assert.Equal(110, v.Change(Cue.Wrap, 1));
            Assert.Equal(100, v.Change(Cue.Wrap, -1));
            Assert.True(v.IsDefault(Cue.Wrap));
        }

        [Fact]
        public void ResetPutsEveryCueBack()
        {
            var v = new CueVolumes();
            v.Load("Navigate=0;Alert=200");
            Assert.False(v.AllDefault);
            v.Reset();
            Assert.True(v.AllDefault);
            Assert.Equal(100, v.Get(Cue.Navigate));
        }
    }
}

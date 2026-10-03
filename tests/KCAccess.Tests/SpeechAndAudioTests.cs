using System;
using System.Collections.Generic;
using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    internal sealed class FakeBackend : ISpeechBackend
    {
        public readonly List<(string Text, bool Interrupt)> Spoken = new List<(string, bool)>();
        public int Stops;
        public string Name => "fake";

        public bool Speak(string text, bool interrupt)
        {
            Spoken.Add((text, interrupt));
            return true;
        }

        public void Stop() => Stops++;
    }

    public class AnnouncerTests
    {
        [Fact]
        public void CleansAndSpeaks()
        {
            var b = new FakeBackend();
            var a = new Announcer(b, () => 0);
            Assert.True(a.Say("<b>Hello</b>"));
            Assert.Equal("Hello", b.Spoken[0].Text);
            Assert.True(b.Spoken[0].Interrupt);
        }

        [Fact]
        public void LowPriorityDoesNotInterrupt()
        {
            var b = new FakeBackend();
            var a = new Announcer(b, () => 0);
            a.Say("queued", Priority.Low);
            Assert.False(b.Spoken[0].Interrupt);
        }

        [Fact]
        public void DropsRapidDuplicates()
        {
            double t = 0;
            var b = new FakeBackend();
            var a = new Announcer(b, () => t);
            a.Say("grass");
            t = 0.1;
            Assert.False(a.Say("grass"));
            t = 1.0;
            Assert.True(a.Say("grass"));
            Assert.Equal(2, b.Spoken.Count);
        }

        [Fact]
        public void ForceBypassesDuplicateFilter()
        {
            var b = new FakeBackend();
            var a = new Announcer(b, () => 0);
            a.Say("grass");
            Assert.True(a.Say("grass", force: true));
        }

        [Fact]
        public void IgnoresEmptyText()
        {
            var b = new FakeBackend();
            var a = new Announcer(b, () => 0);
            Assert.False(a.Say("   "));
            Assert.False(a.Say("<color=red></color>"));
            Assert.Empty(b.Spoken);
        }

        [Fact]
        public void DisabledSpeaksNothing()
        {
            var b = new FakeBackend();
            var a = new Announcer(b, () => 0) { Enabled = false };
            Assert.False(a.Say("hi"));
            Assert.Empty(b.Spoken);
        }

        [Fact]
        public void KeepsLimitedHistory()
        {
            double t = 0;
            var a = new Announcer(new FakeBackend(), () => t += 1) { HistoryLimit = 3 };
            for (int i = 0; i < 5; i++) a.Say("m" + i);
            Assert.Equal(new[] { "m2", "m3", "m4" }, new List<string>(a.History).ToArray());
            Assert.Equal("m4", a.LastSpoken);
        }

        [Fact]
        public void RepeatSpeaksLastAgain()
        {
            var b = new FakeBackend();
            var a = new Announcer(b, () => 0);
            a.Say("year 5");
            a.Repeat();
            Assert.Equal(2, b.Spoken.Count);
        }

        [Fact]
        public void RaisesSpokenEvent()
        {
            var a = new Announcer(new FakeBackend(), () => 0);
            string seen = null;
            a.Spoken += s => seen = s;
            a.Say("<i>Raid</i>");
            Assert.Equal("Raid", seen);
        }

        [Fact]
        public void RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new Announcer(null, () => 0));
            Assert.Throws<ArgumentNullException>(() => new Announcer(new FakeBackend(), null));
        }
    }

    public class ToneSynthTests
    {
        [Fact]
        public void RendersExpectedLength()
        {
            var data = ToneSynth.Render(1000, new ToneSegment(440, 440, 0.5f), new ToneSegment(880, 880, 0.25f));
            Assert.Equal(750, data.Length);
        }

        [Fact]
        public void SamplesStayInRange()
        {
            foreach (Waveform w in Enum.GetValues(typeof(Waveform)))
            {
                var data = ToneSynth.Render(8000, new ToneSegment(300, 900, 0.2f, 1f, w));
                foreach (var s in data) Assert.InRange(s, -1f, 1f);
            }
        }

        [Fact]
        public void EnvelopeStartsAndEndsSilent()
        {
            var data = ToneSynth.Render(44100, new ToneSegment(440, 440, 0.1f, 1f));
            Assert.Equal(0f, data[0], 3);
            Assert.True(Math.Abs(data[data.Length - 1]) < 0.05f);
        }

        [Fact]
        public void ZeroVolumeIsSilent()
        {
            var data = ToneSynth.Render(8000, new ToneSegment(440, 440, 0.1f, 0f));
            foreach (var s in data) Assert.Equal(0f, s);
        }

        [Fact]
        public void InvalidSampleRateThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ToneSynth.Render(0, new ToneSegment(1, 1, 1)));
        }

        [Fact]
        public void PitchForIsMonotonicAndClamped()
        {
            Assert.Equal(300f, ToneSynth.PitchFor(-1f), 1);
            Assert.Equal(1200f, ToneSynth.PitchFor(2f), 1);
            Assert.True(ToneSynth.PitchFor(0.25f) < ToneSynth.PitchFor(0.75f));
            Assert.Equal(300f, ToneSynth.PitchFor(float.NaN), 1);
        }

        [Fact]
        public void EveryCueHasARecipeThatRenders()
        {
            foreach (var cue in CueLibrary.All)
            {
                Assert.True(CueLibrary.HasRecipe(cue), cue + " has no recipe");
                var data = ToneSynth.Render(ToneSynth.DefaultSampleRate, CueLibrary.Get(cue));
                Assert.True(data.Length > 100, cue + " is too short");
                Assert.True(data.Length < ToneSynth.DefaultSampleRate, cue + " is longer than a second");
            }
        }
    }
}

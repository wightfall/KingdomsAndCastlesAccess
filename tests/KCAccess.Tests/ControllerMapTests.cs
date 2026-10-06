using System;
using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class ControllerMapTests
    {
        [Fact]
        public void BaseLayerIsNavigation()
        {
            Assert.Equal("Return", ControllerMap.Map(PadButton.A, false, false, true).Value.Key);
            Assert.Equal("Escape", ControllerMap.Map(PadButton.B, false, false, false).Value.Key);
            Assert.Equal("UpArrow", ControllerMap.Map(PadButton.Up, false, false, true).Value.Key);
            Assert.Equal("F1", ControllerMap.Map(PadButton.Start, false, false, true).Value.Key);
        }

        [Fact]
        public void MapAndMenusDifferWhereNeeded()
        {
            Assert.Equal("TileInfo", ControllerMap.Map(PadButton.Y, false, false, true).Value.Binding);
            Assert.Equal("F5", ControllerMap.Map(PadButton.Y, false, false, false).Value.Key);
            Assert.Equal("NextCategory", ControllerMap.Map(PadButton.RB, false, false, true).Value.Binding);
            Assert.Equal("PageDown", ControllerMap.Map(PadButton.RB, false, false, false).Value.Key);
        }

        [Fact]
        public void TriggerLayers()
        {
            Assert.Equal("Walk", ControllerMap.Map(PadButton.A, true, false, true).Value.Binding);
            Assert.Equal("BuildMenu", ControllerMap.Map(PadButton.A, false, true, true).Value.Binding);
            Assert.Equal("Shift+UpArrow", ControllerMap.Map(PadButton.Up, false, true, true).Value.Key);
            Assert.Equal("Alpha3", ControllerMap.Map(PadButton.Right, true, true, true).Value.Key);
            Assert.Equal("Settings", ControllerMap.Map(PadButton.Start, true, true, false).Value.Binding);
        }

        [Fact]
        public void EveryBindingUsedExistsAndEveryChordParses()
        {
            foreach (PadButton b in Enum.GetValues(typeof(PadButton)))
                foreach (bool lt in new[] { false, true })
                    foreach (bool rt in new[] { false, true })
                        foreach (bool map in new[] { false, true })
                        {
                            var a = ControllerMap.Map(b, lt, rt, map);
                            if (!a.HasValue) continue;
                            if (a.Value.Binding != null) Assert.NotNull(Bindings.Def(a.Value.Binding));
                            else Assert.True(Chord.TryParse(a.Value.Key, out _), a.Value.Key);
                        }
        }

        [Fact]
        public void BaseLayerCoversEveryButton()
        {
            foreach (PadButton b in Enum.GetValues(typeof(PadButton)))
                Assert.True(ControllerMap.Map(b, false, false, true).HasValue, b + " does nothing");
        }
    }
}

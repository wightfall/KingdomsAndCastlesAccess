using System;
using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>All sound cues the mod can play.</summary>
    public enum Cue
    {
        /// <summary>Focus moved to another menu item.</summary>
        Navigate,
        /// <summary>Navigation wrapped from last to first item (or the other way).</summary>
        Wrap,
        /// <summary>Reached the edge of a list or the map.</summary>
        Edge,
        /// <summary>Activated a control.</summary>
        Activate,
        /// <summary>Toggle switched on.</summary>
        ToggleOn,
        /// <summary>Toggle switched off.</summary>
        ToggleOff,
        /// <summary>A menu / panel opened.</summary>
        Open,
        /// <summary>A menu / panel closed.</summary>
        Close,
        /// <summary>Map cursor stepped onto land.</summary>
        StepLand,
        /// <summary>Map cursor stepped onto water.</summary>
        StepWater,
        /// <summary>Map cursor stepped onto a cell with a building or blocking resource.</summary>
        StepOccupied,
        /// <summary>Map cursor stepped onto unexplored (fogged) land.</summary>
        StepFog,
        /// <summary>Current placement position is valid.</summary>
        PlaceValid,
        /// <summary>Current placement position is invalid.</summary>
        PlaceInvalid,
        /// <summary>Building placed.</summary>
        Placed,
        /// <summary>Something failed / not allowed.</summary>
        Error,
        /// <summary>Generic notification.</summary>
        Notify,
        /// <summary>Dangerous event: raid, dragon, fire.</summary>
        Alert,
        /// <summary>Slider / value changed (pitch follows value).</summary>
        Value,
        /// <summary>Navigation beacon ping (panned and pitched towards the target).</summary>
        Beacon,
        /// <summary>One step of auto-walk.</summary>
        Step
    }

    /// <summary>Recipes for every cue, rendered by <see cref="ToneSynth"/>.</summary>
    public static class CueLibrary
    {
        private static readonly Dictionary<Cue, ToneSegment[]> Recipes = new Dictionary<Cue, ToneSegment[]>
        {
            { Cue.Navigate, new[] { new ToneSegment(880, 880, 0.03f, 0.25f) } },
            { Cue.Wrap, new[] { new ToneSegment(660, 990, 0.06f, 0.3f) } },
            { Cue.Edge, new[] { new ToneSegment(180, 140, 0.07f, 0.45f, Waveform.Triangle) } },
            { Cue.Activate, new[] { new ToneSegment(700, 700, 0.03f, 0.3f), new ToneSegment(1050, 1050, 0.04f, 0.3f) } },
            { Cue.ToggleOn, new[] { new ToneSegment(600, 600, 0.04f, 0.3f), new ToneSegment(900, 900, 0.05f, 0.3f) } },
            { Cue.ToggleOff, new[] { new ToneSegment(900, 900, 0.04f, 0.3f), new ToneSegment(600, 600, 0.05f, 0.3f) } },
            { Cue.Open, new[] { new ToneSegment(400, 800, 0.09f, 0.3f) } },
            { Cue.Close, new[] { new ToneSegment(800, 400, 0.09f, 0.3f) } },
            { Cue.StepLand, new[] { new ToneSegment(520, 480, 0.025f, 0.18f, Waveform.Triangle) } },
            { Cue.StepWater, new[] { new ToneSegment(300, 420, 0.06f, 0.22f) } },
            { Cue.StepOccupied, new[] { new ToneSegment(260, 260, 0.035f, 0.25f, Waveform.Square) } },
            { Cue.StepFog, new[] { new ToneSegment(200, 200, 0.05f, 0.08f, Waveform.Noise) } },
            { Cue.PlaceValid, new[] { new ToneSegment(1000, 1000, 0.04f, 0.25f) } },
            { Cue.PlaceInvalid, new[] { new ToneSegment(220, 200, 0.07f, 0.3f, Waveform.Square) } },
            { Cue.Placed, new[] { new ToneSegment(523, 523, 0.06f, 0.3f), new ToneSegment(659, 659, 0.06f, 0.3f), new ToneSegment(784, 784, 0.09f, 0.3f) } },
            { Cue.Error, new[] { new ToneSegment(200, 200, 0.08f, 0.35f, Waveform.Square), new ToneSegment(150, 150, 0.1f, 0.35f, Waveform.Square) } },
            { Cue.Notify, new[] { new ToneSegment(784, 784, 0.07f, 0.3f), new ToneSegment(1047, 1047, 0.1f, 0.3f) } },
            { Cue.Alert, new[] { new ToneSegment(880, 660, 0.15f, 0.45f, Waveform.Square), new ToneSegment(880, 660, 0.15f, 0.45f, Waveform.Square) } },
            { Cue.Value, new[] { new ToneSegment(600, 600, 0.04f, 0.25f) } },
            { Cue.Beacon, new[] { new ToneSegment(660, 660, 0.06f, 0.4f, Waveform.Triangle), new ToneSegment(990, 990, 0.04f, 0.25f, Waveform.Triangle) } },
            { Cue.Step, new[] { new ToneSegment(140, 110, 0.04f, 0.3f, Waveform.Noise) } },
        };

        private static readonly Dictionary<Cue, string> Names = new Dictionary<Cue, string>
        {
            { Cue.Navigate, Loc.N("Navigate") },
            { Cue.Wrap, Loc.N("Wrap") },
            { Cue.Edge, Loc.N("Edge") },
            { Cue.Activate, Loc.N("Activate") },
            { Cue.ToggleOn, Loc.N("Toggle on") },
            { Cue.ToggleOff, Loc.N("Toggle off") },
            { Cue.Open, Loc.N("Open") },
            { Cue.Close, Loc.N("Close") },
            { Cue.StepLand, Loc.N("Step land") },
            { Cue.StepWater, Loc.N("Step water") },
            { Cue.StepOccupied, Loc.N("Step occupied") },
            { Cue.StepFog, Loc.N("Step fog") },
            { Cue.PlaceValid, Loc.N("Place valid") },
            { Cue.PlaceInvalid, Loc.N("Place invalid") },
            { Cue.Placed, Loc.N("Placed") },
            { Cue.Error, Loc.N("Error") },
            { Cue.Notify, Loc.N("Notify") },
            { Cue.Alert, Loc.N("Alert") },
            { Cue.Value, Loc.N("Value") },
            { Cue.Beacon, Loc.N("Beacon") },
            { Cue.Step, Loc.N("Step") },
        };

        private static readonly Dictionary<Cue, string> Meanings = new Dictionary<Cue, string>
        {
            { Cue.Navigate, Loc.N("Moved to another item") },
            { Cue.Wrap, Loc.N("A list wrapped around from the last item to the first") },
            { Cue.Edge, Loc.N("Reached the edge of a list or of the map") },
            { Cue.Activate, Loc.N("A button or command was activated") },
            { Cue.ToggleOn, Loc.N("A check box or option turned on") },
            { Cue.ToggleOff, Loc.N("A check box or option turned off") },
            { Cue.Open, Loc.N("A window, menu or mode opened") },
            { Cue.Close, Loc.N("A window, menu or mode closed") },
            { Cue.StepLand, Loc.N("Map cursor moved onto open land") },
            { Cue.StepWater, Loc.N("Map cursor moved onto water") },
            { Cue.StepOccupied, Loc.N("Map cursor moved onto a building, stone or iron") },
            { Cue.StepFog, Loc.N("Map cursor moved onto unexplored land") },
            { Cue.PlaceValid, Loc.N("While placing: this spot is valid") },
            { Cue.PlaceInvalid, Loc.N("While placing: this spot is not valid") },
            { Cue.Placed, Loc.N("A building was placed, or you arrived at your target") },
            { Cue.Error, Loc.N("Something is not possible right now") },
            { Cue.Notify, Loc.N("A kingdom notification") },
            { Cue.Alert, Loc.N("Danger: raid, dragon, fire or plague") },
            { Cue.Value, Loc.N("A slider or value changed; the pitch follows the value") },
            { Cue.Beacon, Loc.N("Target beacon: left or right ear for west or east, higher pitch north, lower south, faster when closer") },
            { Cue.Step, Loc.N("One step of an auto walk") },
        };

        /// <summary>Short spoken name of a cue ("Place valid").</summary>
        public static string Name(Cue cue) => Names.TryGetValue(cue, out var n) ? Loc.T(n) : TextUtil.Humanize(cue.ToString());

        /// <summary>What the cue means, for the sound preview list.</summary>
        public static string Meaning(Cue cue) => Meanings.TryGetValue(cue, out var m) ? Loc.T(m) : Name(cue);

        public static IEnumerable<Cue> All => (Cue[])Enum.GetValues(typeof(Cue));

        public static ToneSegment[] Get(Cue cue) => Recipes.TryGetValue(cue, out var r) ? r : Recipes[Cue.Navigate];

        public static bool HasRecipe(Cue cue) => Recipes.ContainsKey(cue);
    }
}

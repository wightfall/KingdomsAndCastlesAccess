using System.Collections.Generic;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess
{
    /// <summary>Plays the procedurally generated sound cues on a 2D audio source.</summary>
    internal sealed class AudioCues : MonoBehaviour
    {
        private static AudioCues inst;
        private readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();
        private AudioSource source;

        private void Awake()
        {
            inst = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.ignoreListenerVolume = false;
            source.bypassEffects = true;
            source.bypassListenerEffects = true;
            source.bypassReverbZones = true;
            foreach (Cue cue in CueLibrary.All)
            {
                float[] data = ToneSynth.Render(ToneSynth.DefaultSampleRate, CueLibrary.Get(cue));
                var clip = AudioClip.Create("kcaccess_" + cue, data.Length, 1, ToneSynth.DefaultSampleRate, false);
                clip.SetData(data, 0);
                clips[cue] = clip;
            }
        }

        private AudioSource beaconSource;

        /// <summary>Plays a cue panned left / right (-1..1) on its own source, used by the navigation beacon.</summary>
        internal static void PlayPanned(Cue cue, float pan, float pitch)
        {
            if (inst == null || !Plugin.CfgCues.Value) return;
            if (!inst.clips.TryGetValue(cue, out var clip)) return;
            if (inst.beaconSource == null)
            {
                inst.beaconSource = inst.gameObject.AddComponent<AudioSource>();
                inst.beaconSource.playOnAwake = false;
                inst.beaconSource.spatialBlend = 0f;
                inst.beaconSource.ignoreListenerPause = true;
                inst.beaconSource.bypassEffects = true;
                inst.beaconSource.bypassListenerEffects = true;
            }
            inst.beaconSource.panStereo = Mathf.Clamp(pan, -1f, 1f);
            inst.beaconSource.pitch = Mathf.Clamp(pitch, 0.25f, 3f);
            inst.beaconSource.PlayOneShot(clip, Volume(cue));
        }

        internal static void Play(Cue cue, float pitchScale = 1f)
        {
            if (inst == null || !Plugin.CfgCues.Value) return;
            if (!inst.clips.TryGetValue(cue, out var clip)) return;
            inst.source.pitch = Mathf.Clamp(pitchScale, 0.25f, 3f);
            inst.source.PlayOneShot(clip, Volume(cue));
        }

        /// <summary>The overall cue volume times the cue's own volume (mod settings), at most full volume.</summary>
        private static float Volume(Cue cue) => Mathf.Clamp01(Plugin.CfgCueVolume.Value * Plugin.CueLevels.Scale(cue));

        /// <summary>
        /// Plays a cue to show what it sounds like (key list, mod settings); the beacon pings once from the left (west)
        /// and once from the right (east). Says so when sound cues are off.
        /// </summary>
        internal static void Preview(Cue cue)
        {
            if (!Plugin.CfgCues.Value)
            {
                A.Say(KeyHelp.Resolve(Loc.T("Sound cues are off. {Mute} turns them on.")), force: true);
                return;
            }
            if (cue != Cue.Beacon)
            {
                Play(cue);
                return;
            }
            PlayPanned(Cue.Beacon, -0.9f, 1f);
            if (inst != null) inst.StartCoroutine(inst.Later(0.5f, () => PlayPanned(Cue.Beacon, 0.9f, 1f)));
        }

        private System.Collections.IEnumerator Later(float delay, System.Action action)
        {
            yield return new WaitForSecondsRealtime(delay);
            action();
        }
    }
}

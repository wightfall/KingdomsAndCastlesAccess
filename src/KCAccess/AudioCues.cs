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

        internal static void Play(Cue cue, float pitchScale = 1f)
        {
            if (inst == null || !Plugin.CfgCues.Value) return;
            if (!inst.clips.TryGetValue(cue, out var clip)) return;
            inst.source.pitch = Mathf.Clamp(pitchScale, 0.25f, 3f);
            inst.source.PlayOneShot(clip, Plugin.CfgCueVolume.Value);
        }
    }
}

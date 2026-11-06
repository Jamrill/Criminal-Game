using UnityEngine;
using UnityEngine.Audio;

namespace JuegoCriminal.Audio
{
    [CreateAssetMenu(menuName="Criminal Game/Ambient music")]
    public sealed class AmbientMusicSettings : ScriptableObject
    {
        public bool musicEnabled=true;
        public AudioClip[] tracks=System.Array.Empty<AudioClip>();
        [Range(0,1)] public float volume=.22f;
        public AudioMixerGroup mixerGroup;
        [Min(.1f)] public float fadeSeconds=2.5f;
        [Header("Random silence between cues (seconds)")]
        public Vector2 menuFirstDelay=new(2,5),menuSilence=new(30,65);
        public Vector2 worldFirstDelay=new(45,90),worldSilence=new(120,240);
        [Min(0)] public float calmSecondsBeforeMusic=10;
    }
}

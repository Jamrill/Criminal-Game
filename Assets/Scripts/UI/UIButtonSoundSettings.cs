using UnityEngine;
using UnityEngine.Audio;

namespace JuegoCriminal.UI
{
    [CreateAssetMenu(menuName="Criminal Game/UI button sounds")]
    public sealed class UIButtonSoundSettings : ScriptableObject
    {
        public bool soundEnabled=true;
        public AudioClip appear,hover,click;
        [Tooltip("Optional exit whoosh. Uses the entrance clip when empty.")]
        public AudioClip disappear;
        [Range(0,1)] public float disappearVolume=.4f;
        [Range(0,1)] public float masterVolume=.5f;
        [Range(0,1)] public float appearVolume=.45f,hoverVolume=.4f,clickVolume=.55f;
        [Tooltip("Optional UI mixer group. No 3D attenuation is applied.")]
        public AudioMixerGroup mixerGroup;
        [Tooltip("Coalesce simultaneous button appearances into a single whoosh.")]
        [Min(0)] public float appearCooldown=.15f;
        [Min(0)] public float hoverCooldown=.05f;
    }
}

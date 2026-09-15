using UnityEngine;

namespace JuegoCriminal.Core
{
    [DisallowMultipleComponent]
    public sealed class ReflectionZone : MonoBehaviour
    {
        [Header("Fixed world-space volume (metres, not scaled by Transform)")]
        [SerializeField] private Vector3 volumeSize = new Vector3(20f, 8f, 20f);
        [SerializeField] private Vector3 captureOffset = new Vector3(0f, -2f, 0f);
        [SerializeField, Min(0f)] private float blendDistance = 2f;
        [SerializeField] private bool boxProjection = true;
        [SerializeField] private int importance = 10;
        [Header("Capture budget")]
        [SerializeField, Min(1f)] private float captureDistance = 80f;
        [SerializeField] private LayerMask reflectedLayers = ~(1 << 5);
        [SerializeField, Range(128, 512)] private int maxResolution = 256;
        [SerializeField, Min(0.5f)] private float refreshInterval = 2f;
        [SerializeField, Min(0.2f)] private float transitionSeconds = 2f;

        public Vector3 Size => new Vector3(Mathf.Max(1f, volumeSize.x), Mathf.Max(1f, volumeSize.y), Mathf.Max(1f, volumeSize.z));
        public Vector3 CapturePosition => transform.position + captureOffset;
        public float BlendDistance => Mathf.Clamp(blendDistance, 0f, Mathf.Min(Size.x, Mathf.Min(Size.y, Size.z)) * 0.5f);
        public bool BoxProjection => boxProjection;
        public int Importance => importance;
        public float CaptureDistance => Mathf.Max(1f, captureDistance);
        public int ReflectedLayers => reflectedLayers.value;
        public int MaxResolution => Mathf.Clamp(Mathf.ClosestPowerOfTwo(maxResolution), 128, 512);
        public float RefreshInterval => Mathf.Max(0.5f, refreshInterval);
        public float TransitionSeconds => Mathf.Max(0.2f, transitionSeconds);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = boxProjection ? Color.cyan : Color.yellow;
            Gizmos.DrawWireCube(transform.position, Size);
            Gizmos.DrawWireSphere(CapturePosition, 0.25f);
        }
    }
}

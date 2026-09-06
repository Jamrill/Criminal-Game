using System;
using UnityEngine;

namespace JuegoCriminal.Vehicles
{
    /// <summary>Animates the four authored hinges without requiring an input or driving system.</summary>
    [DisallowMultipleComponent]
    public sealed class VelaroOpenableParts : MonoBehaviour
    {
        [Serializable]
        public sealed class HingedPart
        {
            public Transform pivot;
            [Tooltip("Rotation relative to the saved closed pose, in the pivot's local axes.")]
            public Vector3 openEuler;
            [SerializeField, HideInInspector] private Quaternion closedRotation = Quaternion.identity;
            [SerializeField, HideInInspector] private bool hasClosedPose;
            [NonSerialized] private float current;
            [NonSerialized] private float target;

            public float Amount => current;

            public void CaptureClosedPose()
            {
                if (pivot == null) return;
                closedRotation = pivot.localRotation;
                hasClosedPose = true;
                current = target = 0f;
            }

            public void Initialize()
            {
                if (!hasClosedPose) CaptureClosedPose();
                current = target = 0f;
                Apply();
            }

            public void SetAmount(float amount, bool instant)
            {
                if (!hasClosedPose) CaptureClosedPose();
                target = Mathf.Clamp01(amount);
                if (instant)
                {
                    current = target;
                    Apply();
                }
            }

            public void Tick(float step)
            {
                if (Mathf.Approximately(current, target)) return;
                current = Mathf.MoveTowards(current, target, step);
                Apply();
            }

            private void Apply()
            {
                if (pivot == null || !hasClosedPose) return;
                float eased = Mathf.SmoothStep(0f, 1f, current);
                pivot.localRotation = closedRotation *
                    Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(openEuler), eased);
            }
        }

        [SerializeField, Min(0.01f)] private float openingSeconds = 0.85f;
        [SerializeField] private HingedPart leftDoor = new HingedPart { openEuler = new Vector3(0f, 65f, 0f) };
        [SerializeField] private HingedPart rightDoor = new HingedPart { openEuler = new Vector3(0f, -65f, 0f) };
        [SerializeField] private HingedPart hood = new HingedPart { openEuler = new Vector3(-62f, 0f, 0f) };
        [SerializeField] private HingedPart trunk = new HingedPart { openEuler = new Vector3(70f, 0f, 0f) };

        public HingedPart LeftDoor => leftDoor;
        public HingedPart RightDoor => rightDoor;
        public HingedPart Hood => hood;
        public HingedPart Trunk => trunk;

        private void Awake()
        {
            leftDoor.Initialize();
            rightDoor.Initialize();
            hood.Initialize();
            trunk.Initialize();
        }

        private void Update()
        {
            float step = Time.deltaTime / Mathf.Max(0.01f, openingSeconds);
            leftDoor.Tick(step);
            rightDoor.Tick(step);
            hood.Tick(step);
            trunk.Tick(step);
        }

        public void Configure(Transform doorL, Transform doorR, Transform hoodPivot, Transform trunkPivot)
        {
            leftDoor.pivot = doorL;
            rightDoor.pivot = doorR;
            hood.pivot = hoodPivot;
            trunk.pivot = trunkPivot;
            CaptureClosedPose();
        }

        // Public one-argument setters can also be connected to UnityEvents.
        public void SetLeftDoor(float amount) => leftDoor.SetAmount(amount, !Application.isPlaying);
        public void SetRightDoor(float amount) => rightDoor.SetAmount(amount, !Application.isPlaying);
        public void SetHood(float amount) => hood.SetAmount(amount, !Application.isPlaying);
        public void SetTrunk(float amount) => trunk.SetAmount(amount, !Application.isPlaying);

        public void SetAll(float amount, bool instant = false)
        {
            instant |= !Application.isPlaying;
            leftDoor.SetAmount(amount, instant);
            rightDoor.SetAmount(amount, instant);
            hood.SetAmount(amount, instant);
            trunk.SetAmount(amount, instant);
        }

        [ContextMenu("Open all parts")]
        public void OpenAll() => SetAll(1f);

        [ContextMenu("Close all parts")]
        public void CloseAll() => SetAll(0f);

        [ContextMenu("Capture current transforms as CLOSED pose")]
        public void CaptureClosedPose()
        {
            leftDoor.CaptureClosedPose();
            rightDoor.CaptureClosedPose();
            hood.CaptureClosedPose();
            trunk.CaptureClosedPose();
        }
    }
}

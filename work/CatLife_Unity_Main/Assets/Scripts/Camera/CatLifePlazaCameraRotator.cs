using UnityEngine;

namespace CatLife.CameraControls
{
    [DisallowMultipleComponent]
    public sealed class CatLifePlazaCameraRotator : MonoBehaviour
    {
        [SerializeField] private Vector3 fixedPosition = new Vector3(0.1f, 1.9f, 1.2f);
        [SerializeField] private float yawDegrees = 182.600662f;
        [SerializeField] private float basePitchDegrees = 6.653839f;
        [SerializeField] private float pitchDegrees = 6.653839f;
        [SerializeField] private float rollDegrees = 0.361923f;
        [SerializeField] private float pitchOffsetDegrees;
        [SerializeField] private float maxPitchOffsetDegrees = 45f;
        [SerializeField] private float degreesPerSecond = 10f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool useSceneTransformOnAwake;

        private float rotationDirection;

        public Vector3 FixedPosition
        {
            get { return fixedPosition; }
            set
            {
                fixedPosition = value;
                ApplyPose();
            }
        }

        public float YawDegrees
        {
            get { return yawDegrees; }
            set
            {
                yawDegrees = value;
                ApplyPose();
            }
        }

        public float PitchDegrees
        {
            get { return pitchDegrees; }
            set
            {
                pitchDegrees = ClampPitch(value);
                pitchOffsetDegrees = Mathf.Clamp(pitchDegrees - basePitchDegrees, -MaxPitchOffsetDegrees, MaxPitchOffsetDegrees);
                ApplyPose();
            }
        }

        public float RollDegrees
        {
            get { return rollDegrees; }
            set
            {
                rollDegrees = NormalizeSignedAngle(value);
                ApplyPose();
            }
        }

        public float BasePitchDegrees
        {
            get { return basePitchDegrees; }
            set
            {
                basePitchDegrees = ClampPitch(value);
                pitchOffsetDegrees = Mathf.Clamp(pitchDegrees - basePitchDegrees, -MaxPitchOffsetDegrees, MaxPitchOffsetDegrees);
                ApplyPose();
            }
        }

        public float MaxPitchOffsetDegrees
        {
            get { return Mathf.Max(0f, maxPitchOffsetDegrees); }
            set
            {
                maxPitchOffsetDegrees = Mathf.Max(0f, value);
                pitchOffsetDegrees = Mathf.Clamp(pitchOffsetDegrees, -MaxPitchOffsetDegrees, MaxPitchOffsetDegrees);
                pitchDegrees = ClampPitch(basePitchDegrees + pitchOffsetDegrees);
                ApplyPose();
            }
        }

        public float PitchOffsetNormalized
        {
            get
            {
                float maxOffset = MaxPitchOffsetDegrees;
                return maxOffset > 0f ? Mathf.Clamp(pitchOffsetDegrees / maxOffset, -1f, 1f) : 0f;
            }
        }

        public float DegreesPerSecond
        {
            get { return degreesPerSecond; }
            set { degreesPerSecond = Mathf.Max(0f, value); }
        }

        private void Awake()
        {
            if (useSceneTransformOnAwake)
            {
                CaptureCurrentTransformAsBaseline();
            }

            ApplyPose();
        }

        private void LateUpdate()
        {
            StepRotation(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
            ApplyPose();
        }

        public void SetRotationDirection(float direction)
        {
            SetYawRotationDirection(direction);
        }

        public void SetYawRotationDirection(float direction)
        {
            rotationDirection = Mathf.Approximately(direction, 0f) ? 0f : Mathf.Sign(direction);
        }

        public void SetPitchOffsetNormalized(float normalizedOffset)
        {
            pitchOffsetDegrees = Mathf.Clamp(normalizedOffset, -1f, 1f) * MaxPitchOffsetDegrees;
            pitchDegrees = ClampPitch(basePitchDegrees + pitchOffsetDegrees);
            ApplyPose();
        }

        public void StopRotation()
        {
            rotationDirection = 0f;
        }

        public void CaptureCurrentTransformAsBaseline()
        {
            fixedPosition = transform.position;
            Vector3 euler = transform.eulerAngles;
            yawDegrees = Mathf.Repeat(euler.y, 360f);
            pitchDegrees = ClampPitch(NormalizeSignedAngle(euler.x));
            rollDegrees = NormalizeSignedAngle(euler.z);
            basePitchDegrees = pitchDegrees;
            pitchOffsetDegrees = 0f;
        }

        public void StepRotation(float deltaTime)
        {
            if (Mathf.Approximately(rotationDirection, 0f))
            {
                return;
            }

            yawDegrees = Mathf.Repeat(yawDegrees + Mathf.Sign(rotationDirection) * degreesPerSecond * Mathf.Max(0f, deltaTime), 360f);
        }

        public void ApplyPose()
        {
            transform.position = fixedPosition;
            transform.rotation = Quaternion.Euler(pitchDegrees, yawDegrees, rollDegrees);
        }

        private static float ClampPitch(float value)
        {
            return Mathf.Clamp(value, -89f, 89f);
        }

        private static float NormalizeSignedAngle(float value)
        {
            float normalized = Mathf.Repeat(value + 180f, 360f) - 180f;
            return Mathf.Approximately(normalized, -180f) ? 180f : normalized;
        }
    }
}

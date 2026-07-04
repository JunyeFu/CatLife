using CatLife.Recognition;
using UnityEngine;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    public sealed class CatNeedModel : MonoBehaviour
    {
        [SerializeField] private CatNeedState current = CatNeedState.CreateDefault();
        [SerializeField] private float adaptationSpeed = 0.65f;
        [SerializeField] private float focusCompanionshipBuildRate = 0.08f;
        [SerializeField] private float affectionBuildOnInteraction = 0.08f;

        private float lastCatTapAgo = 999f;
        private float lastCatLongPressAgo = 999f;

        public CatNeedState Current { get { return current.Clamp01(); } }

        private void Awake()
        {
            current = current.Clamp01();
        }

        public void Tick(
            RecognitionSnapshot snapshot,
            RealtimeFeatureSnapshot realtimeFeatures,
            bool hasRealtimeFeatures,
            float unscaledDeltaTime)
        {
            float dt = Mathf.Max(0f, unscaledDeltaTime);
            float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, adaptationSpeed) * dt);
            bool focused = snapshot.IsFocused;
            float distraction = hasRealtimeFeatures ? realtimeFeatures.distraction01 : 0f;
            float arousal = hasRealtimeFeatures ? realtimeFeatures.arousal01 : snapshot.userArousal;
            float focusScore = hasRealtimeFeatures ? realtimeFeatures.focusScore01 : snapshot.focusConfidence;
            float interactionReadiness = snapshot.interactionReadiness;

            CatNeedState target = current;
            target.curiosity01 = focused
                ? Mathf.Lerp(0.2f, 0.45f, 1f - distraction)
                : Mathf.Lerp(0.5f, 0.9f, interactionReadiness);
            target.sleepiness01 = focused
                ? Mathf.Lerp(0.35f, 0.68f, Mathf.Clamp01(focusScore))
                : Mathf.Lerp(0.12f, 0.38f, Mathf.Clamp01(1f - arousal));
            target.safety01 = Mathf.Clamp01(0.86f - distraction * 0.42f - arousal * 0.22f);
            target.interruptionSensitivity01 = focused
                ? Mathf.Clamp01(0.48f + distraction * 0.38f + snapshot.focusConfidence * 0.16f)
                : Mathf.Clamp01(0.18f + distraction * 0.28f);
            target.focusCompanionship01 = focused
                ? Mathf.Clamp01(current.focusCompanionship01 + focusCompanionshipBuildRate * dt)
                : Mathf.Lerp(current.focusCompanionship01, 0.28f, blend);

            bool catTapped = snapshot.secondsSinceCatTap < lastCatTapAgo || snapshot.secondsSinceCatLongPress < lastCatLongPressAgo;
            if (catTapped)
            {
                current.affection01 = Mathf.Clamp01(current.affection01 + affectionBuildOnInteraction);
            }

            target.affection01 = Mathf.Clamp01(Mathf.Lerp(current.affection01, snapshot.companionshipNeed, blend * 0.55f));
            current = Lerp(current, target, blend).Clamp01();
            lastCatTapAgo = snapshot.secondsSinceCatTap;
            lastCatLongPressAgo = snapshot.secondsSinceCatLongPress;
        }

        private static CatNeedState Lerp(CatNeedState from, CatNeedState to, float t)
        {
            return new CatNeedState
            {
                curiosity01 = Mathf.Lerp(from.curiosity01, to.curiosity01, t),
                sleepiness01 = Mathf.Lerp(from.sleepiness01, to.sleepiness01, t),
                affection01 = Mathf.Lerp(from.affection01, to.affection01, t),
                safety01 = Mathf.Lerp(from.safety01, to.safety01, t),
                interruptionSensitivity01 = Mathf.Lerp(from.interruptionSensitivity01, to.interruptionSensitivity01, t),
                focusCompanionship01 = Mathf.Lerp(from.focusCompanionship01, to.focusCompanionship01, t)
            };
        }
    }
}

using System;
using CatLife.Cat;
using CatLife.Recognition;
using CatLife.SceneInteraction;

namespace CatLife.LLM
{
    [Serializable]
    public sealed class CatPromptContext
    {
        public string appMode;
        public string catBehaviorState;
        public string catEmotion;
        public float catMoveSpeed01;
        public string focusState;
        public string userIntent;
        public string interruptionRisk;
        public float focusConfidence;
        public float interactionReadiness;
        public bool focusSessionActive;
        public float secondsSinceLastInteraction;
        public float secondsSinceLastFocusStart;
        public float tapRate1s;
        public float tapRate5s;
        public int pageSwitches30s;
        public float focusScore01;
        public float arousal01;
        public float distraction01;
        public string safeLocalSummary;
        public string[] recentEvents;
        public bool hasSceneInteraction;
        public string sceneInteractionPointId;
        public string sceneInteractionDisplayName;
        public string[] sceneInteractionTags;
        public string sceneInteractionMotionCue;
        public float secondsSinceSceneInteraction;
        public string sceneInteractionSummary;
        public string behaviorPolicy;
        public string[] allowedOutputs;
        public string[] blockedOutputs;
        public bool privacyModeEnabled;

        public static CatPromptContext Create(
            RecognitionSnapshot snapshot,
            CatBehaviorState behaviorState,
            float catMoveSpeed01,
            string catEmotion,
            string[] recentEvents)
        {
            return Create(
                snapshot,
                behaviorState,
                catMoveSpeed01,
                catEmotion,
                recentEvents,
                default(RealtimeFeatureSnapshot),
                false);
        }

        public static CatPromptContext Create(
            RecognitionSnapshot snapshot,
            CatBehaviorState behaviorState,
            float catMoveSpeed01,
            string catEmotion,
            string[] recentEvents,
            RealtimeFeatureSnapshot realtimeFeatures,
            bool hasRealtimeFeatures)
        {
            return Create(
                snapshot,
                behaviorState,
                catMoveSpeed01,
                catEmotion,
                recentEvents,
                realtimeFeatures,
                hasRealtimeFeatures,
                default(SceneInteractionPayload),
                null,
                999f);
        }

        public static CatPromptContext Create(
            RecognitionSnapshot snapshot,
            CatBehaviorState behaviorState,
            float catMoveSpeed01,
            string catEmotion,
            string[] recentEvents,
            RealtimeFeatureSnapshot realtimeFeatures,
            bool hasRealtimeFeatures,
            SceneInteractionPayload scenePayload,
            SceneInteractionPoint scenePoint,
            float secondsSinceSceneInteraction)
        {
            return new CatPromptContext
            {
                appMode = "companion",
                catBehaviorState = behaviorState.ToString(),
                catEmotion = string.IsNullOrEmpty(catEmotion) ? "calm" : catEmotion,
                catMoveSpeed01 = catMoveSpeed01,
                focusState = snapshot.focusState.ToString(),
                userIntent = snapshot.userIntent.ToString(),
                interruptionRisk = snapshot.interruptionRisk.ToString(),
                focusConfidence = snapshot.focusConfidence,
                interactionReadiness = snapshot.interactionReadiness,
                focusSessionActive = hasRealtimeFeatures && realtimeFeatures.isFocusSessionActive,
                secondsSinceLastInteraction = hasRealtimeFeatures ? realtimeFeatures.secondsSinceLastInteraction : 999f,
                secondsSinceLastFocusStart = hasRealtimeFeatures ? realtimeFeatures.secondsSinceLastFocusStart : 999f,
                tapRate1s = hasRealtimeFeatures ? realtimeFeatures.tapRate1s : 0f,
                tapRate5s = hasRealtimeFeatures ? realtimeFeatures.tapRate5s : 0f,
                pageSwitches30s = hasRealtimeFeatures ? realtimeFeatures.pageSwitches30s : 0,
                focusScore01 = hasRealtimeFeatures ? realtimeFeatures.focusScore01 : snapshot.focusConfidence,
                arousal01 = hasRealtimeFeatures ? realtimeFeatures.arousal01 : snapshot.userArousal,
                distraction01 = hasRealtimeFeatures ? realtimeFeatures.distraction01 : 0f,
                safeLocalSummary = snapshot.safeLocalSummary,
                recentEvents = recentEvents ?? new string[0],
                hasSceneInteraction = scenePayload.IsValid && scenePoint != null,
                sceneInteractionPointId = scenePayload.IsValid ? scenePayload.pointId : string.Empty,
                sceneInteractionDisplayName = scenePayload.IsValid ? scenePayload.displayName : string.Empty,
                sceneInteractionTags = scenePayload.IsValid ? scenePayload.tags ?? new string[0] : new string[0],
                sceneInteractionMotionCue = scenePoint != null ? scenePoint.PreferredAnimationTag : string.Empty,
                secondsSinceSceneInteraction = scenePayload.IsValid ? Math.Max(0f, secondsSinceSceneInteraction) : 999f,
                sceneInteractionSummary = BuildSceneInteractionSummary(scenePayload, scenePoint, secondsSinceSceneInteraction),
                behaviorPolicy = "Suggest only high-level cat behavior weights and one optional short companion line. Unity keeps authority over pathfinding, animation, timing, and UI.",
                allowedOutputs = new[]
                {
                    "moodBias",
                    "roamWeightBias",
                    "quietIdleWeightBias",
                    "socialResponseWeightBias",
                    "suggestedLine",
                    "showBubble"
                },
                blockedOutputs = new[]
                {
                    "coordinates",
                    "transform commands",
                    "physics commands",
                    "NavMesh commands",
                    "Animator state names",
                    "screen capture requests",
                    "raw user input",
                    "cross-app content"
                },
                privacyModeEnabled = true
            };
        }

        private static string BuildSceneInteractionSummary(
            SceneInteractionPayload payload,
            SceneInteractionPoint point,
            float secondsSinceSceneInteraction)
        {
            if (!payload.IsValid || point == null)
            {
                return "sceneInteraction=none";
            }

            string displayName = string.IsNullOrEmpty(payload.displayName)
                ? payload.pointId
                : payload.displayName;
            string tags = payload.tags != null && payload.tags.Length > 0
                ? string.Join(",", payload.tags)
                : "none";
            return "sceneInteraction=" + payload.pointId +
                "; label=" + displayName +
                "; tags=" + tags +
                "; motionCue=" + point.PreferredAnimationTag +
                "; ageSeconds=" + Math.Max(0f, secondsSinceSceneInteraction).ToString("0.0");
        }
    }
}

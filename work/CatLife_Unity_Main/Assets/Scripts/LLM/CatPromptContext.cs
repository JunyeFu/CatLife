using System;
using CatLife.Cat;
using CatLife.Recognition;

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
        public string safeLocalSummary;
        public string[] recentEvents;
        public bool privacyModeEnabled;

        public static CatPromptContext Create(
            RecognitionSnapshot snapshot,
            CatBehaviorState behaviorState,
            float catMoveSpeed01,
            string catEmotion,
            string[] recentEvents)
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
                safeLocalSummary = snapshot.safeLocalSummary,
                recentEvents = recentEvents ?? new string[0],
                privacyModeEnabled = true
            };
        }
    }
}

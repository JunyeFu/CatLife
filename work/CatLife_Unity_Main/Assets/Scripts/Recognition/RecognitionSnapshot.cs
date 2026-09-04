using System;
using UnityEngine;

namespace CatLife.Recognition
{
    public enum FocusState
    {
        Unknown = 0,
        NonFocus = 1,
        Focused = 2,
        TransitioningIn = 3,
        TransitioningOut = 4
    }

    public enum UserIntent
    {
        None = 0,
        IgnoreCat = 1,
        ObserveCat = 2,
        WantsInteraction = 3,
        WantsQuiet = 4,
        NeedsComfort = 5,
        Busy = 6
    }

    public enum InterruptionRisk
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    [Serializable]
    public struct RecognitionSnapshot
    {
        public float realtimeSinceStartup;
        public FocusState focusState;
        public UserIntent userIntent;
        public InterruptionRisk interruptionRisk;
        [Range(0f, 1f)] public float focusConfidence;
        [Range(0f, 1f)] public float companionshipNeed;
        [Range(0f, 1f)] public float userArousal;
        [Range(0f, 1f)] public float interactionReadiness;
        public AttentionBand attentionBand;
        public AttentionTrend attentionTrend;
        public bool userPresent;
        public float secondsSinceCatTap;
        public float secondsSinceCatLongPress;
        public string safeLocalSummary;

        public bool IsFocused
        {
            get
            {
                return focusState == FocusState.Focused ||
                       focusState == FocusState.TransitioningIn;
            }
        }

        public static RecognitionSnapshot CreateDefault()
        {
            return new RecognitionSnapshot
            {
                realtimeSinceStartup = Time.realtimeSinceStartup,
                focusState = FocusState.NonFocus,
                userIntent = UserIntent.None,
                interruptionRisk = InterruptionRisk.Low,
                focusConfidence = 0.5f,
                companionshipNeed = 0.2f,
                userArousal = 0.2f,
                interactionReadiness = 0.2f,
                attentionBand = AttentionBand.Transitioning,
                attentionTrend = AttentionTrend.Steady,
                userPresent = true,
                secondsSinceCatTap = 999f,
                secondsSinceCatLongPress = 999f,
                safeLocalSummary = "default_local_mock"
            };
        }
    }
}

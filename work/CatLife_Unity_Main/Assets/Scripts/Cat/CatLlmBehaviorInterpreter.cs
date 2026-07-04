using CatLife.LLM;
using CatLife.Recognition;

namespace CatLife.Cat
{
    public static class CatLlmBehaviorInterpreter
    {
        public const string ActionNone = "none";
        public const string ActionQuietIdle = "quiet_idle";
        public const string ActionSoftRoam = "soft_roam";
        public const string ActionSocialResponse = "social_response";

        public static bool TryBuildLocalDecision(
            LLMBehaviorSuggestion suggestion,
            RecognitionSnapshot snapshot,
            CatNeedState needs,
            out CatBehaviorDecision decision,
            out string reason)
        {
            decision = default(CatBehaviorDecision);
            string action = NormalizeAction(suggestion != null ? suggestion.recommendedLocalAction : "");
            bool focused = snapshot.IsFocused;

            switch (action)
            {
                case ActionQuietIdle:
                    decision = CatBehaviorDecision.Create(
                        CatBehaviorState.IdleBreath,
                        focused ? 1.1f : 1.4f,
                        focused ? 4f : 6f,
                        focused ? 54 : 34,
                        CatActionInterruptPolicy.DropIfBusy,
                        focused,
                        "llm_quiet_idle",
                        new[] { "quiet", "shade" });
                    reason = "llm_action_quiet_idle";
                    return true;

                case ActionSoftRoam:
                    decision = CatBehaviorDecision.Create(
                        focused ? CatBehaviorState.FocusedRoam : CatBehaviorState.Roam,
                        0f,
                        focused ? 3f : 2f,
                        focused ? 48 : 32,
                        CatActionInterruptPolicy.DropIfBusy,
                        true,
                        "llm_soft_roam",
                        focused ? new[] { "quiet", "path", "shade" } : new[] { "path", "garden", "plaza" });
                    reason = "llm_action_soft_roam";
                    return true;

                case ActionSocialResponse:
                    if (focused && needs.interruptionSensitivity01 > 0.45f)
                    {
                        reason = "llm_social_response_suppressed_in_focus";
                        return false;
                    }

                    decision = CatBehaviorDecision.Create(
                        focused ? CatBehaviorState.HeadTiltListen : CatBehaviorState.TailWagHappy,
                        focused ? 1.0f : 1.4f,
                        focused ? 10f : 6f,
                        focused ? 42 : 36,
                        CatActionInterruptPolicy.QueueIfMoving,
                        false,
                        "llm_social_response",
                        new[] { "near_home", "plaza" });
                    reason = "llm_action_social_response";
                    return true;

                default:
                    reason = "llm_action_none";
                    return false;
            }
        }

        public static bool ShouldShowBubble(
            LLMBehaviorSuggestion suggestion,
            RecognitionSnapshot snapshot,
            CatNeedState needs,
            out string safeLine,
            out string reason)
        {
            safeLine = suggestion != null ? suggestion.suggestedLine : "";
            if (suggestion == null || !suggestion.showBubble || string.IsNullOrEmpty(safeLine))
            {
                reason = "llm_bubble_not_requested";
                return false;
            }

            if (snapshot.IsFocused &&
                (snapshot.interruptionRisk != InterruptionRisk.Low || needs.focusCompanionship01 < 0.72f))
            {
                reason = "llm_bubble_suppressed_in_focus";
                return false;
            }

            reason = "llm_bubble_allowed";
            return true;
        }

        public static string NormalizeAction(string action)
        {
            switch ((action ?? "").ToLowerInvariant())
            {
                case "":
                case "none":
                    return ActionNone;
                case "quiet_idle":
                case "quiet_companion":
                    return ActionQuietIdle;
                case "soft_roam":
                case "gentle_return":
                    return ActionSoftRoam;
                case "social_response":
                case "reward_after_focus":
                    return ActionSocialResponse;
                default:
                    return ActionNone;
            }
        }
    }
}

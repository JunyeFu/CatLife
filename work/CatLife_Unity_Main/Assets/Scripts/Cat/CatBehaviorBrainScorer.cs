using CatLife.LLM;
using CatLife.Recognition;
using UnityEngine;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    public sealed class CatBehaviorBrainScorer : MonoBehaviour
    {
        [SerializeField] private float cooldownPenaltyWeight = 999f;
        [SerializeField] private float repeatPenaltyWeight = 28f;
        [SerializeField] private float randomnessJitter = 0.08f;

        public bool TryDecide(
            RecognitionSnapshot snapshot,
            CatNeedState needs,
            CatBehaviorMemory memory,
            LLMBehaviorSuggestion suggestion,
            out CatBehaviorDecision decision)
        {
            bool focused = snapshot.IsFocused;
            Candidate[] candidates = focused ? BuildFocusCandidates(snapshot, needs, suggestion) : BuildNonFocusCandidates(snapshot, needs, suggestion);
            float now = Time.time;
            float total = 0f;

            for (int i = 0; i < candidates.Length; i++)
            {
                Candidate candidate = candidates[i];
                float score = Mathf.Max(0f, candidate.score);
                if (memory != null)
                {
                    score -= memory.GetRepeatPenalty(candidate.state) * repeatPenaltyWeight;
                    if (memory.GetCooldownRemaining(candidate.state, now) > 0f)
                    {
                        score -= cooldownPenaltyWeight;
                    }
                }

                score *= Random.Range(1f - randomnessJitter, 1f + randomnessJitter);
                candidates[i].score = Mathf.Max(0f, score);
                total += candidates[i].score;
            }

            if (total <= 0.01f)
            {
                decision = CatBehaviorDecision.Create(
                    focused ? CatBehaviorState.IdleBreath : CatBehaviorState.Roam,
                    focused ? 0.9f : 0f,
                    0f,
                    focused ? 50 : 10,
                    CatActionInterruptPolicy.DropIfBusy,
                    focused,
                    "scorer_fallback");
                return true;
            }

            float roll = Random.Range(0f, total);
            for (int i = 0; i < candidates.Length; i++)
            {
                roll -= candidates[i].score;
                if (roll > 0f)
                {
                    continue;
                }

                decision = ToDecision(candidates[i], focused);
                return true;
            }

            decision = ToDecision(candidates[candidates.Length - 1], focused);
            return true;
        }

        private static Candidate[] BuildNonFocusCandidates(
            RecognitionSnapshot snapshot,
            CatNeedState needs,
            LLMBehaviorSuggestion suggestion)
        {
            float roamBias = suggestion != null ? suggestion.roamWeightBias : 0f;
            float socialBias = suggestion != null ? suggestion.socialResponseWeightBias : 0f;
            return new[]
            {
                Candidate.Create(CatBehaviorState.Roam, 58f + needs.curiosity01 * 32f + roamBias * 100f, "nonfocus_roam", "plaza", "path", "garden", "bench", "shade"),
                Candidate.Create(CatBehaviorState.CuriousSniff, 9f + needs.curiosity01 * 20f, "nonfocus_curiosity", "garden", "flower", "bench"),
                Candidate.Create(CatBehaviorState.LookBack, 8f + (1f - needs.safety01) * 24f, "nonfocus_safety_check", "path", "edge"),
                Candidate.Create(CatBehaviorState.TailWagHappy, 6f + needs.affection01 * 20f + socialBias * 40f, "nonfocus_social", "plaza", "near_home"),
                Candidate.Create(CatBehaviorState.StretchYawn, 5f + needs.sleepiness01 * 22f, "nonfocus_rest", "shade", "quiet")
            };
        }

        private static Candidate[] BuildFocusCandidates(
            RecognitionSnapshot snapshot,
            CatNeedState needs,
            LLMBehaviorSuggestion suggestion)
        {
            float roamBias = suggestion != null ? suggestion.roamWeightBias : 0f;
            float quietBias = suggestion != null ? suggestion.quietIdleWeightBias : 0f;
            float socialBias = suggestion != null ? suggestion.socialResponseWeightBias : 0f;
            float risk = snapshot.interruptionRisk == InterruptionRisk.High ? 1f :
                snapshot.interruptionRisk == InterruptionRisk.Medium ? 0.55f : 0f;

            return new[]
            {
                Candidate.Create(CatBehaviorState.FocusedRoam, 22f + needs.curiosity01 * 20f + roamBias * 70f - needs.interruptionSensitivity01 * 18f, "focus_slow_roam", "quiet", "path", "shade"),
                Candidate.Create(CatBehaviorState.IdleBreath, 24f + needs.sleepiness01 * 28f + quietBias * 80f + needs.focusCompanionship01 * 12f, "focus_quiet_idle", "quiet", "shade"),
                Candidate.Create(CatBehaviorState.EarTwitchAlert, 13f + risk * 24f + (1f - needs.safety01) * 20f, "focus_low_interrupt_alert", "quiet", "edge"),
                Candidate.Create(CatBehaviorState.AlertLook, 6f + risk * 18f, "focus_attention_check", "path", "edge"),
                Candidate.Create(CatBehaviorState.TailWagHappy, 3f + needs.affection01 * 8f + socialBias * 20f - needs.interruptionSensitivity01 * 10f, "focus_soft_social", "near_home", "quiet")
            };
        }

        private static CatBehaviorDecision ToDecision(Candidate candidate, bool focused)
        {
            return CatBehaviorDecision.Create(
                candidate.state,
                GetHoldSeconds(candidate.state, focused),
                GetCooldownSeconds(candidate.state, focused),
                focused ? 50 : 10,
                CatActionInterruptPolicy.DropIfBusy,
                focused,
                candidate.reason,
                candidate.preferredInterestTags);
        }

        private static float GetHoldSeconds(CatBehaviorState state, bool focused)
        {
            switch (state)
            {
                case CatBehaviorState.FocusedRoam:
                case CatBehaviorState.Roam:
                    return 0f;
                case CatBehaviorState.StretchYawn:
                    return focused ? 1.4f : 2.1f;
                case CatBehaviorState.CuriousSniff:
                case CatBehaviorState.HeadTiltListen:
                case CatBehaviorState.TailWagHappy:
                    return 1.4f;
                default:
                    return focused ? 0.9f : 1.4f;
            }
        }

        private static float GetCooldownSeconds(CatBehaviorState state, bool focused)
        {
            switch (state)
            {
                case CatBehaviorState.FocusedRoam:
                case CatBehaviorState.Roam:
                    return focused ? 1.5f : 0.6f;
                case CatBehaviorState.IdleBreath:
                    return focused ? 0.4f : 1.5f;
                case CatBehaviorState.TailWagHappy:
                    return focused ? 10f : 5f;
                case CatBehaviorState.StretchYawn:
                    return 12f;
                default:
                    return focused ? 6f : 4f;
            }
        }

        private struct Candidate
        {
            public CatBehaviorState state;
            public float score;
            public string reason;
            public string[] preferredInterestTags;

            public static Candidate Create(CatBehaviorState state, float score, string reason, params string[] preferredInterestTags)
            {
                return new Candidate
                {
                    state = state,
                    score = score,
                    reason = reason,
                    preferredInterestTags = preferredInterestTags
                };
            }
        }
    }
}

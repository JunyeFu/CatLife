using System;
using UnityEngine;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    public sealed class CatBehaviorMemory : MonoBehaviour
    {
        private const int RecentStateCount = 4;
        private const int RecentInterestPointCount = 4;
        private const int InterestVisitSlotCount = 16;
        private readonly CatBehaviorState[] recentStates = new CatBehaviorState[RecentStateCount];
        private readonly string[] recentInterestPointIds = new string[RecentInterestPointCount];
        private readonly string[] visitIds = new string[InterestVisitSlotCount];
        private readonly int[] visitCounts = new int[InterestVisitSlotCount];
        private readonly float[] cooldownUntilByState = new float[GetStateSlotCount()];

        [SerializeField] private CatBehaviorState lastState = CatBehaviorState.None;
        [SerializeField] private float lastStateEnteredAt;
        [SerializeField] private float holdUntilTime;
        [SerializeField] private float lastUserInteractionTime = -999f;
        [SerializeField] private string lastInterestPointId = "";

        public CatBehaviorState LastState { get { return lastState; } }
        public float HoldUntilTime { get { return holdUntilTime; } }
        public string LastInterestPointId { get { return lastInterestPointId; } }

        public bool IsHolding(float now)
        {
            return now < holdUntilTime;
        }

        public void RecordUserInteraction()
        {
            lastUserInteractionTime = Time.time;
        }

        public float SecondsSinceUserInteraction(float now)
        {
            return lastUserInteractionTime > -100f ? now - lastUserInteractionTime : 999f;
        }

        public void RecordDecision(CatBehaviorDecision decision, float now)
        {
            RecordState(decision.state, now, decision.holdSeconds);
            SetCooldown(decision.state, now + Mathf.Max(0f, decision.cooldownSeconds));
        }

        public void RecordState(CatBehaviorState state, float now, float holdSeconds)
        {
            lastState = state;
            lastStateEnteredAt = now;
            holdUntilTime = now + Mathf.Max(0f, holdSeconds);
            for (int i = recentStates.Length - 1; i > 0; i--)
            {
                recentStates[i] = recentStates[i - 1];
            }

            recentStates[0] = state;
        }

        public void SetCooldown(CatBehaviorState state, float until)
        {
            int index = (int)state;
            if (index >= 0 && index < cooldownUntilByState.Length)
            {
                cooldownUntilByState[index] = until;
            }
        }

        public float GetCooldownRemaining(CatBehaviorState state, float now)
        {
            int index = (int)state;
            if (index < 0 || index >= cooldownUntilByState.Length)
            {
                return 0f;
            }

            return Mathf.Max(0f, cooldownUntilByState[index] - now);
        }

        public float GetRepeatPenalty(CatBehaviorState state)
        {
            float penalty = 0f;
            for (int i = 0; i < recentStates.Length; i++)
            {
                if (recentStates[i] != state)
                {
                    continue;
                }

                penalty += i == 0 ? 0.55f : 0.18f;
            }

            return penalty;
        }

        public void RecordInterestPointVisit(string interestPointId)
        {
            if (string.IsNullOrEmpty(interestPointId))
            {
                return;
            }

            lastInterestPointId = interestPointId;
            for (int i = recentInterestPointIds.Length - 1; i > 0; i--)
            {
                recentInterestPointIds[i] = recentInterestPointIds[i - 1];
            }

            recentInterestPointIds[0] = interestPointId;
            int slot = FindVisitSlot(interestPointId);
            if (slot >= 0)
            {
                visitCounts[slot] = Mathf.Min(999, visitCounts[slot] + 1);
            }
        }

        public float GetInterestPointRepeatPenalty(string interestPointId)
        {
            if (string.IsNullOrEmpty(interestPointId))
            {
                return 0f;
            }

            float penalty = 0f;
            for (int i = 0; i < recentInterestPointIds.Length; i++)
            {
                if (recentInterestPointIds[i] == interestPointId)
                {
                    penalty += i == 0 ? 1f : 0.32f;
                }
            }

            int slot = FindExistingVisitSlot(interestPointId);
            if (slot >= 0)
            {
                penalty += Mathf.Min(1.2f, visitCounts[slot] * 0.12f);
            }

            return penalty;
        }

        private int FindVisitSlot(string interestPointId)
        {
            int existing = FindExistingVisitSlot(interestPointId);
            if (existing >= 0)
            {
                return existing;
            }

            int empty = -1;
            int minSlot = 0;
            for (int i = 0; i < visitIds.Length; i++)
            {
                if (string.IsNullOrEmpty(visitIds[i]) && empty < 0)
                {
                    empty = i;
                }

                if (visitCounts[i] < visitCounts[minSlot])
                {
                    minSlot = i;
                }
            }

            int slot = empty >= 0 ? empty : minSlot;
            visitIds[slot] = interestPointId;
            visitCounts[slot] = 0;
            return slot;
        }

        private int FindExistingVisitSlot(string interestPointId)
        {
            for (int i = 0; i < visitIds.Length; i++)
            {
                if (visitIds[i] == interestPointId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int GetStateSlotCount()
        {
            Array values = Enum.GetValues(typeof(CatBehaviorState));
            int max = 0;
            for (int i = 0; i < values.Length; i++)
            {
                max = Mathf.Max(max, (int)values.GetValue(i));
            }

            return max + 1;
        }
    }
}

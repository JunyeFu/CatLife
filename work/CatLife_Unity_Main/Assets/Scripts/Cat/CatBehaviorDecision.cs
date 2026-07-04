using System;

namespace CatLife.Cat
{
    [Serializable]
    public struct CatBehaviorDecision
    {
        public CatBehaviorState state;
        public float holdSeconds;
        public float cooldownSeconds;
        public int priority;
        public CatActionInterruptPolicy interruptPolicy;
        public bool canInterruptByMove;
        public string reason;

        public bool IsValid
        {
            get { return state != CatBehaviorState.None; }
        }

        public bool IsLocomotion
        {
            get { return state == CatBehaviorState.Roam || state == CatBehaviorState.FocusedRoam; }
        }

        public static CatBehaviorDecision Create(
            CatBehaviorState state,
            float holdSeconds,
            float cooldownSeconds,
            int priority,
            CatActionInterruptPolicy interruptPolicy,
            bool canInterruptByMove,
            string reason)
        {
            return new CatBehaviorDecision
            {
                state = state,
                holdSeconds = holdSeconds,
                cooldownSeconds = cooldownSeconds,
                priority = priority,
                interruptPolicy = interruptPolicy,
                canInterruptByMove = canInterruptByMove,
                reason = string.IsNullOrEmpty(reason) ? "scored_behavior" : reason
            };
        }
    }
}

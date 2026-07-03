namespace CatLife.Cat
{
    public enum CatActionSource
    {
        Ambient = 0,
        User = 1,
        Ui = 2,
        Session = 3,
        Recognition = 4,
        Llm = 5,
        System = 6
    }

    public enum CatActionInterruptPolicy
    {
        PlayNow = 0,
        QueueIfMoving = 1,
        DropIfBusy = 2,
        ReplaceAmbient = 3
    }

    public struct CatActionRequest
    {
        public CatBehaviorState state;
        public CatActionSource source;
        public CatActionInterruptPolicy interruptPolicy;
        public int priority;
        public float cooldownSeconds;
        public float maxDelaySeconds;
        public float createdAt;
        public bool canInterruptByMove;
        public string reason;

        public static CatActionRequest Create(
            CatBehaviorState state,
            CatActionSource source,
            string reason,
            int priority,
            float cooldownSeconds,
            float maxDelaySeconds,
            CatActionInterruptPolicy interruptPolicy,
            bool canInterruptByMove)
        {
            return new CatActionRequest
            {
                state = state,
                source = source,
                reason = reason,
                priority = priority,
                cooldownSeconds = cooldownSeconds,
                maxDelaySeconds = maxDelaySeconds,
                interruptPolicy = interruptPolicy,
                canInterruptByMove = canInterruptByMove
            };
        }
    }
}

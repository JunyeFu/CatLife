namespace CatLife.LLM
{
    public static class LocalTemplateFallback
    {
        public static FocusFeedback Generate(BehaviorFeatureSummary summary, string reason)
        {
            string text;
            if (summary.interruptCount >= 3)
            {
                text = "这轮中间停顿偏多，但你已经回来了。下一轮可以从短一点开始。";
            }
            else if (summary.focusDurationSec >= 25 * 60)
            {
                text = "这段专注很稳，猫咪已经把动作放慢，安静陪你完成了。";
            }
            else if (summary.focusDurationSec >= 10 * 60)
            {
                text = "刚才这段节奏不错，猫咪会继续在旁边轻轻陪你。";
            }
            else
            {
                text = "记录已保存。先不用急，猫咪会陪你慢慢进入状态。";
            }

            return FocusFeedback.Create(text, "local_template", true, reason);
        }
    }
}

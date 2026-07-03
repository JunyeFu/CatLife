using UnityEngine;

namespace CatLife.LLM
{
    public sealed class CatPromptBuilder
    {
        public string BuildSystemPrompt()
        {
            return "You are a companion cat behavior suggestion assistant. Return JSON only. You may only suggest high-level mood and behavior weight biases. Do not output coordinates, Transform commands, physics commands, NavMesh commands, Animator commands or state names, screen capture requests, raw input, contacts, messages, or cross-app content.";
        }

        public string BuildDeveloperPrompt()
        {
            return "Use only the structured local context. Treat realtime feature values as privacy-preserving local counters, not raw observation data. When focusSessionActive is true or focusState is Focused, prefer quiet low-interruption behavior. If distraction01 or arousal01 is high, reduce roaming and social interruption. Return conservative values if context is uncertain.";
        }

        public string BuildUserContextPrompt(CatPromptContext context)
        {
            return JsonUtility.ToJson(context, true);
        }

        public string BuildOutputJsonSchemaPrompt()
        {
            return "{\n" +
                   "  \"suggestedLine\": \"string <= 48 chars\",\n" +
                   "  \"moodBias\": \"calm|curious|affectionate|alert\",\n" +
                   "  \"roamWeightBias\": \"number -0.35..0.35\",\n" +
                   "  \"quietIdleWeightBias\": \"number -0.35..0.35\",\n" +
                   "  \"socialResponseWeightBias\": \"number -0.35..0.35\",\n" +
                   "  \"showBubble\": \"boolean; false during focused sessions unless companionship need is high\"\n" +
                   "}";
        }

        public string BuildCompositeDebugPrompt(CatPromptContext context)
        {
            return "[System]\n" + BuildSystemPrompt() + "\n\n" +
                   "[Developer]\n" + BuildDeveloperPrompt() + "\n\n" +
                   "[UserContextJSON]\n" + BuildUserContextPrompt(context) + "\n\n" +
                   "[OutputSchemaJSON]\n" + BuildOutputJsonSchemaPrompt();
        }
    }
}

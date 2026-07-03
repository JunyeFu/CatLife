using UnityEngine;

namespace CatLife.LLM
{
    public sealed class CatPromptBuilder
    {
        public string BuildSystemPrompt()
        {
            return "You are a companion cat behavior suggestion assistant. Return JSON only. Do not output coordinates, Transform commands, physics commands, NavMesh commands, Animator commands, screen capture requests, raw input, contacts, messages, or cross-app content.";
        }

        public string BuildDeveloperPrompt()
        {
            return "Use only the structured local context. When focusState is Focused, prefer quiet low-interruption behavior. Return conservative values if context is uncertain.";
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
                   "  \"showBubble\": \"boolean\"\n" +
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

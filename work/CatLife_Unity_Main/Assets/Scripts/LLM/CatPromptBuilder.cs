using UnityEngine;

namespace CatLife.LLM
{
    public sealed class CatPromptBuilder
    {
        public string BuildSystemPrompt()
        {
            return "You are a companion cat behavior suggestion assistant. Return strict JSON only, with no Markdown and no explanation. Write suggestedLine as one short Simplified Chinese sentence. You may only suggest high-level mood and behavior weight biases. Do not output coordinates, position, Transform commands, physics commands, NavMesh commands, Animator commands or state names, camera or microphone requests, screen capture requests, raw input, contacts, messages, package names, clipboard data, or cross-app content.";
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
                   "  \"version\": \"catlife.bluelm.feedback.v1\",\n" +
                   "  \"suggestedLine\": \"string <= 24 zh chars or 48 ascii chars\",\n" +
                   "  \"showBubble\": \"boolean; false during focused sessions unless companionship need is high\",\n" +
                   "  \"moodBias\": \"quiet|calm|curious|affectionate|alert\",\n" +
                   "  \"roamWeightBias\": \"number -0.35..0.35\",\n" +
                   "  \"quietIdleWeightBias\": \"number -0.35..0.35\",\n" +
                   "  \"socialResponseWeightBias\": \"number -0.35..0.35\",\n" +
                   "  \"recommendedLocalAction\": \"none|quiet_idle|soft_roam|social_response\",\n" +
                   "  \"rawTextRequested\": false,\n" +
                   "  \"coordinateCommandIncluded\": false,\n" +
                   "  \"animatorCommandIncluded\": false,\n" +
                   "  \"navMeshCommandIncluded\": false,\n" +
                   "  \"transformCommandIncluded\": false,\n" +
                   "  \"privacyInferenceIncluded\": false\n" +
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

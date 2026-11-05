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
            return "Use only the structured local context. Treat realtime feature values as privacy-preserving local counters, not raw observation data. When userIntent is PostSession, supply gentleReminder, returnSuggestion and catReactionReason, each a nonempty Simplified Chinese sentence of at most 24 characters. Give a gentle acknowledgement, one small next task step, and a reason for the cat reaction. Do not diagnose the user, give medication instructions, shame or threaten abandonment, or invent their task. Local action mapping: quiet_idle means resting, soft_roam means stretching in place, social_response means a friendly gesture; unlocks may substitute a paw wave. When focusSessionActive is true or focusState is Focused, prefer quiet low-interruption behavior. If distraction01 or arousal01 is high, reduce roaming and social interruption. Return conservative values if context is uncertain.";
        }

        public string BuildUserContextPrompt(CatPromptContext context)
        {
            string prompt;
            if (context != null && context.behaviorStatsDisabled && context.userIntent == "PostSession")
                prompt = "PostSession; focusState=Reward; privacyModeEnabled=true; " + context.safeLocalSummary
                    + "\nBehavior statistics were not collected. Do not infer focus, stability, touches, background activity or distraction. Acknowledge only the completed duration and suggest one tiny next task step. Return the required JSON fields.";
            else prompt = JsonUtility.ToJson(context, true);
            if (context != null && context.userIntent == "PostSession")
                prompt += "\nreturnSuggestion must name one concrete, tiny task-return action: identify the next unfinished step, open the material needed for that step, or write its first line. Do not invent the user's task subject. Merely saying drink water, rest, continue, or take a small step is insufficient. Interaction counters and foreground stability are app activity proxies, not observations of mental attention. Never describe the user as distracted, anxious, unfocused or highly focused, and never report a distraction score. Explain the cat reaction as a gentle response to session completion, not a judgment of the user.";
            return prompt;
        }

        public string BuildOutputJsonSchemaPrompt()
        {
            return "{\n" +
                   "  \"version\": \"catlife.bluelm.feedback.v1\",\n" +
                   "  \"suggestedLine\": \"string <= 24 zh chars or 48 ascii chars\",\n" +
                   "  \"gentleReminder\": \"short Simplified Chinese acknowledgement\",\n" +
                   "  \"returnSuggestion\": \"short Simplified Chinese next task step\",\n" +
                   "  \"catReactionReason\": \"short Simplified Chinese reason for reaction\",\n" +
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

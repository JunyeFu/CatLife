package com.catlife.bluelm;

import org.json.JSONObject;

public final class BlueLmPromptBuilder {
    private BlueLmPromptBuilder() {
    }

    public static String buildPromptEnvelope(String requestJson) {
        try {
            JSONObject request = new JSONObject(requestJson);
            JSONObject envelope = new JSONObject();
            envelope.put("schemaVersion", "catlife.bluelm.prompt.v1");
            envelope.put("requestId", request.optString("requestId", ""));
            envelope.put("system", request.optString("systemPrompt", ""));
            envelope.put("developer", request.optString("developerPrompt", ""));
            envelope.put("userContextJson", request.optString("userContextJson", ""));
            envelope.put("outputSchemaJson", request.optString("outputSchemaJson", ""));
            envelope.put("strictJsonOnly", true);
            envelope.put("privacyPolicy", "aggregate_enum_only_no_raw_text_no_screen_no_clipboard_no_package");
            return envelope.toString();
        } catch (Throwable ignored) {
            return "{\"schemaVersion\":\"catlife.bluelm.prompt.v1\",\"strictJsonOnly\":true,\"error\":\"PROMPT_BUILD_FAILED\"}";
        }
    }

    public static String outputSchemaReminder() {
        return "Return only JSON with version,suggestedLine,showBubble,moodBias,roamWeightBias,quietIdleWeightBias,socialResponseWeightBias,recommendedLocalAction and all safety booleans false.";
    }
}

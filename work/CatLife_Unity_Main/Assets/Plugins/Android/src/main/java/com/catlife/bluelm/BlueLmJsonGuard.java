package com.catlife.bluelm;

import org.json.JSONObject;

public final class BlueLmJsonGuard {
    private static final String[] BLOCKED_INPUT_TERMS = {
        "rawtext",
        "raw_text",
        "raw text",
        "x/y",
        "\"x\"",
        "\"y\"",
        "packagename",
        "package name",
        "clipboard",
        "screencontent",
        "screen content"
    };

    private static final String[] BLOCKED_OUTPUT_TERMS = {
        "animator",
        "navmesh",
        "transform",
        "position",
        "coordinate",
        "camera",
        "microphone",
        "clipboard",
        "package"
    };

    private BlueLmJsonGuard() {
    }

    public static BlueLmEngine.GuardResult validateRequest(String requestJson) {
        if (requestJson == null || requestJson.length() == 0) {
            return new BlueLmEngine.GuardResult(false, "", "REQUEST_EMPTY");
        }

        String requestId = "";
        String userContextJson = "";
        try {
            JSONObject object = new JSONObject(requestJson);
            requestId = object.optString("requestId", "");
            userContextJson = object.optString("userContextJson", "");
        } catch (Throwable ignored) {
            return new BlueLmEngine.GuardResult(false, "", "REQUEST_JSON_INVALID");
        }

        if (requestId.length() == 0) {
            return new BlueLmEngine.GuardResult(false, "", "REQUEST_ID_MISSING");
        }

        String lower = userContextJson.toLowerCase();
        for (int i = 0; i < BLOCKED_INPUT_TERMS.length; i++) {
            if (lower.contains(BLOCKED_INPUT_TERMS[i])) {
                return new BlueLmEngine.GuardResult(false, requestId, "BLOCKED_INPUT_" + BLOCKED_INPUT_TERMS[i].replace(' ', '_'));
            }
        }

        return new BlueLmEngine.GuardResult(true, requestId, "PASSED");
    }

    public static boolean isSafeOutput(String outputJson) {
        return validateOutput(outputJson).ok;
    }

    public static BlueLmEngine.GuardResult validateOutput(String outputJson) {
        if (outputJson == null || outputJson.length() == 0) {
            return new BlueLmEngine.GuardResult(false, "", "OUTPUT_EMPTY");
        }

        JSONObject object;
        try {
            object = new JSONObject(outputJson);
        } catch (Throwable ignored) {
            return new BlueLmEngine.GuardResult(false, "", "OUTPUT_JSON_INVALID");
        }

        String version = object.optString("version", "");
        if (!"catlife.bluelm.feedback.v1".equals(version)) {
            return new BlueLmEngine.GuardResult(false, "", "OUTPUT_VERSION_INVALID");
        }

        if (object.optBoolean("rawTextRequested", false) ||
            object.optBoolean("coordinateCommandIncluded", false) ||
            object.optBoolean("animatorCommandIncluded", false) ||
            object.optBoolean("navMeshCommandIncluded", false) ||
            object.optBoolean("transformCommandIncluded", false) ||
            object.optBoolean("privacyInferenceIncluded", false)) {
            return new BlueLmEngine.GuardResult(false, "", "OUTPUT_UNSAFE_FLAGS");
        }

        String lower = object.optString("suggestedLine", "").toLowerCase();
        for (int i = 0; i < BLOCKED_OUTPUT_TERMS.length; i++) {
            if (lower.contains(BLOCKED_OUTPUT_TERMS[i])) {
                return new BlueLmEngine.GuardResult(false, "", "BLOCKED_OUTPUT_" + BLOCKED_OUTPUT_TERMS[i]);
            }
        }

        return new BlueLmEngine.GuardResult(true, "", "PASSED");
    }
}

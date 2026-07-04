package com.catlife.bluelm;

import android.util.Log;
import com.unity3d.player.UnityPlayer;

public final class BlueLmUnityCallback {
    public static final String DEFAULT_CALLBACK_OBJECT = "CatLifeBlueLmCallbackReceiver";
    private static final String TAG = "CatLifeBlueLM";
    private static final String UNITY_METHOD = "OnBlueLmEvent";

    private BlueLmUnityCallback() {
    }

    public static void sendInit(String callbackObject, boolean ok, int code, String message, String modelPath) {
        String safeMessage = safe(message);
        Log.i(TAG, "BlueLM init ok=" + ok + " code=" + code + " message=" + safeMessage);
        sendEvent(callbackObject, buildEvent("bluelm_init", ok, code, ok ? "ok" : "error", safeMessage, modelPath));
    }

    public static void sendGenerate(String callbackObject, String requestId, boolean ok, int code, String message, String responseJson) {
        String safeMessage = safe(message);
        Log.i(TAG, "BlueLM generate complete requestId=" + safe(requestId) + " ok=" + ok + " code=" + code + " message=" + safeMessage + " payloadBytes=" + safe(responseJson).length());
        sendEvent(callbackObject, buildEvent(safe(requestId), ok, code, ok ? "ok" : "error", safeMessage, responseJson));
    }

    private static void sendEvent(String callbackObject, String eventJson) {
        String target = safe(callbackObject);
        if (target.length() == 0) {
            target = DEFAULT_CALLBACK_OBJECT;
        }

        try {
            UnityPlayer.UnitySendMessage(target, UNITY_METHOD, eventJson);
        } catch (Throwable throwable) {
            Log.e(TAG, "UnitySendMessage failed: " + throwable.getClass().getSimpleName());
        }
    }

    private static String buildEvent(String requestId, boolean ok, int code, String status, String message, String payload) {
        StringBuilder sb = new StringBuilder();
        sb.append('{');
        sb.append("\"schemaVersion\":\"catlife.bluelm.android_event.v1\",");
        sb.append("\"requestId\":\"").append(json(safe(requestId))).append("\",");
        sb.append("\"ok\":").append(ok).append(',');
        sb.append("\"success\":").append(ok).append(',');
        sb.append("\"status\":\"").append(json(safe(status))).append("\",");
        sb.append("\"code\":").append(code).append(',');
        sb.append("\"source\":\"").append(ok ? "bluelm_on_device" : "local_template").append("\",");
        if (ok) {
            sb.append("\"content\":\"").append(json(safe(payload))).append("\",");
        } else {
            sb.append("\"error\":\"").append(json(safe(message))).append("\",");
        }
        sb.append("\"message\":\"").append(json(safe(message))).append("\"");
        sb.append('}');
        return sb.toString();
    }

    private static String safe(String value) {
        return value == null ? "" : value;
    }

    private static String json(String value) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < value.length(); i++) {
            char c = value.charAt(i);
            switch (c) {
                case '\\':
                    sb.append("\\\\");
                    break;
                case '"':
                    sb.append("\\\"");
                    break;
                case '\n':
                    sb.append("\\n");
                    break;
                case '\r':
                    sb.append("\\r");
                    break;
                case '\t':
                    sb.append("\\t");
                    break;
                default:
                    sb.append(c);
                    break;
            }
        }
        return sb.toString();
    }
}

package com.catlife.recognition;

import android.util.Log;
import com.unity3d.player.UnityPlayer;

public final class CatLifeEventBridge {
    public static final String DEFAULT_CALLBACK_OBJECT = "CatLifeAndroidBehaviorEventBridge";

    private static final String TAG = "CatLifeBehavior";
    private static final String UNITY_METHOD = "OnBehaviorEvent";

    private CatLifeEventBridge() {
    }

    public static void sendEvent(String eventType, String routeId, String zoneId) {
        sendEvent(eventType, routeId, zoneId, -1, -1f, 0f, 0f, "android");
    }

    public static void sendEvent(
        String eventType,
        String routeId,
        String zoneId,
        int durationMs,
        float deltaLen,
        float scrollDy,
        float velocity,
        String source) {
        if (!isAllowedEventType(eventType)) {
            Log.w(TAG, "Rejected behavior event type=" + safe(eventType));
            return;
        }

        String eventJson = buildEvent(
            eventType,
            sanitizeLabel(routeId, "route"),
            sanitizeLabel(zoneId, "zone"),
            durationMs,
            deltaLen,
            scrollDy,
            velocity,
            sanitizeSource(source));
        UnityPlayer.UnitySendMessage(DEFAULT_CALLBACK_OBJECT, UNITY_METHOD, eventJson);
        Log.i(TAG, "Behavior event sent type=" + safe(eventType) + " route=" + sanitizeLabel(routeId, "route"));
    }

    public static void notifyAppPause() {
        sendEvent("AppPause", "application", "lifecycle", -1, -1f, 0f, 0f, "lifecycle");
    }

    public static void notifyAppResume() {
        sendEvent("AppResume", "application", "lifecycle", -1, -1f, 0f, 0f, "lifecycle");
    }

    public static boolean isAllowedEventType(String eventType) {
        String value = safe(eventType);
        return value.equals("UiTap") ||
            value.equals("UiButton") ||
            value.equals("UiScroll") ||
            value.equals("PageEnter") ||
            value.equals("PageExit") ||
            value.equals("FocusStart") ||
            value.equals("FocusCancel") ||
            value.equals("FocusComplete") ||
            value.equals("Unlock") ||
            value.equals("CatTap") ||
            value.equals("CatLongPress") ||
            value.equals("ScenePointTap") ||
            value.equals("AppPause") ||
            value.equals("AppResume");
    }

    private static String buildEvent(
        String eventType,
        String routeId,
        String zoneId,
        int durationMs,
        float deltaLen,
        float scrollDy,
        float velocity,
        String source) {
        StringBuilder sb = new StringBuilder();
        sb.append('{');
        sb.append("\"schemaVersion\":\"catlife.behavior_event.v1\",");
        sb.append("\"eventType\":\"").append(json(eventType)).append("\",");
        sb.append("\"routeId\":\"").append(json(routeId)).append("\",");
        sb.append("\"zoneId\":\"").append(json(zoneId)).append("\",");
        sb.append("\"sceneState\":\"android_runtime\",");
        sb.append("\"source\":\"").append(json(source)).append("\",");
        sb.append("\"tsMs\":").append((System.currentTimeMillis() / 100L) * 100L).append(',');
        sb.append("\"durationMs\":").append(Math.max(-1, durationMs)).append(',');
        sb.append("\"deltaLen\":").append(Math.max(-1f, deltaLen)).append(',');
        sb.append("\"scrollDy\":").append(clamp(scrollDy, -5000f, 5000f)).append(',');
        sb.append("\"velocity\":").append(clamp(velocity, -5000f, 5000f));
        sb.append('}');
        return sb.toString();
    }

    private static String sanitizeSource(String value) {
        String lower = safe(value).toLowerCase();
        if (lower.equals("android") || lower.equals("unity") || lower.equals("ui") || lower.equals("lifecycle")) {
            return lower;
        }

        return "android";
    }

    private static String sanitizeLabel(String value, String fallback) {
        String safeValue = safe(value).trim();
        if (safeValue.length() == 0) {
            return fallback;
        }

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < safeValue.length() && sb.length() < 48; i++) {
            char c = safeValue.charAt(i);
            boolean allowed =
                (c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '_' ||
                c == '-' ||
                c == '.';
            sb.append(allowed ? c : '_');
        }

        return sb.length() == 0 ? fallback : sb.toString();
    }

    private static float clamp(float value, float min, float max) {
        return Math.max(min, Math.min(max, value));
    }

    private static String safe(String value) {
        return value == null ? "" : value;
    }

    private static String json(String value) {
        StringBuilder sb = new StringBuilder();
        String safeValue = safe(value);
        for (int i = 0; i < safeValue.length(); i++) {
            char c = safeValue.charAt(i);
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

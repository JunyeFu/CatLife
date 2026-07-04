using System;
using System.Globalization;
using UnityEngine;

namespace CatLife.Recognition
{
    public static class BehaviorEventSanitizer
    {
        private const int MaxLabelLength = 48;

        private static readonly string[] AllowedEventTypes =
        {
            "UiTap",
            "UiButton",
            "UiScroll",
            "PageEnter",
            "PageExit",
            "FocusStart",
            "FocusCancel",
            "FocusComplete",
            "Unlock",
            "CatTap",
            "CatLongPress",
            "ScenePointTap",
            "AppPause",
            "AppResume"
        };

        private static readonly string[] BlockedPayloadTerms =
        {
            "rawtext",
            "raw_text",
            "rawinputtext",
            "raw_input_text",
            "rawtouch",
            "raw_touch",
            "raw_touch_path",
            "\"x\"",
            "\"y\"",
            "packagename",
            "package_name",
            "clipboard",
            "screencontent",
            "screen_content",
            "screenshot",
            "ocr",
            "contact",
            "preciselocation",
            "precise_location"
        };

        public static bool TryParseAndSanitize(string json, out BehaviorEvent safeEvent, out string reason)
        {
            safeEvent = null;
            if (string.IsNullOrEmpty(json))
            {
                reason = "behavior_event_empty";
                return false;
            }

            if (ContainsBlockedPayloadTerm(json, out reason))
            {
                return false;
            }

            BehaviorEvent raw;
            try
            {
                raw = JsonUtility.FromJson<BehaviorEvent>(json);
            }
            catch (Exception ex)
            {
                reason = "behavior_event_parse_" + ex.GetType().Name;
                return false;
            }

            return TrySanitize(raw, out safeEvent, out reason);
        }

        public static bool TrySanitize(BehaviorEvent raw, out BehaviorEvent safeEvent, out string reason)
        {
            safeEvent = null;
            if (raw == null)
            {
                reason = "behavior_event_missing";
                return false;
            }

            string eventType = NormalizeEventType(raw.eventType);
            if (string.IsNullOrEmpty(eventType))
            {
                reason = "behavior_event_type_blocked";
                return false;
            }

            safeEvent = new BehaviorEvent
            {
                schemaVersion = BehaviorEvent.ExpectedSchemaVersion,
                eventType = eventType,
                routeId = SanitizeLabel(raw.routeId, "route"),
                zoneId = SanitizeLabel(raw.zoneId, "zone"),
                sceneState = SanitizeLabel(raw.sceneState, "scene"),
                source = SanitizeSource(raw.source),
                tsMs = raw.tsMs > 0 ? RoundTimestamp(raw.tsMs) : RoundTimestamp(CurrentUnixMs()),
                durationMs = raw.durationMs >= 0 ? Mathf.Min(raw.durationMs, 12 * 60 * 60 * 1000) : -1,
                deltaLen = raw.deltaLen >= 0f ? Mathf.Min(raw.deltaLen, 1000f) : -1f,
                scrollDy = Mathf.Clamp(raw.scrollDy, -5000f, 5000f),
                velocity = Mathf.Clamp(raw.velocity, -5000f, 5000f)
            };

            reason = "passed";
            return true;
        }

        public static bool IsAllowedEventType(string eventType)
        {
            return !string.IsNullOrEmpty(NormalizeEventType(eventType));
        }

        public static string NormalizeEventType(string value)
        {
            string trimmed = (value ?? string.Empty).Trim();
            for (int i = 0; i < AllowedEventTypes.Length; i++)
            {
                if (string.Equals(trimmed, AllowedEventTypes[i], StringComparison.OrdinalIgnoreCase))
                {
                    return AllowedEventTypes[i];
                }
            }

            return string.Empty;
        }

        public static bool ContainsBlockedPayloadTerm(string json, out string reason)
        {
            string lower = (json ?? string.Empty).ToLowerInvariant();
            for (int i = 0; i < BlockedPayloadTerms.Length; i++)
            {
                if (lower.Contains(BlockedPayloadTerms[i]))
                {
                    reason = "blocked_behavior_payload_" + BlockedPayloadTerms[i].Replace("\"", string.Empty);
                    return true;
                }
            }

            reason = "passed";
            return false;
        }

        private static string SanitizeSource(string value)
        {
            string lower = (value ?? string.Empty).Trim().ToLowerInvariant();
            switch (lower)
            {
                case "android":
                case "unity":
                case "ui":
                case "lifecycle":
                    return lower;
                default:
                    return "unity";
            }
        }

        private static string SanitizeLabel(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value))
            {
                return fallback;
            }

            string trimmed = value.Trim();
            char[] chars = trimmed.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool allowed =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_' ||
                    c == '-' ||
                    c == '.';
                if (!allowed)
                {
                    chars[i] = '_';
                }
            }

            string sanitized = new string(chars);
            if (sanitized.Length > MaxLabelLength)
            {
                sanitized = sanitized.Substring(0, MaxLabelLength);
            }

            return string.IsNullOrEmpty(sanitized) ? fallback : sanitized;
        }

        private static long RoundTimestamp(long tsMs)
        {
            return (tsMs / 100L) * 100L;
        }

        private static long CurrentUnixMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}

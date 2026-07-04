using System;
using UnityEngine;

namespace CatLife.SceneInteraction
{
    [Serializable]
    public struct SceneInteractionMemory
    {
        public string lastPointId;
        public string lastAnimationTag;
        public float lastSceneClickTime;
        public float lastBubbleTime;
        public float lastFocusQuietBubbleTime;
        public float lastRewardBubbleTime;
        public float lastPointArrivalTime;
        public int repeatedPointCount;

        public static SceneInteractionMemory CreateDefault()
        {
            return new SceneInteractionMemory
            {
                lastPointId = string.Empty,
                lastAnimationTag = string.Empty,
                lastSceneClickTime = -999f,
                lastBubbleTime = -999f,
                lastFocusQuietBubbleTime = -999f,
                lastRewardBubbleTime = -999f,
                lastPointArrivalTime = -999f,
                repeatedPointCount = 0
            };
        }

        public void RecordClick(string pointId, float now)
        {
            string safePointId = string.IsNullOrEmpty(pointId) ? string.Empty : pointId;
            repeatedPointCount = safePointId == lastPointId ? Mathf.Min(999, repeatedPointCount + 1) : 0;
            lastPointId = safePointId;
            lastSceneClickTime = Mathf.Max(0f, now);
        }

        public void RecordArrival(string pointId, string animationTag, float now)
        {
            if (!string.IsNullOrEmpty(pointId))
            {
                lastPointId = pointId;
            }

            lastAnimationTag = string.IsNullOrEmpty(animationTag) ? string.Empty : animationTag;
            lastPointArrivalTime = Mathf.Max(0f, now);
        }

        public float SecondsSinceLastBubble(float now)
        {
            return lastBubbleTime > -100f ? Mathf.Max(0f, now - lastBubbleTime) : 999f;
        }

        public float SecondsSinceLastSceneClick(float now)
        {
            return lastSceneClickTime > -100f ? Mathf.Max(0f, now - lastSceneClickTime) : 999f;
        }
    }
}

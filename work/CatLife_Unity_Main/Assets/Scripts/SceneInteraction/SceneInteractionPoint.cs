using System;
using CatLife.Cat;
using UnityEngine;

namespace CatLife.SceneInteraction
{
    [DisallowMultipleComponent]
    public sealed class SceneInteractionPoint : MonoBehaviour
    {
        [Serializable]
        public struct BubbleTemplate
        {
            [TextArea(2, 4)] public string text;
            public int weight;
            public bool allowInFocus;
            public string requiredTag;

            public bool IsUsable(bool focused, SceneInteractionPayload payload)
            {
                if (focused && !allowInFocus)
                {
                    return false;
                }

                if (string.IsNullOrEmpty(text))
                {
                    return false;
                }

                return string.IsNullOrEmpty(requiredTag) || payload.HasTag(requiredTag);
            }
        }

        [Header("Identity")]
        [SerializeField] private string id = "";
        [SerializeField] private string displayName = "";
        [SerializeField] private string[] tags = new string[0];

        [Header("Behavior")]
        [SerializeField] private int priority = 50;
        [SerializeField] private bool allowedInFocus = true;
        [SerializeField] private float cooldownSeconds = 10f;
        [SerializeField] private CatBehaviorState preferredCatState = CatBehaviorState.Roam;
        [SerializeField] private string preferredAnimationTag = "observe";

        [Header("Navigation")]
        [SerializeField] private Transform navigationAnchor;

        [Header("Bubble")]
        [SerializeField] private BubbleTemplate[] bubbleTemplates = new BubbleTemplate[0];

        private float lastTriggeredAt = -999f;

        public string Id
        {
            get { return string.IsNullOrEmpty(id) ? name : id; }
        }

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(displayName) ? Id : displayName; }
        }

        public string[] Tags
        {
            get { return tags ?? new string[0]; }
        }

        public int Priority
        {
            get { return priority; }
        }

        public bool AllowedInFocus
        {
            get { return allowedInFocus; }
        }

        public float CooldownSeconds
        {
            get { return Mathf.Max(0f, cooldownSeconds); }
        }

        public CatBehaviorState PreferredCatState
        {
            get { return preferredCatState; }
        }

        public string PreferredAnimationTag
        {
            get { return string.IsNullOrEmpty(preferredAnimationTag) ? "observe" : preferredAnimationTag; }
        }

        public Transform NavigationAnchor
        {
            get { return navigationAnchor != null ? navigationAnchor : transform; }
        }

        public BubbleTemplate[] BubbleTemplates
        {
            get { return bubbleTemplates ?? new BubbleTemplate[0]; }
        }

        public bool IsCoolingDown(float now)
        {
            return now - lastTriggeredAt < CooldownSeconds;
        }

        public bool CanTrigger(bool focused, float now)
        {
            if (focused && !allowedInFocus)
            {
                return false;
            }

            return !IsCoolingDown(now);
        }

        public void MarkTriggered(float now)
        {
            lastTriggeredAt = Mathf.Max(0f, now);
        }

        public SceneInteractionPayload CreatePayload(Vector3 hitWorldPosition, float now)
        {
            return new SceneInteractionPayload(Id, DisplayName, Tags, hitWorldPosition, now);
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            string[] localTags = Tags;
            for (int i = 0; i < localTags.Length; i++)
            {
                if (string.Equals(localTags[i], tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public float GetTagMatchScore(string[] desiredTags)
        {
            if (desiredTags == null || desiredTags.Length == 0)
            {
                return 1f;
            }

            float score = 0f;
            for (int i = 0; i < desiredTags.Length; i++)
            {
                if (HasTag(desiredTags[i]))
                {
                    score += 1f;
                }
            }

            return score;
        }

        public void Configure(
            string pointId,
            string pointDisplayName,
            string[] semanticTags,
            int pointPriority,
            bool focusAllowed,
            float cooldown,
            CatBehaviorState behaviorState,
            string animationTag,
            Transform anchor)
        {
            id = string.IsNullOrEmpty(pointId) ? name : pointId;
            displayName = string.IsNullOrEmpty(pointDisplayName) ? id : pointDisplayName;
            tags = semanticTags ?? new string[0];
            priority = Mathf.Clamp(pointPriority, 0, 100);
            allowedInFocus = focusAllowed;
            cooldownSeconds = Mathf.Max(0f, cooldown);
            preferredCatState = behaviorState == CatBehaviorState.None ? CatBehaviorState.Roam : behaviorState;
            preferredAnimationTag = string.IsNullOrEmpty(animationTag) ? "observe" : animationTag;
            navigationAnchor = anchor;
        }

        private void OnValidate()
        {
            priority = Mathf.Clamp(priority, 0, 100);
            cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            if (preferredCatState == CatBehaviorState.None)
            {
                preferredCatState = CatBehaviorState.Roam;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform anchor = NavigationAnchor;
            Gizmos.color = allowedInFocus ? new Color(1f, 0.68f, 0.12f, 0.7f) : new Color(0.9f, 0.25f, 0.12f, 0.7f);
            Gizmos.DrawWireSphere(anchor.position, 0.28f);
            if (anchor != transform)
            {
                Gizmos.DrawLine(transform.position, anchor.position);
            }
        }
    }
}

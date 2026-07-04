using System;
using UnityEngine;

namespace CatLife.Cat
{
    public sealed class CatInterestPoint : MonoBehaviour
    {
        [SerializeField] private string interestId = "";
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private float nonFocusWeight = 1f;
        [SerializeField] private float focusWeight = 0.35f;
        [SerializeField] private float sampleRadius = 0.85f;
        [SerializeField] private bool allowedInFocus = true;

        public string InterestId
        {
            get { return string.IsNullOrEmpty(interestId) ? name : interestId; }
        }

        public float SampleRadius
        {
            get { return Mathf.Max(0.05f, sampleRadius); }
        }

        public float GetBaseWeight(bool focused)
        {
            if (focused && !allowedInFocus)
            {
                return 0f;
            }

            return Mathf.Max(0f, focused ? focusWeight : nonFocusWeight);
        }

        public bool MatchesAnyTag(string[] desiredTags)
        {
            if (desiredTags == null || desiredTags.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < desiredTags.Length; i++)
            {
                if (HasTag(desiredTags[i]))
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
            string id,
            string[] semanticTags,
            float nonFocus,
            float focus,
            float radius,
            bool focusAllowed)
        {
            interestId = string.IsNullOrEmpty(id) ? name : id;
            tags = semanticTags ?? Array.Empty<string>();
            nonFocusWeight = Mathf.Max(0f, nonFocus);
            focusWeight = Mathf.Max(0f, focus);
            sampleRadius = Mathf.Max(0.05f, radius);
            allowedInFocus = focusAllowed;
        }

        private bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tags == null)
            {
                return false;
            }

            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = allowedInFocus ? new Color(0.2f, 0.9f, 1f, 0.55f) : new Color(1f, 0.65f, 0.1f, 0.55f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.05f, sampleRadius));
        }
    }
}

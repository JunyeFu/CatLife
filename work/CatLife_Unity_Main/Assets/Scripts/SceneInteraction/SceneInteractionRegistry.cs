using System;
using UnityEngine;

namespace CatLife.SceneInteraction
{
    [DisallowMultipleComponent]
    public sealed class SceneInteractionRegistry : MonoBehaviour
    {
        [SerializeField] private SceneInteractionPoint[] points = new SceneInteractionPoint[0];
        [SerializeField] private bool autoCollectChildren = true;

        public int Count
        {
            get { return points != null ? points.Length : 0; }
        }

        public SceneInteractionPoint[] Points
        {
            get { return points ?? new SceneInteractionPoint[0]; }
        }

        private void Awake()
        {
            if (autoCollectChildren)
            {
                RebuildFromChildren();
            }
        }

        private void OnValidate()
        {
            if (autoCollectChildren)
            {
                RebuildFromChildren();
            }
        }

        public void SetPoints(SceneInteractionPoint[] scenePoints)
        {
            points = scenePoints ?? new SceneInteractionPoint[0];
        }

        public void RebuildFromChildren()
        {
            points = GetComponentsInChildren<SceneInteractionPoint>(true);
        }

        public bool TryGet(string pointId, out SceneInteractionPoint point)
        {
            point = null;
            if (string.IsNullOrEmpty(pointId) || points == null)
            {
                return false;
            }

            for (int i = 0; i < points.Length; i++)
            {
                SceneInteractionPoint candidate = points[i];
                if (candidate != null && string.Equals(candidate.Id, pointId, StringComparison.OrdinalIgnoreCase))
                {
                    point = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryFindBestByTags(string[] desiredTags, bool focused, float now, out SceneInteractionPoint point)
        {
            point = null;
            if (points == null || points.Length == 0)
            {
                return false;
            }

            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < points.Length; i++)
            {
                SceneInteractionPoint candidate = points[i];
                if (candidate == null || !candidate.CanTrigger(focused, now))
                {
                    continue;
                }

                float score = candidate.Priority + candidate.GetTagMatchScore(desiredTags) * 20f;
                if (score > bestScore)
                {
                    bestScore = score;
                    point = candidate;
                }
            }

            return point != null;
        }

        public bool ValidateUniqueIds(out string duplicateId)
        {
            duplicateId = string.Empty;
            if (points == null)
            {
                return true;
            }

            for (int i = 0; i < points.Length; i++)
            {
                SceneInteractionPoint left = points[i];
                if (left == null)
                {
                    continue;
                }

                for (int j = i + 1; j < points.Length; j++)
                {
                    SceneInteractionPoint right = points[j];
                    if (right == null)
                    {
                        continue;
                    }

                    if (string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        duplicateId = left.Id;
                        return false;
                    }
                }
            }

            return true;
        }
    }
}

using CatLife.Recognition;
using UnityEngine;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    public sealed class CatInterestPointRegistry : MonoBehaviour
    {
        [SerializeField] private CatInterestPoint[] interestPoints = new CatInterestPoint[0];
        [SerializeField] private float tagMatchWeight = 1.6f;
        [SerializeField] private float distancePreferenceWeight = 0.35f;
        [SerializeField] private float repeatPenaltyWeight = 2.5f;

        public int Count
        {
            get { return interestPoints != null ? interestPoints.Length : 0; }
        }

        public CatInterestPoint[] Points
        {
            get { return interestPoints; }
        }

        public void SetPoints(CatInterestPoint[] points)
        {
            interestPoints = points ?? new CatInterestPoint[0];
        }

        public bool TryPickPoint(
            RecognitionSnapshot snapshot,
            CatBehaviorDecision decision,
            CatNeedState needs,
            CatBehaviorMemory memory,
            Vector3 origin,
            out CatInterestPoint point)
        {
            if (interestPoints == null || interestPoints.Length == 0)
            {
                point = null;
                return false;
            }

            bool focused = snapshot.IsFocused || decision.state == CatBehaviorState.FocusedRoam;
            float total = 0f;
            float[] weights = GetScratchWeights();

            for (int i = 0; i < interestPoints.Length; i++)
            {
                CatInterestPoint candidate = interestPoints[i];
                float weight = Score(candidate, focused, decision, needs, memory, origin);
                weights[i] = weight;
                total += weight;
            }

            if (total <= 0.001f)
            {
                point = null;
                return false;
            }

            float roll = Random.value * total;
            for (int i = 0; i < interestPoints.Length; i++)
            {
                roll -= weights[i];
                if (roll <= 0f)
                {
                    point = interestPoints[i];
                    return point != null;
                }
            }

            point = interestPoints[interestPoints.Length - 1];
            return point != null;
        }

        private float Score(
            CatInterestPoint point,
            bool focused,
            CatBehaviorDecision decision,
            CatNeedState needs,
            CatBehaviorMemory memory,
            Vector3 origin)
        {
            if (point == null)
            {
                return 0f;
            }

            float score = point.GetBaseWeight(focused);
            if (score <= 0f)
            {
                return 0f;
            }

            string[] desiredTags = decision.preferredInterestTags;
            if (!point.MatchesAnyTag(desiredTags))
            {
                score *= 0.18f;
            }
            else
            {
                score *= 1f + point.GetTagMatchScore(desiredTags) * Mathf.Max(0f, tagMatchWeight);
            }

            if (!focused)
            {
                score *= Mathf.Lerp(0.8f, 1.35f, needs.curiosity01);
            }
            else
            {
                score *= Mathf.Lerp(0.75f, 1.25f, needs.focusCompanionship01);
            }

            float distance = Vector3.Distance(origin, point.transform.position);
            float distanceScore = Mathf.InverseLerp(0.75f, focused ? 4.5f : 9f, distance);
            score *= 1f + distanceScore * Mathf.Max(0f, distancePreferenceWeight);

            if (memory != null)
            {
                score -= memory.GetInterestPointRepeatPenalty(point.InterestId) * repeatPenaltyWeight;
            }

            return Mathf.Max(0f, score);
        }

        private float[] scratchWeights = new float[0];

        private float[] GetScratchWeights()
        {
            int count = interestPoints != null ? interestPoints.Length : 0;
            if (scratchWeights == null || scratchWeights.Length != count)
            {
                scratchWeights = new float[count];
            }

            return scratchWeights;
        }
    }
}

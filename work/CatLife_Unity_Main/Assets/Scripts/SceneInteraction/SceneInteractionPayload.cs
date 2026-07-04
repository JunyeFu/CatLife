using UnityEngine;

namespace CatLife.SceneInteraction
{
    public struct SceneInteractionPayload
    {
        public string pointId;
        public string displayName;
        public string[] tags;
        public Vector3 hitWorldPosition;
        public float occurredAt;

        public SceneInteractionPayload(
            string pointId,
            string displayName,
            string[] tags,
            Vector3 hitWorldPosition,
            float occurredAt)
        {
            this.pointId = string.IsNullOrEmpty(pointId) ? string.Empty : pointId;
            this.displayName = string.IsNullOrEmpty(displayName) ? this.pointId : displayName;
            this.tags = tags ?? new string[0];
            this.hitWorldPosition = hitWorldPosition;
            this.occurredAt = Mathf.Max(0f, occurredAt);
        }

        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(pointId); }
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tags == null)
            {
                return false;
            }

            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], tag, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

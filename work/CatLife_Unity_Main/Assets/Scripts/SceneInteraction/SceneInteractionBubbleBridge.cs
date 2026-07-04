using CatLife.Cat;
using CatLife.UI;
using UnityEngine;

namespace CatLife.SceneInteraction
{
    [DisallowMultipleComponent]
    public sealed class SceneInteractionBubbleBridge : MonoBehaviour
    {
        [SerializeField] private SceneInteractionMapper mapper;
        [SerializeField] private CatBehaviorDriver behaviorDriver;
        [SerializeField] private CatBubblePresenter bubblePresenter;
        [SerializeField] private float minBubbleIntervalSeconds = 3f;
        [SerializeField] private float focusBubbleIntervalSeconds = 10f;
        [SerializeField] private bool allowFocusedSceneBubbles = true;

        private float nextBubbleTime;
        private bool subscribed;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void NotifySceneInteraction(SceneInteractionPayload payload, SceneInteractionPoint point)
        {
            if (!payload.IsValid || point == null)
            {
                return;
            }

            bool focused = behaviorDriver != null && behaviorDriver.LatestRecognitionSnapshot.IsFocused;
            if (focused && !allowFocusedSceneBubbles)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now < nextBubbleTime)
            {
                return;
            }

            ResolveReferences();
            if (bubblePresenter == null)
            {
                return;
            }

            string message = PickBubbleText(point, payload, focused);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            bubblePresenter.Show(message, "scene_interaction");
            nextBubbleTime = now + Mathf.Max(
                0.5f,
                focused ? focusBubbleIntervalSeconds : minBubbleIntervalSeconds);
        }

        private void ResolveReferences()
        {
            if (mapper == null)
            {
                mapper = FindAnyObjectByType<SceneInteractionMapper>();
            }

            if (behaviorDriver == null)
            {
                behaviorDriver = FindAnyObjectByType<CatBehaviorDriver>();
            }

            if (bubblePresenter == null)
            {
                bubblePresenter = FindAnyObjectByType<CatBubblePresenter>();
            }
        }

        private void Subscribe()
        {
            if (subscribed || mapper == null)
            {
                return;
            }

            mapper.InteractionMapped += NotifySceneInteraction;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || mapper == null)
            {
                return;
            }

            mapper.InteractionMapped -= NotifySceneInteraction;
            subscribed = false;
        }

        private static string PickBubbleText(
            SceneInteractionPoint point,
            SceneInteractionPayload payload,
            bool focused)
        {
            SceneInteractionPoint.BubbleTemplate[] templates = point.BubbleTemplates;
            int totalWeight = 0;
            for (int i = 0; i < templates.Length; i++)
            {
                SceneInteractionPoint.BubbleTemplate template = templates[i];
                if (!template.IsUsable(focused, payload))
                {
                    continue;
                }

                totalWeight += Mathf.Max(1, template.weight);
            }

            if (totalWeight > 0)
            {
                int roll = Random.Range(0, totalWeight);
                for (int i = 0; i < templates.Length; i++)
                {
                    SceneInteractionPoint.BubbleTemplate template = templates[i];
                    if (!template.IsUsable(focused, payload))
                    {
                        continue;
                    }

                    roll -= Mathf.Max(1, template.weight);
                    if (roll < 0)
                    {
                        return template.text;
                    }
                }
            }

            string displayName = point.DisplayName;
            return string.IsNullOrEmpty(displayName)
                ? "我去那边看看。"
                : "我去" + displayName + "看看。";
        }
    }
}

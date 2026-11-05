using UnityEngine;

namespace CatLife.Cat
{
    public sealed class CatCrouchShapeDriver : MonoBehaviour
    {
        public const string ShapeName = "CL_CAT_CrouchSilhouette";

        private static readonly int SitDownHash = Animator.StringToHash("CL_CAT_StandToSit_v01_72f");
        private static readonly int SitIdleHash = Animator.StringToHash("CL_CAT_SitIdle_v01_loop_96f");
        private static readonly int LieDownHash = Animator.StringToHash("CL_CAT_SitToLie_v01_120f");
        private static readonly int FocusRestHash = Animator.StringToHash("CL_CAT_LieFocusRest_v01_loop_96f");
        private static readonly int FocusAttentionHash = Animator.StringToHash("CL_CAT_LieFocusAttention_v01_48f");
        private static readonly int WakeUpHash = Animator.StringToHash("CL_CAT_LieToSit_v01_72f");

        [SerializeField] private Animator animator;
        [SerializeField] private float blendSeconds = 0.28f;

        private SkinnedMeshRenderer[] renderers;
        private int[] shapeIndices;
        private float currentWeight;

        public bool HasCrouchShape { get; private set; }
        public float CurrentWeight => currentWeight;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            ResolveShape();
        }

        public void Bind(Animator targetAnimator)
        {
            animator = targetAnimator;
            ResolveShape();
        }

        private void ResolveShape()
        {
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            shapeIndices = new int[renderers.Length];
            HasCrouchShape = false;
            for (int index = 0; index < renderers.Length; index++)
            {
                Mesh mesh = renderers[index].sharedMesh;
                shapeIndices[index] = mesh == null ? -1 : mesh.GetBlendShapeIndex(ShapeName);
                HasCrouchShape |= shapeIndices[index] >= 0;
            }
        }

        private void LateUpdate()
        {
            if (animator == null || !HasCrouchShape) return;

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            float target = TargetWeight(current);
            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                float transition = Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime);
                target = Mathf.Lerp(TargetWeight(current), TargetWeight(next), transition);
            }

            float step = blendSeconds <= 0f ? 100f : Time.deltaTime * 100f / blendSeconds;
            currentWeight = Mathf.MoveTowards(currentWeight, target, step);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (shapeIndices[index] >= 0) renderers[index].SetBlendShapeWeight(shapeIndices[index], currentWeight);
            }
        }

        public static float TargetWeight(AnimatorStateInfo state)
        {
            int stateHash = state.shortNameHash;
            if (stateHash == SitDownHash)
                return Mathf.Clamp01(state.normalizedTime) * 100f;
            if (stateHash == SitIdleHash ||
                stateHash == LieDownHash ||
                stateHash == FocusRestHash ||
                stateHash == FocusAttentionHash ||
                stateHash == WakeUpHash)
                return 100f;
            return 0f;
        }
    }
}

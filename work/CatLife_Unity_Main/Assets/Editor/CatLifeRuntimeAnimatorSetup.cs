using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CatLife.EditorTools
{
    public static class CatLifeRuntimeAnimatorSetup
    {
        private const string MenuPath = "CatLife/Runtime/Setup Runtime Animator States";
        private const string ControllerPath = "Assets/Art/Cat/Animator/CatLife_TownWalker.controller";
        private const string CatName = "CatCompanionModel";

        private static readonly string[] StateNames =
        {
            "CL_CAT_IdleBreath_v06_headsync_loop_108f",
            "CL_CAT_SRC_Walk_60fps",
            "CL_CAT_AlertLook_v01_loop_120f",
            "CL_CAT_CuriousSniff_v02_loop_112f",
            "CL_CAT_EarTwitchAlert_v02_loop_120f",
            "CL_CAT_HeadShakeNo_v01_loop_108f",
            "CL_CAT_HeadTiltListen_v01_loop_96f",
            "CL_CAT_LookBack_v02_loop_112f",
            "CL_CAT_PawWave_v01_loop_96f",
            "CL_CAT_StretchYawn_v03_slow_loop_264f",
            "CL_CAT_TailWagHappy_v01_loop_96f",
        };

        private static readonly string[] ClipPaths =
        {
            "Assets/Art/Cat/Animations/Clips/CL_CAT_IdleBreath_v06_headsync_loop_108f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_SRC_Walk_60fps.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_AlertLook_v01_loop_120f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_CuriousSniff_v02_loop_112f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_EarTwitchAlert_v02_loop_120f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_HeadShakeNo_v01_loop_108f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_HeadTiltListen_v01_loop_96f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_LookBack_v02_loop_112f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_PawWave_v01_loop_96f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_StretchYawn_v03_slow_loop_264f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_TailWagHappy_v01_loop_96f.anim",
        };

        [MenuItem(MenuPath)]
        public static void SetupRuntimeAnimatorStates()
        {
            AnimatorController controller = EnsureController();
            RebuildControllerStates(controller);
            AssignControllerToSceneCat(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CatLife runtime animator states installed: " + StateNames.Length + " states are available for CatAnimationController crossfade.");
        }

        public static AnimatorController EnsureController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            EnsureParameter(controller, "MoveSpeed01", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "InFocusMode", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Arousal01", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsWalking", AnimatorControllerParameterType.Bool);
            return controller;
        }

        public static void RebuildControllerStates(AnimatorController controller)
        {
            if (controller == null)
            {
                Debug.LogError("CatLife runtime animator setup failed: controller is null.");
                return;
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            ClearStateMachine(stateMachine);

            AnimatorState defaultState = null;
            for (int i = 0; i < StateNames.Length; i++)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPaths[i]);
                if (clip == null)
                {
                    Debug.LogWarning("CatLife runtime animator setup skipped missing clip: " + ClipPaths[i]);
                    continue;
                }

                AnimatorState state = stateMachine.AddState(StateNames[i], new Vector3(260f + (i % 3) * 260f, 40f + (i / 3) * 90f, 0f));
                state.motion = clip;
                state.speed = 1f;
                state.writeDefaultValues = true;

                if (StateNames[i] == "CL_CAT_IdleBreath_v06_headsync_loop_108f")
                {
                    defaultState = state;
                }
            }

            if (defaultState == null && stateMachine.states.Length > 0)
            {
                defaultState = stateMachine.states[0].state;
            }

            stateMachine.defaultState = defaultState;
            EditorUtility.SetDirty(controller);
        }

        private static void AssignControllerToSceneCat(RuntimeAnimatorController controller)
        {
            GameObject cat = GameObject.Find(CatName);
            if (cat == null)
            {
                return;
            }

            Animator animator = cat.GetComponent<Animator>();
            if (animator == null)
            {
                animator = cat.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(animator);
        }

        private static void EnsureParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType parameterType)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == parameterName && parameters[i].type == parameterType)
                {
                    return;
                }
            }

            controller.AddParameter(parameterName, parameterType);
        }

        private static void ClearStateMachine(AnimatorStateMachine stateMachine)
        {
            ChildAnimatorState[] states = stateMachine.states;
            for (int i = states.Length - 1; i >= 0; i--)
            {
                stateMachine.RemoveState(states[i].state);
            }

            AnimatorStateTransition[] anyStateTransitions = stateMachine.anyStateTransitions;
            for (int i = anyStateTransitions.Length - 1; i >= 0; i--)
            {
                stateMachine.RemoveAnyStateTransition(anyStateTransitions[i]);
            }

            AnimatorTransition[] entryTransitions = stateMachine.entryTransitions;
            for (int i = entryTransitions.Length - 1; i >= 0; i--)
            {
                stateMachine.RemoveEntryTransition(entryTransitions[i]);
            }
        }
    }
}

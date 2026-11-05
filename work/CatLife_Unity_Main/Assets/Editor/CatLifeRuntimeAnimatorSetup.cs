using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CatLife.EditorTools
{
    public static class CatLifeRuntimeAnimatorSetup
    {
        private const string MenuPath = "CatLife/Runtime/Setup Runtime Animator States";
        private const string ControllerPath = "Assets/MobileRuntime/Art/Cat/CL_CAT_Mobile.controller";
        public const string ClipFolder = "Assets/MobileRuntime/Art/Cat/AnimationsV12/";
        private const string CatName = "CatCompanionModel";

        private static readonly string[] StateNames =
        {
            "CL_CAT_StandIdle_v01_loop_96f",
            "CL_CAT_Walk_v01_loop_60f",
            "CL_CAT_CrouchIdleBreath_v01_loop_108f",
            "CL_CAT_SitAlertLook_v01_loop_120f",
            "CL_CAT_SitCuriousSniff_v01_loop_112f",
            "CL_CAT_SitEarTwitchAlert_v01_loop_120f",
            "CL_CAT_CrouchHeadShakeNo_v01_loop_108f",
            "CL_CAT_CrouchHeadTiltListen_v01_loop_96f",
            "CL_CAT_CrouchLookBack_v01_loop_112f",
            "CL_CAT_CrouchPawWave_v01_loop_96f",
            "CL_CAT_StandStretchYawn_v01_144f",
            "CL_CAT_SitTailGreeting_v01_loop_96f",
            "CL_CAT_RewardSideTailReturn_v01_384f",
            "CL_CAT_StandToSit_v01_72f",
            "CL_CAT_SitIdle_v01_loop_96f",
            "CL_CAT_SitToLie_v01_120f",
            "CL_CAT_LieFocusRest_v01_loop_96f",
            "CL_CAT_LieFocusAttention_v01_48f",
            "CL_CAT_LieToSit_v01_72f",
            "CL_CAT_StandToLie_v11_150f", "CL_CAT_LieToStand_v11_120f",
            "CL_CAT_ProneBreath_v11_loop_240f", "CL_CAT_ProneAttention_v11_48f",
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
                string clipName = StateNames[i];
                if (clipName == "CL_CAT_LieFocusRest_v01_loop_96f") clipName = "CL_CAT_ProneBreath_v11_loop_240f";
                if (clipName == "CL_CAT_LieFocusAttention_v01_48f") clipName = "CL_CAT_ProneAttention_v11_48f";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipFolder + clipName + ".anim");
                if (clip == null)
                {
                    throw new InvalidOperationException("Missing v12 runtime clip: " + clipName);
                }

                AnimatorState state = stateMachine.AddState(StateNames[i], new Vector3(260f + (i % 3) * 260f, 40f + (i / 3) * 90f, 0f));
                state.motion = clip;
                state.speed = 1f;
                state.writeDefaultValues = false;

                if (StateNames[i] == "CL_CAT_StandIdle_v01_loop_96f")
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

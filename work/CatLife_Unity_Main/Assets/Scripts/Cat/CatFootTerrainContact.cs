using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatLife.Cat
{
    // v12: support correction preserves authored clearance for a swinging paw.
    public sealed class CatFootTerrainContact : MonoBehaviour
    {
        [Serializable] public sealed class SoleProxy { public Vector3[] points; }
        [SerializeField] SoleProxy[] soleProxies;
        sealed class Leg
        {
            public readonly Transform[] bones = new Transform[4];
            public readonly Vector3[] positions = new Vector3[4];
            public readonly Quaternion[] rotations = new Quaternion[4];
            public readonly float[] lengths = new float[3];
            public Vector3 goal;
            public bool correct;
        }
        readonly Leg[] legs = new Leg[4];
        Animator animator;
        SkinnedMeshRenderer skin;
        Transform[] roots;
        public float BodyOffset { get; private set; }
        public float MaxLengthError { get; private set; }
        public int CorrectedFeet { get; private set; }

        void Awake()
        {
            animator = GetComponent<Animator>();
            skin = GetComponentInChildren<SkinnedMeshRenderer>();
            string[] names = { "frontleg", "R_frontleg", "backleg", "R_backleg" };
            var rootList = new List<Transform>();
            foreach (var bone in skin.bones)
                if (Array.IndexOf(skin.bones, bone.parent) < 0) rootList.Add(bone);
            roots = rootList.ToArray();
            for (int f = 0; f < 4; f++)
            {
                legs[f] = new Leg();
                string[] suffix = { "", "0", "1", "2" };
                for (int i = 0; i < 4; i++) legs[f].bones[i] = Array.Find(skin.bones, b => b.name == names[f] + suffix[i]);
            }
        }

        static bool IsSupportedIdle(AnimatorStateInfo state) =>
            state.IsName("CL_CAT_StandIdle_v01_loop_96f") ||
            state.IsName("CL_CAT_ProneBreath_v11_loop_240f") ||
            state.IsName("CL_CAT_ProneAttention_v11_48f") ||
            state.IsName("CL_CAT_SitTailGreeting_v01_loop_96f");

        static bool IsExitGesture(AnimatorStateInfo state) =>
            state.IsName("CL_CAT_ProneExitReach_v01_24f") ||
            state.IsName("CL_CAT_ProneExitHold_v01_loop_48f") ||
            state.IsName("CL_CAT_ProneExitRelease_v01_24f");

        static bool IsStepping(AnimatorStateInfo state) =>
            state.IsName("CL_CAT_TurnLocalLeft") ||
            state.IsName("CL_CAT_TurnLocalRight") ||
            state.IsName("CL_CAT_Walk_v01_loop_60f") ||
            state.IsName("CL_CAT_CrouchPawWave_v01_loop_96f") ||
            state.IsName("CL_CAT_SitSniffStand_v01_280f") ||
            state.IsName("CL_CAT_StandToLie_v11_150f") ||
            state.IsName("CL_CAT_RewardSideTailReturn_v01_384f");

        void LateUpdate()
        {
            BodyOffset = 0; CorrectedFeet = 0;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool walk = IsStepping(state);
            bool exitGesture = IsExitGesture(state);
            if (!walk && !exitGesture && !IsSupportedIdle(state)) return;
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (!IsStepping(next) && !IsExitGesture(next) && !IsSupportedIdle(next)) return;
                walk |= IsStepping(next);
                exitGesture |= IsExitGesture(next);
            }
            // Step height is measured against the supporting surface, not the
            // Rigidbody origin, which can lag that surface during a physics step.
            float rootFloor = float.NaN, rootDistance = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= rootDistance) continue;
                rootDistance = hit.distance;
                rootFloor = hit.point.y;
            }
            for (int f = 0; f < 4; f++)
            {
                var leg = legs[f];
                for (int i = 0; i < 4; i++) { leg.positions[i] = leg.bones[i].position; leg.rotations[i] = leg.bones[i].rotation; }
                for (int i = 0; i < 3; i++) leg.lengths[i] = Vector3.Distance(leg.positions[i], leg.positions[i + 1]);
                Vector3 sole = new Vector3(0, float.PositiveInfinity, 0);
                foreach (var point in soleProxies[f].points)
                {
                    var world = leg.bones[3].TransformPoint(point);
                    if (world.y < sole.y) sole = world;
                }
                var hits = Physics.RaycastAll(sole + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity, floor = 0;
                foreach (var hit in hits)
                {
                    if (hit.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                    nearest = hit.distance; floor = hit.point.y;
                }
                // The exit gesture raises only the first front paw; the other
                // three keep contact instead of inheriting its swing clearance.
                bool preserveSwing = walk || exitGesture && f == 0;
                float correction = preserveSwing ? floor - transform.position.y + .00055f : floor + .001f - sole.y;
                if (preserveSwing) correction = Mathf.Max(correction, floor + .001f - sole.y);
                leg.correct = !float.IsPositiveInfinity(nearest) && Mathf.Abs(floor - rootFloor) <= .025f;
                leg.goal = leg.positions[3] + Vector3.up * correction;
                if (!leg.correct) continue;
                Vector3 hock = leg.positions[2] + Vector3.up * correction;
                Vector3 horizontal = hock - leg.positions[0]; horizontal.y = 0;
                float reach = leg.lengths[0] + leg.lengths[1] - .00001f;
                float vertical = Mathf.Sqrt(Mathf.Max(0, reach * reach - horizontal.sqrMagnitude));
                BodyOffset = Mathf.Min(BodyOffset, hock.y + vertical - leg.positions[0].y);
            }
            // Large steps are navigation work, not a licence to crush the accepted body pose.
            if (BodyOffset < -.025f) { BodyOffset = 0; return; }
            foreach (var root in roots) root.position += Vector3.up * BodyOffset;
            for (int f = 0; f < 4; f++)
            {
                var leg = legs[f]; if (!leg.correct) continue;
                var p = leg.positions;
                Vector3 root = p[0] + Vector3.up * BodyOffset;
                Vector3 hock = leg.goal - (p[3] - p[2]).normalized * leg.lengths[2];
                Vector3 axis = hock - root; float distance = axis.magnitude;
                float a = leg.lengths[0], b = leg.lengths[1];
                if (distance >= a + b || distance <= Mathf.Abs(a - b)) continue;
                axis /= distance;
                Vector3 sourceAxis = (p[2] - p[0]).normalized;
                Vector3 pole = p[1] - p[0]; pole -= sourceAxis * Vector3.Dot(pole, sourceAxis);
                pole -= axis * Vector3.Dot(pole, axis); pole.Normalize();
                float along = (a * a - b * b + distance * distance) / (2 * distance);
                Vector3 knee = root + axis * along + pole * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
                Vector3[] solved = { root, knee, hock, leg.goal };
                for (int i = 0; i < 4; i++)
                {
                    leg.bones[i].position = solved[i];
                    leg.bones[i].rotation = i == 3 ? leg.rotations[i] : Quaternion.FromToRotation(p[i + 1] - p[i], solved[i + 1] - solved[i]) * leg.rotations[i];
                }
                for (int i = 0; i < 3; i++) MaxLengthError = Mathf.Max(MaxLengthError, Mathf.Abs(Vector3.Distance(leg.bones[i].position, leg.bones[i + 1].position) - leg.lengths[i]));
                CorrectedFeet++;
            }
        }
    }
}

using System.Collections;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CatLifeStateMachineRegressionTests
{
    private const string Walk = "CL_CAT_Walk_v01_loop_60f";
    private const string StandIdle = "CL_CAT_StandIdle_v01_loop_96f";
    private const string SitIdle = "CL_CAT_SitIdle_v01_loop_96f";
    private const string FocusRest = "CL_CAT_ProneBreath_v11_loop_240f";
    private string savedData;
    [SetUp] public void SaveData() { savedData = PlayerPrefs.GetString("CatLife.Mobile.Data.v1", ""); }
    [TearDown] public void RestoreData()
    {
        if (savedData.Length == 0) PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        else PlayerPrefs.SetString("CatLife.Mobile.Data.v1", savedData);
    }

    [UnityTest]
    public IEnumerator WalkClipLoopsWhileNavigationCanContinue()
    {
        yield return LoadMobileScene();

        AnimationClip walk = System.Array.Find(
            GameObject.Find("CatLifeMobileCat").GetComponentInChildren<Animator>().runtimeAnimatorController.animationClips,
            clip => clip.name.EndsWith(Walk));

        Assert.That(walk, Is.Not.Null);
        Assert.That(walk.isLooping, Is.True, "R1-01: Walk must not stop at its final pose while navigation continues.");
    }

    [UnityTest]
    public IEnumerator RuntimeClipsHaveApprovedLoopPoliciesAndWalkAdvancesThreeCycles()
    {
        yield return LoadMobileScene();

        GameObject cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        Animator animator = cat.GetComponentInChildren<Animator>();
        string[] oneShots = { "CL_CAT_LieFocusAttention_v01_48f", "CL_CAT_SitToLie_v01_120f", "CL_CAT_StandToSit_v01_72f", "CL_CAT_LieToSit_v01_72f", "CL_CAT_StandStretchYawn_v01_144f", "CL_CAT_StandToLie_v11_150f", "CL_CAT_LieToStand_v11_120f", "CL_CAT_ProneAttention_v11_48f" };
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        Assert.That(clips.Length, Is.EqualTo(22));
        foreach (AnimationClip clip in clips)
        {
            bool oneShot = clip.name.EndsWith("CL_CAT_CrouchPawWave_v01_loop_96f") || System.Array.Exists(oneShots, name => clip.name.EndsWith(name));
            Assert.That(clip.isLooping, Is.EqualTo(!oneShot), clip.name);
        }

        AnimationClip walk = System.Array.Find(clips, clip => clip.name.EndsWith(Walk));
        animator.Play("Base Layer." + Walk, 0, 0f);
        yield return new WaitForSeconds(walk.length * 3.1f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(Walk), Is.True);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(3f));
    }

    [UnityTest]
    public IEnumerator ReturnHomeDelayedSitCannotOverwriteWalk()
    {
        yield return LoadMobileScene();

        GameObject cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        Component presenter = cat.GetComponent("CatLifeMobileCatPresenter");
        Component animation = cat.GetComponent("CatAnimationController");

        presenter.GetType().GetMethod("ReturnHome").Invoke(presenter, null);
        animation.GetType().GetMethod("ForceLocomotion").Invoke(animation, new object[] { true });
        yield return new WaitForSeconds(1.3f);

        string command = (string)presenter.GetType().GetProperty("LastAnimationCommand").GetValue(presenter);
        Assert.That(command, Is.EqualTo(StandIdle), "R1-02: ReturnHome must hand locomotion back to standing, never to sitting.");
    }

    [UnityTest]
    public IEnumerator LocomotionRestoresWalkAfterPresenterWritesAnotherState()
    {
        yield return LoadMobileScene();

        GameObject cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        Component animation = cat.GetComponent("CatAnimationController");
        Component presenter = cat.GetComponent("CatLifeMobileCatPresenter");

        animation.GetType().GetMethod("ForceLocomotion").Invoke(animation, new object[] { true });
        yield return new WaitForSeconds(.2f);
        presenter.GetType().GetMethod("ShowPhase", new[] { typeof(CatLifeSessionPhase) }).Invoke(presenter, new object[] { CatLifeSessionPhase.Focus });
        yield return new WaitForSeconds(.2f);
        animation.GetType().GetMethod("ForceLocomotion").Invoke(animation, new object[] { true });
        yield return new WaitForSeconds(.2f);

        Assert.That(CurrentState(cat), Is.EqualTo(Walk), "R1-03: locomotion cache must synchronize after Presenter takes the Animator.");
    }

    [UnityTest]
    public IEnumerator WalkArrivalReturnsToStandingIdleInsteadOfSitting()
    {
        yield return LoadMobileScene();

        GameObject cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        Component animation = cat.GetComponent("CatAnimationController");
        animation.GetType().GetMethod("ForceLocomotion").Invoke(animation, new object[] { true });
        yield return new WaitForSeconds(.2f);
        animation.GetType().GetMethod("ForceLocomotion").Invoke(animation, new object[] { false });
        yield return new WaitForSeconds(.25f);

        Assert.That(CurrentState(cat), Is.EqualTo(StandIdle), "Walk must hand off to the standing idle clip after arrival.");
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        Assert.That(skin.sharedMesh.blendShapeCount, Is.EqualTo(39), "Accepted v12 shared shape library must remain installed.");
        for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
            Assert.That(skin.GetBlendShapeWeight(i), Is.LessThan(1f), skin.sharedMesh.GetBlendShapeName(i) + " leaked into standing idle.");
    }

    [UnityTest]
    public IEnumerator ExpiredArrivalActionCannotInterruptAnActivePath()
    {
        GameObject host = new GameObject("R1ActionRouter");
        System.Type routerType = System.Type.GetType("CatLife.Cat.CatActionRouter, Assembly-CSharp");
        System.Type requestType = System.Type.GetType("CatLife.Cat.CatActionRequest, Assembly-CSharp");
        System.Type stateType = System.Type.GetType("CatLife.Cat.CatBehaviorState, Assembly-CSharp");
        System.Type sourceType = System.Type.GetType("CatLife.Cat.CatActionSource, Assembly-CSharp");
        System.Type policyType = System.Type.GetType("CatLife.Cat.CatActionInterruptPolicy, Assembly-CSharp");
        Component router = host.AddComponent(routerType);
        object request = requestType.GetMethod("Create").Invoke(null, new object[]
        {
            System.Enum.Parse(stateType, "CuriousSniff"), System.Enum.Parse(sourceType, "User"), "r1_expired_arrival", 70, 0f, .01f,
            System.Enum.Parse(policyType, "QueueIfMoving"), false
        });
        object[] routeArgs = { request, true, false, null };
        Assert.That((bool)router.GetType().GetMethod("TryRoute").Invoke(router, routeArgs), Is.False);

        yield return new WaitForSeconds(.05f);

        object[] popArgs = { true, false, null };
        Assert.That((bool)router.GetType().GetMethod("TryPopReady").Invoke(router, popArgs), Is.False, "R1-05: an expired QueueIfMoving action must be dropped while the path is active.");
        Assert.That((int)router.GetType().GetProperty("QueuedActionCount").GetValue(router), Is.Zero);
        Object.Destroy(host);
    }

    [UnityTest]
    public IEnumerator ReturnHomeHoldsAnimatorUntilSitTransitionCompletes()
    {
        yield return LoadMobileScene();

        GameObject systems = GameObject.Find("CatLifeRuntimeSystems");
        Component coordinator = systems.GetComponent("CatLifeMobileRuntimeCoordinator");
        Behaviour driver = (Behaviour)GameObject.Find("CatLifeMobileCat").GetComponent("CatBehaviorDriver");
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Reward });
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Normal });
        coordinator.GetType().GetMethod("ReturnCatHome").Invoke(coordinator, null);
        Assert.That(driver.enabled, Is.False);
        yield return new WaitForSeconds(1.3f);
        Assert.That(driver.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator FocusAnchorSurvivesNavigationLateUpdate()
    {
        yield return LoadMobileScene();

        GameObject cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        cat.GetComponent("CatNavigationAgent").GetType().GetMethod("StopSoft").Invoke(cat.GetComponent("CatNavigationAgent"), null);
        yield return new WaitForSeconds(.5f);
        float anchorY = cat.GetComponent<Rigidbody>().position.y;
        Component coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Focus });
        yield return null;

        Assert.That(cat.GetComponent<Rigidbody>().position.y, Is.EqualTo(anchorY).Within(.03f), "R1-06: Focus must retain the gravity-grounded session anchor.");
    }

    [UnityTest]
    public IEnumerator FocusNudgeReturnsToFocusRest()
    {
        yield return LoadMobileScene();

        GameObject cat = GameObject.Find("CatLifeMobileCat");
        Component coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Focus });
        yield return new WaitForSeconds(.2f);
        coordinator.GetType().GetMethod("NudgeCat").Invoke(coordinator, null);
        yield return new WaitForSeconds(1.1f);

        Assert.That(CurrentState(cat), Is.EqualTo(FocusRest), "R1-07: FocusAttention must return to FocusRest after its one-shot clip.");
    }

    [UnityTest]
    public IEnumerator SameFocusPhaseDoesNotRestartNudge()
    {
        yield return LoadMobileScene();

        GameObject cat = GameObject.Find("CatLifeMobileCat");
        Component coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Focus });
        coordinator.GetType().GetMethod("NudgeCat").Invoke(coordinator, null);
        yield return new WaitForSeconds(.2f);
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Focus });
        yield return new WaitForSeconds(.2f);

        Assert.That(CurrentState(cat), Is.EqualTo("CL_CAT_ProneAttention_v11_48f"));
    }

    [UnityTest]
    public IEnumerator NewPathIsActiveBeforeVelocityRisesAndStopsExplicitly()
    {
        yield return LoadMobileScene();

        GameObject cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        Component navigation = cat.GetComponent("CatNavigationAgent");
        NavMeshHit hit;
        Assert.That(NavMesh.SamplePosition(cat.transform.position + Vector3.right, out hit, 2f, NavMesh.AllAreas), Is.True);
        Assert.That((bool)navigation.GetType().GetMethod("TryMoveTo").Invoke(navigation, new object[] { hit.position }), Is.True);
        Assert.That((bool)navigation.GetType().GetProperty("IsPathInProgress").GetValue(navigation), Is.True);
        navigation.GetType().GetMethod("StopSoft").Invoke(navigation, null);
        Assert.That((bool)navigation.GetType().GetProperty("IsPathInProgress").GetValue(navigation), Is.False);
    }

    [UnityTest]
    public IEnumerator TelemetryCapturesPhaseOwnerAnimatorAndPositionForTrajectoryReview()
    {
        yield return LoadMobileScene();

        GameObject cat = GameObject.Find("CatLifeMobileCat");
        Component telemetry = cat.GetComponent("CatBehaviorTelemetry");
        if (telemetry == null)
            telemetry = cat.AddComponent(System.Type.GetType("CatLife.Cat.CatBehaviorTelemetry, Assembly-CSharp"));
        string report = (string)telemetry.GetType().GetMethod("BuildDebugReport").Invoke(telemetry, null);

        Assert.That(report, Does.Contain("mobile_phase=").And.Contain("animator_owner=").And.Contain("position=").And.Contain("anim_current=").And.Contain("anim_next=").And.Contain("presenter_command="));
    }

    [UnityTest]
    public IEnumerator BlockedPhysicsBodyCannotMasqueradeAsNavigationProgress()
    {
        yield return LoadMobileScene();
        var cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        var navigation = cat.GetComponent("CatNavigationAgent");
        var guard = cat.GetComponent("CatNavMeshSafetyGuard");
        var body = cat.GetComponent<Rigidbody>();
        yield return new WaitForSeconds(.5f);
        body.constraints = RigidbodyConstraints.FreezeAll;
        Assert.That((bool)navigation.GetType().GetMethod("TryMoveTo").Invoke(navigation,new object[]{body.position+Vector3.right}), Is.True);
        yield return new WaitForSeconds(3.5f);
        Assert.That((int)guard.GetType().GetProperty("StuckRecoveryCount").GetValue(guard), Is.GreaterThan(0), "Agent's calculated velocity must not hide a physically blocked body.");
        Assert.That((bool)navigation.GetType().GetProperty("IsPathInProgress").GetValue(navigation), Is.False);
    }

    [UnityTest]
    public IEnumerator AmbientRoamingCannotKeepWalkingWithoutPhysicalProgress()
    {
        yield return LoadMobileScene();
        var cat = GameObject.Find("CatLifeMobileCat");
        var navigation = cat.GetComponent("CatNavigationAgent");
        var body = cat.GetComponent<Rigidbody>();
        var agent = cat.GetComponent<NavMeshAgent>();
        var rows = new System.Text.StringBuilder("seconds,body,nav,desired,velocity,remaining,state\n");
        float stationaryWalking = 0;
        for (int i = 0; i < 70; i++)
        {
            var before = body.position;
            yield return new WaitForSeconds(1);
            var delta = body.position - before; delta.y = 0;
            bool walking = CurrentState(cat) == Walk;
            stationaryWalking = walking && delta.magnitude < .02f ? stationaryWalking + 1 : 0;
            rows.AppendLine($"{i+1},\"{body.position:F3}\",\"{agent.nextPosition:F3}\",\"{agent.desiredVelocity:F3}\",\"{body.linearVelocity:F3}\",{agent.remainingDistance:F3},{CurrentState(cat)}");
            if (stationaryWalking >= 5)
            {
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../../Reports/interview-demo-20261008/ambient-roaming.csv"));
                System.IO.File.WriteAllText(path, rows.ToString());
                Assert.Fail("Walk remained active for five seconds with less than 2cm/s physical progress; see ambient-roaming.csv");
            }
        }
        System.IO.File.WriteAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../../Reports/interview-demo-20261008/ambient-roaming.csv")), rows.ToString());
    }

    private static IEnumerator LoadMobileScene()
    {
        PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
    }

    [UnityTest] public IEnumerator AiPawWaveMovesPawAndReturnsToStandingAfterFullClip()
    {
        yield return LoadMobileScene();
        var cat = GameObject.Find("CatLifeMobileCat");
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Reward });
        yield return new WaitForSeconds(2.3f);
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var paw = System.Array.Find(skin.bones, bone => bone.name == "frontleg2");
        Vector3 initial = cat.transform.InverseTransformPoint(paw.position);
        Vector3 worldInitial = paw.position;
        float worldMovement = 0;
        var samples = new System.Text.StringBuilder("time,state,paw_world,displacement_m\n");
        Assert.That((bool)coordinator.GetType().GetMethod("PlayAiReaction").Invoke(coordinator, new object[] { "paw_wave" }), Is.True);
        float movement = 0f;
        float until = Time.time + 2.8f;
        while (Time.time < until)
        {
            movement = Mathf.Max(movement, Vector3.Distance(initial, cat.transform.InverseTransformPoint(paw.position)));
            worldMovement = Mathf.Max(worldMovement, Vector3.Distance(worldInitial, paw.position));
            samples.AppendLine($"{Time.time:F3},{CurrentState(cat)},\"{paw.position:F4}\",{Vector3.Distance(worldInitial,paw.position):F5}");
            yield return null;
        }
        Assert.That(movement, Is.GreaterThan(.01f), "A dispatched state must produce a visible paw displacement, not just a played log.");
        System.IO.File.WriteAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../../Reports/interview-demo-20261008/ai-paw-runtime.csv")),samples.ToString());
        Assert.That(worldMovement, Is.GreaterThan(.10f), "Wave must move its paw by at least 10cm in metre-space, not scaled bone-space.");
        Assert.That(CurrentState(cat), Is.EqualTo(StandIdle));
    }

    [UnityTest] public IEnumerator AiRestUsesAcceptedLieAndRiseTransitions()
    {
        yield return LoadMobileScene();
        var cat = GameObject.Find("CatLifeMobileCat");
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Reward });
        yield return new WaitForSeconds(2.3f);
        Assert.That((bool)coordinator.GetType().GetMethod("PlayAiReaction").Invoke(coordinator, new object[] { "focus_rest" }), Is.True);
        yield return new WaitForSeconds(.4f);
        Assert.That(cat.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandToLie_v11_150f"), Is.True);
        Assert.That((bool)coordinator.GetType().GetProperty("IsAiReactionPlaying").GetValue(coordinator), Is.True);
        yield return new WaitForSeconds(2.5f);
        Assert.That(CurrentState(cat), Is.EqualTo(FocusRest));
        yield return new WaitForSeconds(4.1f);
        Assert.That(cat.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_LieToStand_v11_120f"), Is.True);
        yield return new WaitForSeconds(2.2f);
        Assert.That(CurrentState(cat), Is.EqualTo(StandIdle));
    }

    private static string CurrentState(GameObject cat)
    {
        Animator animator = cat.GetComponentInChildren<Animator>();
        foreach (string state in new[] { Walk, StandIdle, SitIdle, FocusRest, "CL_CAT_ProneAttention_v11_48f", "CL_CAT_StandToSit_v01_72f" })
            if (animator.GetCurrentAnimatorStateInfo(0).IsName(state) || animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + state)) return state;
        return animator.GetCurrentAnimatorStateInfo(0).shortNameHash.ToString();
    }
}

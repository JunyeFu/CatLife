using System.Collections;
using System.IO;
using System.Reflection;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CatLifeProneV11Tests
{
    [UnityTest] public IEnumerator InstalledV12OwnsAllShapesAndUsesFullWeights()
    {
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        var cat=GameObject.Find("CatLifeMobileCat");
        var animator=cat.GetComponent<Animator>();
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        Assert.That(cat.GetComponent("CatCrouchShapeDriver"),Is.Null,"Old LateUpdate must not overwrite v12 clip shapes.");
        Assert.That(QualitySettings.skinWeights,Is.EqualTo(SkinWeights.Unlimited),"Use saved project quality, not a test-only override.");
        Assert.That(skin.quality,Is.EqualTo(SkinQuality.Auto));
        Assert.That(skin.sharedMesh.name,Is.EqualTo("MSH_CL_CAT_Runtime_v12"));
        Assert.That(skin.sharedMesh.blendShapeCount,Is.EqualTo(39));
        using(var counts=skin.sharedMesh.GetBonesPerVertex())
        {
            int maximum=0;foreach(var count in counts) maximum=System.Math.Max(maximum,count);
            Assert.That(maximum,Is.EqualTo(9));
        }
        var coordinator=GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator,new object[]{CatLifeSessionPhase.Focus});
        yield return new WaitForSeconds(.3f);
        foreach(var clip in animator.runtimeAnimatorController.animationClips)
        {
            animator.CrossFadeInFixedTime(clip.name,.15f,0);
            yield return new WaitForSeconds(.3f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(clip.name),Is.True,clip.name);
            yield return new WaitForSeconds(clip.length);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(clip.name),Is.True,"Unexpected owner replaced "+clip.name);
            animator.CrossFadeInFixedTime("CL_CAT_StandIdle_v01_loop_96f",.15f,0);
            yield return new WaitForSeconds(.3f);
            for(int i=0;i<39;i++)Assert.That(skin.GetBlendShapeWeight(i),Is.EqualTo(0).Within(.001f),clip.name+" left "+skin.sharedMesh.GetBlendShapeName(i));
            Debug.Log("V12_FORMAL_STATE "+clip.name+" completed and returned neutral");
        }
    }
    const string Key="CatLife.Mobile.Data.v1";
    [UnityTest] public IEnumerator InstalledV12OneShotsFinishBeforeAmbientDecision()
    {
        SceneManager.LoadScene("CatLifeMobile"); yield return new WaitForSeconds(.5f);
        var cat=GameObject.Find("CatLifeMobileCat");
        var driver=cat.GetComponent("CatBehaviorDriver");
        var animator=cat.GetComponent<Animator>();
        var route=driver.GetType().GetMethod("PlayRoutedAction",BindingFlags.NonPublic|BindingFlags.Instance);
        var requestType=route.GetParameters()[0].ParameterType;
        foreach(string action in new[]{"StretchYawn","PawWave"})
        {
            object request=System.Activator.CreateInstance(requestType);
            var stateField=requestType.GetField("state");
            stateField.SetValue(request,System.Enum.Parse(stateField.FieldType,action));
            route.Invoke(driver,new[]{request});
            float started=Time.time;
            string expected=action=="StretchYawn"?"CL_CAT_StandStretchYawn_v01_144f":"CL_CAT_CrouchPawWave_v01_loop_96f";
            yield return new WaitForSeconds(.2f);
            bool completed=false;
            while(Time.time-started<6f)
            {
                var state=animator.GetCurrentAnimatorStateInfo(0);
                Assert.That(state.IsName(expected),Is.True,"Ambient logic interrupted "+action);
                if(state.normalizedTime>=.995f){completed=true;break;}
                yield return null;
            }
            Assert.That(completed,Is.True,action+" did not reach its last frames.");
            Debug.Log("V12_ONESHOT_COMPLETED "+action+" elapsed="+(Time.time-started));
        }
    }
    bool hadData; string savedData;
    [SetUp] public void Setup()
    {
        hadData=PlayerPrefs.HasKey(Key); savedData=PlayerPrefs.GetString(Key);
        PlayerPrefs.DeleteKey(Key);
    }
    [UnityTearDown] public IEnumerator Restore()
    {
        var scene=SceneManager.GetSceneByName("CatLifeMobile");
        if(scene.isLoaded)
        {
            var empty=SceneManager.CreateScene("ProneTestCleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        if(hadData) PlayerPrefs.SetString(Key,savedData); else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
    [UnityTest] public IEnumerator InstalledSceneCompletesProneFlowWithoutShapeLeak()
    {
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        var app=GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var cat=GameObject.Find("CatLifeMobileCat");
        var animator=cat.GetComponentInChildren<Animator>();
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        Assert.That(skin.sharedMaterial.GetTexture("_BaseMap"),Is.Not.Null);
        int corrective=skin.sharedMesh.GetBlendShapeIndex("CL_CAT_ProneJointCorrective_v11");
        Assert.That(corrective,Is.GreaterThanOrEqualTo(0));
        Assert.That(skin.GetBlendShapeWeight(corrective),Is.EqualTo(0).Within(.01));
        app.GetType().GetMethod("BeginTransition",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,null);
        Quaternion sessionRotation=cat.transform.rotation;
        float end=(float)app.GetType().GetField("transitionEndsAt",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);
        Assert.That(end-Time.unscaledTime,Is.GreaterThanOrEqualTo(2.5f));
        float deadline=Time.realtimeSinceStartup+12f;
        while((CatLifeSessionPhase)app.GetType().GetProperty("CurrentPhase").GetValue(app)==CatLifeSessionPhase.Transition && Time.realtimeSinceStartup<deadline)
        {
            yield return null;
            AssertSessionRotation(cat,sessionRotation);
        }
        yield return new WaitForSeconds(.3f);
        Assert.That(app.GetType().GetProperty("CurrentPhase").GetValue(app),Is.EqualTo(CatLifeSessionPhase.Focus));
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_ProneBreath_v11_loop_240f"),Is.True);
        Assert.That(skin.GetBlendShapeWeight(corrective),Is.GreaterThan(99));
        Capture("focus");
        var presenter=cat.GetComponent("CatLifeMobileCatPresenter");
        presenter.GetType().GetMethod("Nudge").Invoke(presenter,null);
        yield return new WaitForSecondsRealtime(.2f);
        AssertSessionRotation(cat,sessionRotation);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_ProneAttention_v11_48f"),Is.True);
        yield return new WaitForSecondsRealtime(1f);
        AssertSessionRotation(cat,sessionRotation);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_ProneBreath_v11_loop_240f"),Is.True);
        var coordinator=GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator,new object[]{CatLifeSessionPhase.Reward});
        float riseEnd=Time.realtimeSinceStartup+2.3f;
        while(Time.realtimeSinceStartup<riseEnd)
        {
            yield return null;
            AssertSessionRotation(cat,sessionRotation);
        }
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_LieToStand_v11_120f"),Is.True);
        presenter.GetType().GetMethod("ReturnHome").Invoke(presenter,null);
        yield return new WaitForSecondsRealtime(.5f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"),Is.True);
        Assert.That(skin.GetBlendShapeWeight(corrective),Is.EqualTo(0).Within(.01));
        Capture("standing");
        var locomotion=cat.GetComponent("CatAnimationController");
        var force=locomotion.GetType().GetMethod("ForceLocomotion");
        force.Invoke(locomotion,new object[]{true});
        yield return new WaitForSeconds(.3f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_Walk_v01_loop_60f"),Is.True);
        Assert.That(skin.GetBlendShapeWeight(corrective),Is.EqualTo(0).Within(.01));
        Capture("walking");
        yield return new WaitForSeconds(3.2f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_Walk_v01_loop_60f"),Is.True);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,Is.GreaterThan(3f));
        force.Invoke(locomotion,new object[]{false});
        yield return new WaitForSeconds(.3f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"),Is.True);
        Assert.That(skin.GetBlendShapeWeight(corrective),Is.EqualTo(0).Within(.01));
    }
    [UnityTest] public IEnumerator RewardWaitsForRiseWhileCompletionIsAlreadySaved()
    {
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        var app=GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var data=(CatLifeAppData)app.GetType().GetField("data",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);
        var session=(CatLifeSessionController)app.GetType().GetField("session",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);
        session.BeginTransition(60,100); session.EnterFocus(100);
        Assert.That(session.TryComplete(160,out var record),Is.True);
        app.GetType().GetMethod("Complete",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,new object[]{record});
        var views=(System.Collections.Generic.Dictionary<string,GameObject>)app.GetType().GetField("views",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);
        Assert.That(views["RewardCard"].activeSelf,Is.False);
        Assert.That(CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key)).records.Count,Is.EqualTo(1));
        Assert.That(data.records[0].completed,Is.True);
        // Exercise both callback presentation outcomes without making a cloud request.
        foreach(var status in new[]{"succeeded","failed"})
        {
            record.aiRequestAttempted=true;
            record.aiRequestStatus=status;
            app.GetType().GetMethod("RenderReward",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,new object[]{record});
            Assert.That(views["RewardCard"].activeSelf,Is.False,"Early AI "+status+" must not hide the rise.");
        }
        var presenter=GameObject.Find("CatLifeMobileCat").GetComponent("CatLifeMobileCatPresenter");
        float revealStarted=Time.realtimeSinceStartup;
        float deadline=Time.realtimeSinceStartup+8f;
        while(!views["RewardCard"].activeSelf && Time.realtimeSinceStartup<deadline)
        {
            if(!(bool)presenter.GetType().GetProperty("WakeUpAnimationComplete").GetValue(presenter))
                Assert.That(views["RewardCard"].activeSelf,Is.False);
            yield return null;
        }
        Assert.That(views["RewardCard"].activeSelf,Is.True);
        Assert.That(Time.realtimeSinceStartup-revealStarted,Is.GreaterThanOrEqualTo(1.9f),"The full rise must be visible before the card opens.");
        Assert.That((bool)presenter.GetType().GetProperty("WakeUpAnimationComplete").GetValue(presenter),Is.True);
        Assert.That(data.records.Count,Is.EqualTo(1));
    }
    [UnityTest] public IEnumerator LeavingRewardDuringRiseDoesNotRevealCardLater()
    {
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        var app=GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var session=(CatLifeSessionController)app.GetType().GetField("session",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);
        session.BeginTransition(60,100); session.EnterFocus(100);
        session.TryComplete(160,out var record);
        app.GetType().GetMethod("Complete",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,new object[]{record});
        record.aiRequestAttempted=true;
        record.aiActionStatus="selected";
        var aiReaction=(IEnumerator)app.GetType().GetMethod("PlayAiRewardReaction",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,new object[]{record});
        ((MonoBehaviour)app).StartCoroutine(aiReaction);
        app.GetType().GetMethod("ReturnToTown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,null);
        yield return new WaitForSecondsRealtime(3f);
        var views=(System.Collections.Generic.Dictionary<string,GameObject>)app.GetType().GetField("views",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);
        Assert.That(app.GetType().GetProperty("CurrentView").GetValue(app),Is.EqualTo("HomeHudLayer"));
        Assert.That(views["RewardPanel"].activeSelf,Is.False);
        Assert.That(views["RewardCard"].activeSelf,Is.False);
        Assert.That(record.aiActionStatus,Is.EqualTo("skipped_page_left"));
    }
    static void AssertSessionRotation(GameObject cat,Quaternion expected)
    {
        Assert.That(Quaternion.Angle(cat.transform.rotation,expected),Is.LessThan(.1f),"Session phase changed the cat root heading.");
        Assert.That(Quaternion.Angle(cat.GetComponent<Rigidbody>().rotation,expected),Is.LessThan(.1f),"Session phase changed the physics heading.");
    }
    [UnityTest] public IEnumerator CompletedRewardSurvivesReloadWithoutDuplicateAward()
    {
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        var app=GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var session=(CatLifeSessionController)app.GetType().GetField("session",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);
        session.BeginTransition(60,100); session.EnterFocus(100); session.TryComplete(160,out var record);
        app.GetType().GetMethod("Complete",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,new object[]{record});
        // Reload while the result card is still hidden during rise.
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        yield return new WaitForSecondsRealtime(3f);
        app=GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var restored=(CatLifeAppData)app.GetType().GetField("data",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);
        Assert.That(restored.records.Count,Is.EqualTo(1));
        Assert.That(restored.records[0].GrowthAwarded,Is.EqualTo(1));
        Assert.That(restored.records[0].PawsAwarded,Is.EqualTo(1));
        Assert.That(app.GetType().GetProperty("CurrentPhase").GetValue(app),Is.EqualTo(CatLifeSessionPhase.Normal));
        Assert.That(app.GetType().GetProperty("CurrentView").GetValue(app),Is.EqualTo("HomeHudLayer"));
    }
    [UnityTest] public IEnumerator NavigationAfterProneReturnsToStandingAtEachTurn()
    {
        SceneManager.LoadScene("CatLifeMobile"); yield return new WaitForSeconds(.5f);
        var coordinator=GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        var apply=coordinator.GetType().GetMethod("ApplyPhase");
        apply.Invoke(coordinator,new object[]{CatLifeSessionPhase.Transition});
        yield return new WaitForSeconds(2.8f);
        apply.Invoke(coordinator,new object[]{CatLifeSessionPhase.Focus});
        yield return new WaitForSeconds(.3f);
        apply.Invoke(coordinator,new object[]{CatLifeSessionPhase.Reward});
        yield return new WaitForSeconds(2.3f);
        var app=GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        app.GetType().GetMethod("ReturnToTown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,null);
        yield return new WaitForSeconds(.5f);
        var cat=GameObject.Find("CatLifeMobileCat");
        var body=cat.GetComponent<Rigidbody>(); var agent=cat.GetComponent<UnityEngine.AI.NavMeshAgent>();
        var animator=cat.GetComponentInChildren<Animator>(); var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var driver=cat.GetComponent("CatBehaviorDriver");
        foreach(var point in new[]{new Vector3(2.5f,.3f,4.5f),new Vector3(2.5f,.3f,2f),new Vector3(0,.3f,2f),new Vector3(0,.3f,4.5f)})
        {
            Assert.That(UnityEngine.AI.NavMesh.SamplePosition(point,out var hit,1f,UnityEngine.AI.NavMesh.AllAreas),Is.True);
            var plannedPath=new UnityEngine.AI.NavMeshPath();
            Assert.That(agent.CalculatePath(hit.position,plannedPath),Is.True);
            Assert.That(plannedPath.status,Is.EqualTo(UnityEngine.AI.NavMeshPathStatus.PathComplete));
            float pathLength=0;
            for(int i=1;i<plannedPath.corners.Length;i++) pathLength+=Vector3.Distance(plannedPath.corners[i-1],plannedPath.corners[i]);
            Assert.That((bool)driver.GetType().GetMethod("NotifyGroundTapped").Invoke(driver,new object[]{hit.position}),Is.True);
            Vector3 selected=agent.destination;
            Assert.That(new Vector2(selected.x-hit.position.x,selected.z-hit.position.z).magnitude,Is.LessThanOrEqualTo(.75f),"Landing moved beyond the local support search.");
            Vector3 start=body.position; float began=Time.time; bool walked=false; float nextTrace=began;
            float travelBudget=pathLength/agent.speed*2f+2f;
            Debug.Log($"PRONE_ROUTE length={pathLength} budget={travelBudget} target={hit.position}");
            while(Time.time-began<travelBudget)
            {
                yield return null;
                walked|=animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_Walk_v01_loop_60f");
                if(point.x==0 && point.z==2 && Time.time>=nextTrace)
                {
                    Debug.Log($"PRONE_TURN_TRACE t={Time.time-began:F2} body={body.position} dest={agent.destination} remaining={agent.remainingDistance} velocity={agent.velocity} desired={agent.desiredVelocity}");
                    nextTrace=Time.time+.25f;
                }
                if(Time.time-began>.2f && !agent.pathPending && agent.remainingDistance<=agent.stoppingDistance && agent.velocity.sqrMagnitude<.01f) break;
            }
            Assert.That(Vector3.Distance(start,body.position),Is.GreaterThan(1f));
            Assert.That(walked,Is.True);
            yield return new WaitForSeconds(.2f);
            Vector3 delta=body.position-selected;
            Debug.Log($"PRONE_TURN_END requested={hit.position} selected={selected} body={body.position} remaining={agent.remainingDistance} path={agent.pathStatus} velocity={body.linearVelocity} desired={agent.desiredVelocity} radius={agent.radius} colliderRadius={cat.GetComponent<CapsuleCollider>().bounds.extents.x}");
            Assert.That(new Vector2(delta.x,delta.z).magnitude,Is.LessThan(agent.stoppingDistance+.2f),"Missed target "+point);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"),Is.True,"Not standing at "+point);
            Assert.That(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("CL_CAT_ProneJointCorrective_v11")),Is.EqualTo(0).Within(.01));
            Debug.Log($"PRONE_TURN target={point} reached={body.position} walked={walked}");
        }
    }
    [UnityTest] public IEnumerator GroundRequestMovesBodyAlongRealNavigationPath()
    {
        SceneManager.LoadScene("CatLifeMobile"); yield return new WaitForSeconds(.5f);
        var cat=GameObject.Find("CatLifeMobileCat");
        var body=cat.GetComponent<Rigidbody>();
        var agent=cat.GetComponent<UnityEngine.AI.NavMeshAgent>();
        var driver=cat.GetComponent("CatBehaviorDriver");
        Vector3 start=body.position;
        var triangles=UnityEngine.AI.NavMesh.CalculateTriangulation();
        bool near=UnityEngine.AI.NavMesh.SamplePosition(start,out var nearest,50f,UnityEngine.AI.NavMesh.AllAreas);
        Debug.Log($"PRONE_NAV_DIAG start={start} enabled={agent.enabled} onMesh={agent.isOnNavMesh} vertices={triangles.vertices.Length} nearestFound={near} nearest={nearest.position}");
        Assert.That(UnityEngine.AI.NavMesh.SamplePosition(start+Vector3.right*2.5f,out var hit,1f,UnityEngine.AI.NavMesh.AllAreas),Is.True);
        bool requested=(bool)driver.GetType().GetMethod("NotifyGroundTapped").Invoke(driver,new object[]{hit.position});
        Assert.That(requested,Is.True,"Real ground request was rejected at "+hit.position);
        Vector3 selected=agent.destination;
        Assert.That(new Vector2(selected.x-hit.position.x,selected.z-hit.position.z).magnitude,Is.LessThanOrEqualTo(.75f));
        var animator=cat.GetComponentInChildren<Animator>();
        bool walked=false; float maxDisplacement=0; float maxHeightChange=0;
        var diagnosticSkin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var diagnosticMesh=new Mesh();
        var contactRows=new System.Text.StringBuilder("time,body_y,state,normalized_time,foot,sole_y,ground_y,gap\n");
        diagnosticSkin.BakeMesh(diagnosticMesh,false);
        var sampledBounds=new Bounds(BakedWorld(diagnosticSkin,diagnosticMesh.vertices[0]),Vector3.zero);
        foreach(var vertex in diagnosticMesh.vertices) sampledBounds.Encapsulate(BakedWorld(diagnosticSkin,vertex));
        Debug.Log($"PRONE_CONTACT_SPACE skinScale={diagnosticSkin.transform.lossyScale} bakedWorld={sampledBounds} rendererWorld={diagnosticSkin.bounds} collider={cat.GetComponent<CapsuleCollider>().bounds}");
        float deadline=Time.time+10f;
        while(Time.time<deadline)
        {
            yield return null;
            Vector3 delta=body.position-start;
            maxDisplacement=Mathf.Max(maxDisplacement,new Vector2(delta.x,delta.z).magnitude);
            maxHeightChange=Mathf.Max(maxHeightChange,Mathf.Abs(delta.y));
            walked|=animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_Walk_v01_loop_60f");
            SampleFootContact(cat,diagnosticSkin,diagnosticMesh,animator,contactRows);
            if(!agent.pathPending && maxDisplacement>1f && agent.remainingDistance<=agent.stoppingDistance && agent.velocity.sqrMagnitude<.01f) break;
        }
        Debug.Log($"PRONE_NAV start={start} target={hit.position} end={body.position} displacement={maxDisplacement} heightChange={maxHeightChange} path={agent.pathStatus} remaining={agent.remainingDistance} walked={walked}");
        Directory.CreateDirectory("Reports/ProneImportReview/installed");
        File.WriteAllText("Reports/ProneImportReview/installed/navigation-foot-contact.csv",contactRows.ToString());
        Object.Destroy(diagnosticMesh);
        Assert.That(maxDisplacement,Is.GreaterThan(1f),"Agent path did not move the rigidbody.");
        Assert.That(walked,Is.True);
        Vector3 remaining=body.position-selected;
        Assert.That(new Vector2(remaining.x,remaining.z).magnitude,Is.LessThan(agent.stoppingDistance+.2f));
        float stopDeadline=Time.time+3f;
        while(Time.time<stopDeadline && !animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f")) yield return null;
        Debug.Log($"PRONE_ARRIVAL body={body.position} remaining={agent.remainingDistance} stopping={agent.stoppingDistance} velocity={agent.velocity} desired={agent.desiredVelocity} clip={animator.GetCurrentAnimatorClipInfo(0)[0].clip.name}");
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"),Is.True,"Arrival must return to standing.");
        yield return new WaitForSeconds(.2f);
        Vector3 settled=body.position-selected;
        Assert.That(new Vector2(settled.x,settled.z).magnitude,Is.LessThan(agent.stoppingDistance+.2f),"Standing at a later unrelated destination is not arrival.");
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"),Is.True,"Arrival standing must be visible before another ambient action.");
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        Assert.That(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("CL_CAT_ProneJointCorrective_v11")),Is.EqualTo(0).Within(.01));
        Capture("navigation-arrival");
    }
    [UnityTest] public IEnumerator AmbientActionsExposeClipAndPhysicalHeight()
    {
        SceneManager.LoadScene("CatLifeMobile"); yield return new WaitForSeconds(.5f);
        var cat=GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled=false;
        var navigation=cat.GetComponent("CatNavigationAgent");
        navigation.GetType().GetMethod("StopSoft").Invoke(navigation,null);
        var controller=cat.GetComponent("CatAnimationController");
        var play=controller.GetType().GetMethod("PlayAction");
        var animator=cat.GetComponentInChildren<Animator>();
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked=new Mesh();
        var rows=new System.Text.StringBuilder("time,body_y,state,normalized_time,foot,sole_y,ground_y,gap\n");
        foreach(string action in new[]{"CuriousSniff","LookBack","TailWagHappy","StretchYawn","PawWave","HeadTiltListen"})
        {
            object state=System.Enum.Parse(play.GetParameters()[0].ParameterType,action);
            play.Invoke(controller,new object[]{state,4f,false});
            yield return new WaitForSeconds(.2f);
            float initialY=cat.GetComponent<Rigidbody>().position.y;
            for(int sample=0;sample<8;sample++)
            {
                SampleFootContact(cat,skin,baked,animator,rows);
                Capture("ambient-"+action+"-"+sample);
                Assert.That(cat.GetComponent<Rigidbody>().position.y,Is.EqualTo(initialY).Within(.02f),"Physical root bounced during "+action);
                yield return new WaitForSeconds(.25f);
            }
        }
        File.WriteAllText("Reports/ProneImportReview/installed/ambient-foot-contact.csv",rows.ToString());
        Object.Destroy(baked);
    }
#if UNITY_EDITOR
    [UnityTest] public IEnumerator TailCandidateUsesSettledBodyAndStandingFeet()
        => AmbientCandidateUsesStandingSupport(false);
    [UnityTest] public IEnumerator PawCandidateKeepsThreeFeetPlanted()
        => AmbientCandidateUsesStandingSupport(true);
    [UnityTest] public IEnumerator StretchCandidateReturnsToStandingWithoutResidualShapes()
        => AmbientCandidateUsesStandingSupport(false,true);
    IEnumerator AmbientCandidateUsesStandingSupport(bool paw,bool stretch=false)
    {
        SceneManager.LoadScene("CatLifeMobile"); yield return new WaitForSeconds(.5f);
        var cat=GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled=false;
        var navigation=cat.GetComponent("CatNavigationAgent");
        navigation.GetType().GetMethod("StopSoft").Invoke(navigation,null);
        var animator=cat.GetComponentInChildren<Animator>();
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var controller=cat.GetComponent("CatAnimationController");
        string assetPath="Assets/Editor/CatProneReview/CL_CAT_Ambient"+(stretch?"Stretch":paw?"Paw":"Tail")+"_v12.fbx";
        if(stretch)
        {
            var sourceSkin=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath).GetComponentInChildren<SkinnedMeshRenderer>();
            var oldBones=skin.bones;
            skin.sharedMesh=sourceSkin.sharedMesh;
            skin.bones=System.Array.ConvertAll(sourceSkin.bones,b=>System.Array.Find(oldBones,old=>old.name==b.name));
            for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)skin.SetBlendShapeWeight(i,0);
            animator.Rebind();
        }
        controller.GetType().GetMethod("ForceLocomotion").Invoke(controller,new object[]{false});
        yield return new WaitForSeconds(.3f);
        var baked=new Mesh();skin.BakeMesh(baked,false);
        Vector3[] standing=baked.vertices;
        float bodyY=cat.GetComponent<Rigidbody>().position.y;
        var weights=skin.sharedMesh.boneWeights;
        var paws=new System.Collections.Generic.List<int>();
        var moving=new System.Collections.Generic.List<int>();
        for(int i=0;i<weights.Length;i++)
        {
            if(weights[i].weight0<=.99f || !skin.bones[weights[i].boneIndex0].name.EndsWith("leg2"))continue;
            string foot=skin.bones[weights[i].boneIndex0].name;
            if(paw && foot=="frontleg2")moving.Add(i);
            else if(!stretch || foot.Contains("backleg"))paws.Add(i);
        }
        string label=stretch?"stretch":paw?"paw":"tail";
        var assets=UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
        string clipName=stretch?"CL_CAT_StandStretch_v12_288f":paw?"CL_CAT_StandPawWave_v12_144f":"CL_CAT_StandTailGreeting_v12_loop_144f";
        var candidate=System.Array.Find(assets,a=>a is AnimationClip && a.name==clipName) as AnimationClip;
        Assert.That(candidate,Is.Not.Null);
        var overrides=new AnimatorOverrideController(animator.runtimeAnimatorController);
        var pairs=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip,AnimationClip>>();
        overrides.GetOverrides(pairs);
        for(int i=0;i<pairs.Count;i++) if(pairs[i].Key.name.EndsWith(stretch?"CL_CAT_StandStretchYawn_v01_144f":paw?"CL_CAT_CrouchPawWave_v01_loop_96f":"CL_CAT_SitTailGreeting_v01_loop_96f"))pairs[i]=new System.Collections.Generic.KeyValuePair<AnimationClip,AnimationClip>(pairs[i].Key,candidate);
        overrides.ApplyOverrides(pairs);animator.runtimeAnimatorController=overrides;
        // Let the replacement controller initialize before issuing the action command.
        yield return null;
        var play=controller.GetType().GetMethod("PlayAction");
        play.Invoke(controller,new object[]{System.Enum.Parse(play.GetParameters()[0].ParameterType,stretch?"StretchYawn":paw?"PawWave":"TailWagHappy"),8f,false});
        yield return new WaitForSeconds(.25f);
        Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip,Is.EqualTo(candidate));
        var rows=new System.Text.StringBuilder("time,body_y,state,normalized_time,foot,sole_y,ground_y,gap\n");
        float maxDifference=0,maxLift=0,maxCorrective=0;
        for(int sample=0;sample<48;sample++)
        {
            Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip,Is.EqualTo(candidate));
            SampleFootContact(cat,skin,baked,animator,rows);
            var points=baked.vertices;
            foreach(int i in paws)maxDifference=Mathf.Max(maxDifference,Vector3.Distance(points[i],standing[i]));
            foreach(int i in moving)maxLift=Mathf.Max(maxLift,(skin.transform.rotation*(points[i]-standing[i])).y);
            if(stretch)maxCorrective=Mathf.Max(maxCorrective,skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("CL_CAT_StretchShoulderCorrective_v12")));
            Assert.That(cat.GetComponent<Rigidbody>().position.y,Is.EqualTo(bodyY).Within(.02f));
            if(sample%8==0)Capture(label+"-v12-runtime-"+sample);
            yield return new WaitForSeconds(.1f);
        }
        File.WriteAllText("Reports/ProneImportReview/installed/"+label+"-v12-contact.csv",rows.ToString());
        Debug.Log($"AMBIENT_RUNTIME action={label} bodyY={bodyY} maxPawDifferenceFromStand={maxDifference} maxLift={maxLift}");
        Assert.That(maxDifference,Is.LessThan(.005f),"Candidate changed standing support.");
        if(paw)Assert.That(maxLift,Is.GreaterThan(.15f),"Wave must actually raise the active paw.");
        if(stretch)
        {
            Assert.That(maxCorrective,Is.GreaterThan(50f),"Stretch corrective must actually animate.");
            controller.GetType().GetMethod("ForceLocomotion").Invoke(controller,new object[]{false});
            yield return new WaitForSeconds(.5f);
            for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)
                Assert.That(skin.GetBlendShapeWeight(i),Is.EqualTo(0f).Within(.01f),skin.sharedMesh.GetBlendShapeName(i));
            skin.BakeMesh(baked,false);
            var returned=baked.vertices;
            float returnError=0;
            for(int i=0;i<returned.Length;i++)returnError=Mathf.Max(returnError,Vector3.Distance(returned[i],standing[i]));
            Debug.Log($"STRETCH_RETURN error={returnError} correctivePeak={maxCorrective}");
            Assert.That(returnError,Is.LessThan(.005f));
            Capture("stretch-v12-return-stand");
        }
        Object.Destroy(baked);Object.Destroy(overrides);
    }
#endif
    [UnityTest] public IEnumerator CompareGroundingAcrossInstalledMotionChain()
    {
        SceneManager.LoadScene("CatLifeMobile"); yield return new WaitForSeconds(.6f);
        var cat=GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled=false;
        var navigation=cat.GetComponent("CatNavigationAgent");
        navigation.GetType().GetMethod("StopSoft").Invoke(navigation,null);
        ((Behaviour)cat.GetComponent("CatAnimationController")).enabled=false;
        var animator=cat.GetComponent<Animator>();
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var capsule=cat.GetComponent<CapsuleCollider>();
        var baked=new Mesh();
        var rows=new System.Text.StringBuilder("time,body_y,state,normalized_time,foot,sole_y,ground_y,gap\n");
        var bounds=new System.Text.StringBuilder("state,time,root_y,capsule_bottom,mesh_bottom,min_vertex,min_bone,min_weight,min_x,min_z\n");
        string[] states={"CL_CAT_StandIdle_v01_loop_96f","CL_CAT_Walk_v01_loop_60f","CL_CAT_StandToLie_v11_150f","CL_CAT_ProneBreath_v11_loop_240f","CL_CAT_LieToStand_v11_120f","CL_CAT_StandIdle_v01_loop_96f"};
        float[] durations={1.6f,2f,2.5f,1f,2f,1f};
        for(int s=0;s<states.Length;s++)
        {
            Assert.That(animator.HasState(0,Animator.StringToHash(states[s])),Is.True,states[s]);
            animator.Play(states[s],0,0); animator.speed=1;
            yield return null;
            int samples=Mathf.CeilToInt(durations[s]/.1f);
            for(int sample=0;sample<samples;sample++)
            {
                SampleFootContact(cat,skin,baked,animator,rows);
                float bottom=float.PositiveInfinity;
                var points=baked.vertices;int lowest=0;Vector3 low=Vector3.zero;
                for(int i=0;i<points.Length;i++)
                {
                    var p=BakedWorld(skin,points[i]);
                    if(p.y<bottom){bottom=p.y;lowest=i;low=p;}
                }
                var weight=skin.sharedMesh.boneWeights[lowest];
                bounds.AppendLine(System.FormattableString.Invariant($"{states[s]},{Time.time:F4},{cat.transform.position.y:F5},{capsule.bounds.min.y:F5},{bottom:F5},{lowest},{skin.bones[weight.boneIndex0].name},{weight.weight0:F5},{low.x:F5},{low.z:F5}"));
                if(sample==samples/2)Capture("grounding-"+s);
                yield return new WaitForSeconds(.1f);
            }
        }
        File.WriteAllText("Reports/ProneImportReview/installed/grounding-chain-contact.csv",rows.ToString());
        File.WriteAllText("Reports/ProneImportReview/installed/grounding-chain-bounds.csv",bounds.ToString());
        Object.Destroy(baked);
    }
    [UnityTest] public IEnumerator CompareTwoAndFourBoneProneSkinning()
    {
        SceneManager.LoadScene("CatLifeMobile"); yield return new WaitForSeconds(.6f);
        var cat=GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled=false;
        ((Behaviour)cat.GetComponent("CatAnimationController")).enabled=false;
        var animator=cat.GetComponent<Animator>();
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        animator.Play("CL_CAT_ProneBreath_v11_loop_240f",0,0);animator.speed=0;
        yield return null;
        var baked=new Mesh();Vector3[] baseline=null;
        var report=new System.Text.StringBuilder();
        foreach(var quality in new[]{SkinQuality.Bone2,SkinQuality.Bone4})
        {
            skin.quality=quality;yield return null;
            skin.BakeMesh(baked,false);var points=baked.vertices;
            float min=float.PositiveInfinity,maxDifference=0;int lowest=0;
            for(int i=0;i<points.Length;i++)
            {
                float y=BakedWorld(skin,points[i]).y;
                if(y<min){min=y;lowest=i;}
                if(baseline!=null)maxDifference=Mathf.Max(maxDifference,Vector3.Distance(points[i],baseline[i]));
            }
            report.AppendLine($"quality={quality}; global={QualitySettings.skinWeights}; bottom={min}; vertex={lowest}; maxDifferenceFromTwo={maxDifference}");
            if(baseline==null)baseline=points;
            Capture("prone-quality-"+quality);
        }
        File.WriteAllText("Reports/ProneImportReview/installed/prone-quality-comparison.txt",report.ToString());
        Object.Destroy(baked);
    }
    [UnityTest] public IEnumerator TitleDotAndCenterRemainClickableAfterPagesClose()
    {
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        var app=GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var views=(System.Collections.Generic.Dictionary<string,GameObject>)app.GetType().GetField("views",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);
        foreach(string page in new[]{"ShowRecords","ShowGrowth","ShowSettings"})
        {
            app.GetType().GetMethod(page,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,null);
            app.GetType().GetMethod("ShowHome",BindingFlags.NonPublic|BindingFlags.Instance,null,System.Type.EmptyTypes,null).Invoke(app,null);
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach(string part in new[]{"BrandDot","BrandTitle"})
            {
                var rect=(RectTransform)views["TitleButton"].transform.Find(part);
                Vector2 point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=point};
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
                Assert.That(hits.Count,Is.GreaterThan(0),part);
                var button=hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>();
                Debug.Log($"TITLE_RAY page={page} part={part} point={point} hit={hits[0].gameObject.name} button={(button!=null?button.name:"none")}");
                Assert.That(button,Is.EqualTo(views["TitleButton"].GetComponent<UnityEngine.UI.Button>()));
            }
        }
    }
    static void SampleFootContact(GameObject cat,SkinnedMeshRenderer skin,Mesh baked,Animator animator,System.Text.StringBuilder rows)
    {
        skin.BakeMesh(baked,false);
        var vertices=baked.vertices; var weights=skin.sharedMesh.boneWeights;
        var state=animator.GetCurrentAnimatorStateInfo(0);
        foreach(string foot in new[]{"frontleg2","R_frontleg2","backleg2","R_backleg2"})
        {
            int bone=System.Array.FindIndex(skin.bones,b=>b.name==foot);
            Assert.That(bone,Is.GreaterThanOrEqualTo(0),foot);
            Vector3 sole=new Vector3(0,float.PositiveInfinity,0);
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];
                float influence=(w.boneIndex0==bone?w.weight0:0)+(w.boneIndex1==bone?w.weight1:0)+(w.boneIndex2==bone?w.weight2:0)+(w.boneIndex3==bone?w.weight3:0);
                if(influence<.99f) continue;
                Vector3 p=BakedWorld(skin,vertices[i]);
                if(p.y<sole.y) sole=p;
            }
            Assert.That(float.IsInfinity(sole.y),Is.False,foot+" has no sole vertices");
            float ground=float.NaN; float distance=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(sole+Vector3.up*2f,Vector3.down,5f,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(cat.transform) || hit.distance>=distance) continue;
                distance=hit.distance; ground=hit.point.y;
            }
            string clip=animator.GetCurrentAnimatorClipInfo(0)[0].clip.name;
            rows.AppendLine(System.FormattableString.Invariant($"{Time.time:F4},{cat.GetComponent<Rigidbody>().position.y:F4},{clip},{state.normalizedTime:F4},{foot},{sole.y:F4},{ground:F4},{sole.y-ground:F4}"));
        }
    }
    // This imported rig's baked vertices already include its inherited 4.5 scale.
    // Applying TransformPoint scales the evaluated surface a second time.
    static Vector3 BakedWorld(SkinnedMeshRenderer skin,Vector3 vertex)
        => skin.transform.position+skin.transform.rotation*vertex;
    static void Capture(string name)
    {
        var camera=Camera.main; var target=new RenderTexture(540,1200,24);
        var old=camera.targetTexture; camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(540,1200,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,540,1200),0,0);image.Apply();
        Directory.CreateDirectory("Reports/ProneImportReview/installed");
        File.WriteAllBytes("Reports/ProneImportReview/installed/"+name+".png",image.EncodeToPNG());
        camera.targetTexture=old;RenderTexture.active=null;target.Release();Object.Destroy(image);Object.Destroy(target);
    }
}

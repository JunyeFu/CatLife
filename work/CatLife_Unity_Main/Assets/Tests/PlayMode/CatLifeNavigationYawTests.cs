using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using CatLife.Mobile;
using System.IO;
using System.Text;

public sealed class CatLifeNavigationYawTests
{
    private string savedData;
    [SetUp]
    public void UseDeviceEquivalentSettings()
    {
        savedData = PlayerPrefs.GetString("CatLife.Mobile.Data.v1", "");
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        data.settings.localBehaviorStatsEnabled = false;
        data.settings.autoFocusAdaptationSeconds = 0;
        PlayerPrefs.SetString("CatLife.Mobile.Data.v1", CatLifeDataJson.Serialize(data));
    }

    [TearDown]
    public void RestoreSettings()
    {
        if (savedData.Length == 0) PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        else PlayerPrefs.SetString("CatLife.Mobile.Data.v1", savedData);
    }

    [UnityTest]
    public IEnumerator MeasureRuntimeCatVisibilityEnvelope()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(2);
        var cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled=false;
        var animator=cat.GetComponentInChildren<Animator>();
        animator.enabled=false;
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh=new Mesh();
        var output=new StringBuilder("clip,maxRadius,minY,maxY\n");
        float allRadius=0,allHeight=0;
        foreach(var clip in animator.runtimeAnimatorController.animationClips)
        {
            float radius=0,minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
            int frameCount=Mathf.CeilToInt(clip.length*clip.frameRate);
            for(int frame=0;frame<=frameCount;frame++)
            {
                clip.SampleAnimation(cat,clip.length*frame/frameCount);
                skin.BakeMesh(mesh);
                Quaternion inverseCat=Quaternion.Inverse(cat.transform.rotation);
                Quaternion localRotation=inverseCat*skin.transform.rotation;
                Vector3 localOffset=inverseCat*(skin.transform.position-cat.transform.position);
                foreach(var vertex in mesh.vertices)
                {
                    // BakeMesh already includes this imported renderer's scale;
                    // match the established terrain-contact mesh measurement.
                    Vector3 local=localOffset+localRotation*vertex;
                    radius=Mathf.Max(radius,new Vector2(local.x,local.z).magnitude);
                    minY=Mathf.Min(minY,local.y);maxY=Mathf.Max(maxY,local.y);
                }
            }
            allRadius=Mathf.Max(allRadius,radius);allHeight=Mathf.Max(allHeight,maxY);
            output.AppendLine($"{clip.name},{radius:F4},{minY:F4},{maxY:F4}");
        }
        Object.Destroy(mesh);
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/wp03-r11-visibility-envelope.csv")),output.ToString());
        Assert.That(skin.sharedMesh.vertexCount,Is.GreaterThan(0));
        Assert.That(allRadius,Is.LessThanOrEqualTo(1.25f));
        Assert.That(allHeight,Is.LessThanOrEqualTo(1.7f));
    }

    [UnityTest]
    public IEnumerator TrappedPointHasVisibleSupportedExit()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(2);
        Camera.main.aspect = 1080f / 2424f;
        var cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        var body = cat.GetComponent<Rigidbody>();
        var agent = cat.GetComponent<NavMeshAgent>();
        var navigation = cat.GetComponent("CatNavigationAgent");
        var planner = cat.GetComponent("CatDestinationPlanner");
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var radiusField = planner.GetType().GetField("visibleBodyRadius",flags);
        float originalRadius = (float)radiusField.GetValue(planner);
        var landing = navigation.GetType().GetMethod("TrySupportedLanding",flags);
        navigation.GetType().GetMethod("StopSoft").Invoke(navigation,null);
        Vector3 origin = new Vector3(-.039f,.278f,4.009f);
        Assert.That(NavMesh.SamplePosition(origin,out var anchor,1,NavMesh.AllAreas),Is.True);
        agent.Warp(anchor.position); body.position=origin; cat.transform.position=origin;
        body.linearVelocity=Vector3.zero; Physics.SyncTransforms();
        var output = new StringBuilder();
        int normalExits = 0;
        foreach (float radius in new[] { originalRadius, 0f })
        {
            radiusField.SetValue(planner,radius);
            int supported=0; Vector3 closest=Vector3.zero; float nearest=float.PositiveInfinity;
            for(int x=-30;x<=30;x++) for(int z=-30;z<=30;z++)
            {
                object[] args={origin+new Vector3(x*.1f,0,z*.1f),Vector3.zero};
                if (!(bool)landing.Invoke(navigation,args)) continue;
                if(radius==originalRadius)
                {
                    for(int angle=0;angle<64;angle++) for(int cap=0;cap<2;cap++)
                    {
                        float radians=angle*Mathf.PI/32;
                        Vector3 rim=(Vector3)args[1]+new Vector3(Mathf.Cos(radians)*radius,cap*1.7f,Mathf.Sin(radians)*radius);
                        Vector3 viewport=Camera.main.WorldToViewportPoint(rim);
                        Assert.That(viewport.x,Is.InRange(.045f,.955f));
                        Assert.That(viewport.y,Is.InRange(.19f,.83f));
                        Assert.That(viewport.z,Is.GreaterThan(0));
                    }
                }
                supported++;
                float distance=Vector3.Distance(origin,(Vector3)args[1]);
                if(distance<nearest){nearest=distance;closest=(Vector3)args[1];}
            }
            if(radius==originalRadius)normalExits=supported;
            output.AppendLine($"visibleRadius={radius:F2}; supported={supported}; nearest={nearest:F4}; closest={closest:F4}");
        }
        radiusField.SetValue(planner,originalRadius);
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/wp03-r11-visible-exit.txt")),output.ToString());
        Assert.That(normalExits,Is.GreaterThan(0),output.ToString());
    }

    [UnityTest]
    public IEnumerator ArrivedFootprintMatchesPlannedSupport()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(2);
        Camera.main.aspect = 1080f / 2424f;
        var cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        var body = cat.GetComponent<Rigidbody>();
        var agent = cat.GetComponent<NavMeshAgent>();
        var navigation = cat.GetComponent("CatNavigationAgent");
        var navType = navigation.GetType();
        var feet = (Vector3[])navType.GetField("StandingFeet",System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        var output = new StringBuilder();
        int unsupported = 0;
        int wrongArrivalHeading = 0;
        foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
        {
            navType.GetMethod("StopSoft").Invoke(navigation,null);
            Vector3 start = new Vector3(0,.27f,4.5f);
            Assert.That(NavMesh.SamplePosition(start,out var anchor,1f,NavMesh.AllAreas),Is.True);
            agent.Warp(anchor.position);
            body.position = start;
            body.rotation = Quaternion.Euler(0,yaw,0);
            cat.transform.position = start;
            body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            Assert.That((bool)navType.GetMethod("TryMoveTo").Invoke(navigation,new object[]{new Vector3(-.039f,.33f,4.009f)}),Is.True);
            yield return null;
            while (agent.pathPending) yield return null;
            Vector3 target = agent.destination;
            var corners = agent.path.corners;
            Vector3 tangent = corners[corners.Length-1]-corners[corners.Length-2]; tangent.y=0;
            Quaternion plannedHeading = Quaternion.LookRotation(tangent);
            float plannedSpread = FootSurfaceSpread(target,plannedHeading,feet);
            yield return new WaitForSeconds(3);
            float actualSpread = FootSurfaceSpread(body.position,body.rotation,feet);
            float positionOnlySpread = FootSurfaceSpread(body.position,plannedHeading,feet);
            output.AppendLine($"startYaw={yaw}; target={target:F4}; plannedYaw={plannedHeading.eulerAngles.y:F2}; plannedSpread={plannedSpread:F4}; body={body.position:F4}; actualYaw={body.rotation.eulerAngles.y:F2}; actualSpread={actualSpread:F4}; positionOnlySpread={positionOnlySpread:F4}");
            if (actualSpread > .02f) unsupported++;
            if (Quaternion.Angle(body.rotation,plannedHeading) > 5f) wrongArrivalHeading++;
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/wp03-r11-arrival-footprint.txt")),output.ToString());
        Assert.That(unsupported,Is.Zero,output.ToString());
        Assert.That(wrongArrivalHeading,Is.Zero,"Arrival must retain its validated heading within 5 degrees. " + output);
    }

    private static float FootSurfaceSpread(Vector3 position, Quaternion heading, Vector3[] feet)
    {
        float low = float.PositiveInfinity, high = float.NegativeInfinity;
        string[] groundNames = { "CL_ENV_IslandBase_01", "CL_ENV_IslandGrass_01", "CL_ROAD_CentralGardenRing_01", "CL_ROAD_PlazaPath_01", "CL_ROAD_PlazaRing_01", "CL_PROP_CenterStone_01" };
        foreach (var foot in feet)
        {
            float nearest = float.PositiveInfinity, height = 0;
            foreach (var hit in Physics.RaycastAll(position+heading*foot+Vector3.up*2,Vector3.down,5,~0,QueryTriggerInteraction.Ignore))
            {
                if (System.Array.IndexOf(groundNames,hit.collider.name)<0 || hit.distance>=nearest) continue;
                nearest=hit.distance; height=hit.point.y;
            }
            if (float.IsPositiveInfinity(nearest)) return float.PositiveInfinity;
            low=Mathf.Min(low,height); high=Mathf.Max(high,height);
        }
        return high-low;
    }

    [UnityTest]
    public IEnumerator SettledAndroidPointCanStartAnotherRoute()
    {
        return CheckAndroidPointCanStartAnotherRoute(new Vector3(.164f,.279f,3.818f), "wp03-r10-next-route.txt");
    }

    [UnityTest]
    public IEnumerator TrappedAndroidPointCanPlanSupportedExit()
    {
        return CheckAndroidPointCanStartAnotherRoute(new Vector3(-.039f,.278f,4.009f), "wp03-r12-trapped-planner.txt");
    }

    private IEnumerator CheckAndroidPointCanStartAnotherRoute(Vector3 origin, string reportName)
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(2);
        Camera.main.aspect = 1080f / 2424f;
        var cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        var body = cat.GetComponent<Rigidbody>();
        var agent = cat.GetComponent<NavMeshAgent>();
        var navigation = cat.GetComponent("CatNavigationAgent");
        var planner = cat.GetComponent("CatDestinationPlanner");
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var landingMethod = navigation.GetType().GetMethod("TrySupportedLanding", flags);
        var distanceField = planner.GetType().GetField("minMoveDistance", flags);
        float originalDistance = (float)distanceField.GetValue(planner);
        navigation.GetType().GetMethod("StopSoft").Invoke(navigation,null);
        yield return new WaitForSeconds(.7f);
        Assert.That(NavMesh.SamplePosition(origin,out var anchor,1f,NavMesh.AllAreas),Is.True);
        agent.Warp(anchor.position);
        body.position = origin;
        cat.transform.position = origin;
        body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        int ordinarySupported = 0;
        var output = new StringBuilder();
        foreach (float minimum in new[] { originalDistance, 0f })
        {
            distanceField.SetValue(planner, minimum);
            int supported = 0;
            Vector3 first = Vector3.zero;
            for (int x=-20; x<=20; x++) for (int z=-20; z<=20; z++)
            {
                object[] args = { origin + new Vector3(x*.05f,0,z*.05f), Vector3.zero };
                if (!(bool)landingMethod.Invoke(navigation,args)) continue;
                if (supported++ == 0) first = (Vector3)args[1];
            }
            if (minimum == originalDistance) ordinarySupported = supported;
            output.AppendLine($"origin={origin:F3}; minMove={minimum:F3}; supported={supported}; first={first:F4}");
        }
        distanceField.SetValue(planner, originalDistance);
        var planMethod = System.Array.Find(planner.GetType().GetMethods(), method => method.Name == "TryPlanNext" && method.GetParameters().Length == 4);
        var planParameters = planMethod.GetParameters();
        var resolveMethod = navigation.GetType().GetMethod("TryFindSupportedLanding", flags);
        var randomState = Random.state;
        Random.InitState(1010);
        int plannedCount = 0, resolvedCount = 0;
        for (int attempt = 0; attempt < 64; attempt++)
        {
            object[] planArgs = { System.Activator.CreateInstance(planParameters[0].ParameterType), System.Enum.Parse(planParameters[1].ParameterType,"Roam"), origin, Vector3.zero };
            if (!(bool)planMethod.Invoke(planner,planArgs)) continue;
            plannedCount++;
            object[] resolveArgs = { planArgs[3], Vector3.zero };
            if ((bool)resolveMethod.Invoke(navigation,resolveArgs)) resolvedCount++;
        }
        Random.state = randomState;
        output.AppendLine($"actualPlanner: planned={plannedCount}/64; resolved={resolvedCount}/64");
        var driver = cat.GetComponent("CatBehaviorDriver");
        var scorer = cat.GetComponent("CatBehaviorBrainScorer");
        var needsModel = cat.GetComponent("CatNeedModel");
        var memory = cat.GetComponent("CatBehaviorMemory");
        var snapshot = driver.GetType().GetField("snapshot", flags).GetValue(driver);
        var needs = needsModel.GetType().GetProperty("Current").GetValue(needsModel);
        var suggestion = driver.GetType().GetMethod("GetSuggestion", flags).Invoke(driver,null);
        var decideMethod = scorer.GetType().GetMethod("TryDecide");
        output.AppendLine($"snapshot={JsonUtility.ToJson(snapshot)}");
        var scoredPlanMethod = System.Array.Find(planner.GetType().GetMethods(), method => method.Name == "TryPlanNext" && method.GetParameters().Length == 6);
        int roamDecisions = 0, scoredPlans = 0, scoredLandings = 0;
        Random.InitState(1010);
        for (int attempt = 0; attempt < 64; attempt++)
        {
            object[] decisionArgs = { snapshot, needs, memory, suggestion, null };
            Assert.That((bool)decideMethod.Invoke(scorer,decisionArgs),Is.True);
            var decision = decisionArgs[4];
            if (!(bool)decision.GetType().GetProperty("IsLocomotion").GetValue(decision)) continue;
            roamDecisions++;
            object[] planArgs = { snapshot, decision, needs, memory, origin, Vector3.zero };
            if (!(bool)scoredPlanMethod.Invoke(planner,planArgs)) continue;
            scoredPlans++;
            object[] resolveArgs = { planArgs[5], Vector3.zero };
            if ((bool)resolveMethod.Invoke(navigation,resolveArgs)) scoredLandings++;
        }
        Random.state = randomState;
        output.AppendLine($"scored: roam={roamDecisions}/64; planned={scoredPlans}; resolved={scoredLandings}");
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/",reportName)),output.ToString());
        Assert.That(ordinarySupported,Is.GreaterThan(0),output.ToString());
        Assert.That(resolvedCount,Is.GreaterThan(0),output.ToString());
        Assert.That(scoredLandings,Is.GreaterThan(0),output.ToString());
    }

    [UnityTest]
    public IEnumerator NavigationStopsOnItsValidatedLanding()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(2);
        Camera.main.aspect = 1080f / 2424f;
        var cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        var body = cat.GetComponent<Rigidbody>();
        var agent = cat.GetComponent<NavMeshAgent>();
        var navigation = cat.GetComponent("CatNavigationAgent");
        Vector3 origin = new Vector3(0,.27f,4.5f);
        Assert.That(NavMesh.SamplePosition(origin,out var anchor,1f,NavMesh.AllAreas),Is.True);
        agent.Warp(anchor.position);
        body.position = origin;
        cat.transform.position = origin;
        body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        bool accepted = (bool)navigation.GetType().GetMethod("TryMoveTo").Invoke(navigation,new object[]{ new Vector3(-.046f,.33f,3.875f) });
        Assert.That(accepted,Is.True,"Request must first pass landing validation.");
        yield return null;
        while (agent.pathPending) yield return null;
        Vector3 validatedDestination = agent.destination;
        yield return new WaitForSeconds(3);
        Vector3 error = body.position-validatedDestination; error.y=0;
        string report = $"body={body.position:F4}; validatedDestination={validatedDestination:F4}; distance={error.magnitude:F4}; stoppingDistance={agent.stoppingDistance:F4}";
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/wp03-r8-landing-stop.txt")),report);
        Assert.That(error.magnitude,Is.LessThan(.03f),report);
        float maximumSpeed = 0;
        float maximumDrift = 0;
        Vector3 settledPosition = body.position;
        for (int sample = 0; sample < 100; sample++)
        {
            yield return new WaitForFixedUpdate();
            Vector3 horizontalVelocity = body.linearVelocity; horizontalVelocity.y = 0;
            Vector3 drift = body.position - settledPosition; drift.y = 0;
            maximumSpeed = Mathf.Max(maximumSpeed, horizontalVelocity.magnitude);
            maximumDrift = Mathf.Max(maximumDrift, drift.magnitude);
        }
        string settling = $"{report}; maximumSpeed={maximumSpeed:F4}; maximumDrift={maximumDrift:F4}";
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/wp03-r9-landing-settle.txt")),settling);
        Assert.That(maximumSpeed,Is.LessThan(.05f),settling);
        Assert.That(maximumDrift,Is.LessThan(.01f),settling);
    }

    [UnityTest]
    public IEnumerator ReverseDestinationDoesNotTranslateBackwardDuringWalk()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(2);
        Camera.main.aspect = 1080f / 2424f;
        var cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        var body = cat.GetComponent<Rigidbody>();
        var agent = cat.GetComponent<NavMeshAgent>();
        var navigation = cat.GetComponent("CatNavigationAgent");
        navigation.GetType().GetMethod("StopSoft").Invoke(navigation, null);
        Vector3 origin = new Vector3(0,.27f,4.5f);
        Assert.That(NavMesh.SamplePosition(origin,out var anchor,1f,NavMesh.AllAreas),Is.True);
        agent.Warp(anchor.position);
        body.position = origin;
        cat.transform.position = origin;
        body.rotation = Quaternion.identity;
        cat.transform.rotation = Quaternion.identity;
        body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        Assert.That((bool)navigation.GetType().GetMethod("TryMoveTo").Invoke(navigation,
            new object[]{new Vector3(-.046f,.33f,3.875f)}),Is.True);
        var animation = cat.GetComponent("CatAnimationController");
        animation.GetType().GetMethod("ForceLocomotion").Invoke(animation,new object[]{true});
        var animator = cat.GetComponentInChildren<Animator>();
        var report = new StringBuilder("sample,yaw,headingError,speed,backwardDistance,clip,normalizedTime\n");
        float backwardDistance = 0;
        Vector3 previous = body.position;
        Quaternion previousRotation = body.rotation;
        for (int sample = 0; sample < 100; sample++)
        {
            yield return new WaitForFixedUpdate();
            Vector3 movement = body.position - previous; movement.y = 0;
            backwardDistance += Mathf.Max(0,-Vector3.Dot(movement,previousRotation*Vector3.forward));
            Vector3 velocity = body.linearVelocity; velocity.y = 0;
            float error = velocity.sqrMagnitude > .001f ? Vector3.Angle(body.rotation*Vector3.forward,velocity) : 0;
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            string clipName = clips.Length == 0 ? "<no-current-clip>" : clips[0].clip.name;
            report.AppendLine($"{sample},{body.rotation.eulerAngles.y:F2},{error:F2},{velocity.magnitude:F4},{backwardDistance:F4},{clipName},{animator.GetCurrentAnimatorStateInfo(0).normalizedTime:F3}");
            previous = body.position;
            previousRotation = body.rotation;
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/wp03-r12-reverse-heading.csv")),report.ToString());
        Assert.That(backwardDistance,Is.LessThan(.02f),"Forward Walk must not translate backward while turning toward a reversed destination.");
    }

    [UnityTest]
    public IEnumerator TurningBodyDoesNotFreezeItsFootstepAnimation()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(2);
        Camera.main.aspect = 1080f / 2424f;
        var cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        var body = cat.GetComponent<Rigidbody>();
        var agent = cat.GetComponent<NavMeshAgent>();
        var navigation = cat.GetComponent("CatNavigationAgent");
        navigation.GetType().GetMethod("StopSoft").Invoke(navigation,null);
        var animation = cat.GetComponent("CatAnimationController");
        var tick = animation.GetType().GetMethod("Tick");
        var animator = cat.GetComponentInChildren<Animator>();
        animation.GetType().GetMethod("ForceLocomotion").Invoke(animation,new object[]{true});
        yield return new WaitForSeconds(.2f);
        Vector3 origin = new Vector3(0,.27f,4.5f);
        Assert.That(NavMesh.SamplePosition(origin,out var anchor,1f,NavMesh.AllAreas),Is.True);
        agent.Warp(anchor.position);
        body.position = origin;
        cat.transform.position = origin;
        body.rotation = Quaternion.identity;
        cat.transform.rotation = Quaternion.identity;
        body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        Assert.That((bool)navigation.GetType().GetMethod("TryMoveTo").Invoke(navigation,
            new object[]{new Vector3(-.046f,.33f,3.875f)}),Is.True);
        var report = new StringBuilder("frame,yawDelta,bodySpeed,animatorSpeed,phaseDelta\n");
        float frozenTurnDegrees = 0;
        for (int sample=0; sample<45; sample++)
        {
            float speed01 = (float)navigation.GetType().GetProperty("Speed01").GetValue(navigation);
            bool moving = (bool)navigation.GetType().GetProperty("IsPathInProgress").GetValue(navigation);
            tick.Invoke(animation,new object[]{speed01,moving,false,0f});
            float previousYaw = body.rotation.eulerAngles.y;
            float previousPhase = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return null;
            float yawDelta = Mathf.Abs(Mathf.DeltaAngle(previousYaw,body.rotation.eulerAngles.y));
            float phaseDelta = animator.GetCurrentAnimatorStateInfo(0).normalizedTime-previousPhase;
            if (Mathf.Abs(phaseDelta)<.001f) frozenTurnDegrees += yawDelta;
            report.AppendLine($"{sample},{yawDelta:F3},{body.linearVelocity.magnitude:F4},{animator.speed:F4},{phaseDelta:F5}");
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/wp03-r16-turn-gait.csv")),report.ToString());
        Assert.That(frozenTurnDegrees,Is.LessThan(5f),"Turning must not rotate a frozen walking pose across the ground.");
    }

    // Deliberately teleports to a historical off-target stop, bypassing arrival.
    // Keep the failed investigation reproducible; normal regression tests arrival above.
    [UnityTest, Explicit("Historical off-target placement diagnostic; does not exercise the corrected arrival path.")]
    public IEnumerator AndroidStoppedPointHasSupportedLanding()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(2);
        Camera.main.aspect = 1080f / 2424f;
        var cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        var body = cat.GetComponent<Rigidbody>();
        var agent = cat.GetComponent<NavMeshAgent>();
        var navigation = cat.GetComponent("CatNavigationAgent");
        var planner = cat.GetComponent("CatDestinationPlanner");
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var validMethod = planner.GetType().GetMethod("IsDestinationValid", flags);
        var landingMethod = navigation.GetType().GetMethod("TryFindSupportedLanding", flags);
        var output = new StringBuilder();
        int trapped = 0;
        foreach (var origin in new[] { new Vector3(-.046f,.278f,4.015f), new Vector3(.133f,.279f,3.966f), new Vector3(-.033f,.270f,4.371f) })
        {
            Assert.That(NavMesh.SamplePosition(origin,out var anchor,1f,NavMesh.AllAreas), Is.True);
            agent.Warp(anchor.position);
            body.position = origin;
            cat.transform.position = origin;
            body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            int valid = 0, supported = 0;
            for (int i=0; i<24; i++)
            {
                float angle = i*Mathf.PI/12f;
                Vector3 candidate = origin + new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*.5f;
                if (!NavMesh.SamplePosition(candidate,out var hit,1f,NavMesh.AllAreas)) continue;
                if (!(bool)validMethod.Invoke(planner,new object[]{hit.position,origin,false})) continue;
                valid++;
                object[] args = { hit.position, Vector3.zero };
                if ((bool)landingMethod.Invoke(navigation,args)) supported++;
            }
            output.AppendLine($"origin={origin:F3}; valid={valid}; supported={supported}");
            if (supported == 0)
            {
                trapped++;
                int fineSupported = 0;
                Vector3 first = Vector3.zero;
                var directLanding = navigation.GetType().GetMethod("TrySupportedLanding", flags);
                for (int x=-20; x<=20; x++) for (int z=-20; z<=20; z++)
                {
                    object[] args = { origin + new Vector3(x*.025f,0,z*.025f), Vector3.zero };
                    if (!(bool)directLanding.Invoke(navigation,args)) continue;
                    if (fineSupported++ == 0) first = (Vector3)args[1];
                }
                output.AppendLine($"fineSupported={fineSupported}; first={first:F4}");
            }
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/wp03-r8-supported-landing.txt")),output.ToString());
        Assert.That(trapped, Is.Zero, output.ToString());
    }

    [UnityTest]
    public IEnumerator AndroidStoppedPointHasReachableRoamingDestinations()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(2);
        Camera.main.aspect = 1080f / 2424f;
        var cat = GameObject.Find("CatLifeMobileCat");
        var planner = cat.GetComponent("CatDestinationPlanner");
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var validMethod = planner.GetType().GetMethod("IsDestinationValid", flags);
        var forbiddenMethod = planner.GetType().GetMethod("IsForbidden", flags);
        var output = new StringBuilder();
        int androidValid = 0;
        foreach (var origin in new[] { new Vector3(-.046f,.278f,4.015f), new Vector3(-.033f,.270f,4.371f) })
        {
            int valid = 0, sampled = 0;
            for (int x = -5; x <= 5; x++) for (int z = -5; z <= 5; z++)
            {
                Vector3 candidate = origin + new Vector3(x*.1f,0,z*.1f);
                if (!NavMesh.SamplePosition(candidate, out var hit, 1f, NavMesh.AllAreas)) continue;
                sampled++;
                if ((bool)validMethod.Invoke(planner, new object[] { hit.position, origin, false })) valid++;
            }
            bool forbidden = (bool)forbiddenMethod.Invoke(planner, new object[] { origin, .3f });
            output.AppendLine($"origin={origin:F3}; forbidden={forbidden}; sampled={sampled}; valid={valid}");
            if (origin.z < 4.1f) androidValid = valid;
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/wp03-r7-destination-probe.txt")),output.ToString());
        Assert.That(androidValid, Is.GreaterThan(0), output.ToString());
    }

    [UnityTest]
    public IEnumerator ObserveStatisticsOffRoamingHeading()
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        data.settings.localBehaviorStatsEnabled = false;
        data.settings.autoFocusAdaptationSeconds = 0;
        PlayerPrefs.SetString("CatLife.Mobile.Data.v1", CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Camera.main.aspect = 1080f / 2424f;
        var cat = GameObject.Find("CatLifeMobileCat");
        var body = cat.GetComponent<Rigidbody>();
        var agent = cat.GetComponent<NavMeshAgent>();
        var animator = cat.GetComponentInChildren<Animator>();
        var report = new StringBuilder("seconds,position,yaw,desired,velocity,animatorLocalEuler,clip\n");
        for (int i = 0; i < 60; i++)
        {
            yield return new WaitForSeconds(.5f);
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            report.AppendLine($"{(i+1)*.5f:F1},\"{body.position:F3}\",{body.rotation.eulerAngles.y:F2},\"{agent.desiredVelocity:F3}\",\"{body.linearVelocity:F3}\",\"{animator.transform.localEulerAngles:F2}\",{clips[0].clip.name}");
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp03-r6-stats-off-heading.csv")), report.ToString());
        Assert.That(cat.activeInHierarchy, Is.True);
    }

    [UnityTest]
    public IEnumerator CompareFrozenYawWithUprightOnlyConstraints()
    {
        foreach (var constraints in new[] { RigidbodyConstraints.FreezeRotation,
            RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ })
        {
            var item = new GameObject("Cat navigation yaw probe");
            var body = item.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = constraints;
            for (int i = 0; i < 20; i++)
            {
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.Euler(0, 90, 0), 8.4f));
                yield return new WaitForFixedUpdate();
            }
            Debug.Log("CATLIFE_YAW_PROBE constraints=" + constraints + " yaw=" + body.rotation.eulerAngles.y);
            if (constraints != RigidbodyConstraints.FreezeRotation)
                Assert.That(Quaternion.Angle(body.rotation, Quaternion.Euler(0, 90, 0)), Is.LessThan(1f));
            Object.Destroy(item);
            yield return null;
        }
    }
}

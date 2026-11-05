#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class CatLifeRoamUprightTests
{
    const string Key = "CatLife.Mobile.Data.v1";
    const string ScenePath = "Assets/Editor/CatProneReview/Atlas512RepackedCandidate/CatLifeTownAtlasReview.unity";
    const string Group = "../wp03-r23-upright-state-20261017";
    bool hadData;
    string saved;
    int savedCaptureRate;
    UnityEngine.Events.UnityAction<Scene, LoadSceneMode> freezeAmbient;

    [SetUp] public void PreserveData()
    {
        hadData = PlayerPrefs.HasKey(Key);
        saved = PlayerPrefs.GetString(Key);
        savedCaptureRate = Time.captureFramerate;
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        data.settings.localBehaviorStatsEnabled = false;
        data.settings.autoFocusAdaptationSeconds = 0;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
    }

    [UnityTearDown] public IEnumerator RestoreData()
    {
        Time.captureFramerate = savedCaptureRate;
        SceneManager.sceneLoaded -= freezeAmbient;
        freezeAmbient = null;
        var scene = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("UprightTraceCleanup"));
        if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        if (hadData) PlayerPrefs.SetString(Key, saved); else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    [UnityTest] public IEnumerator RenderSelectedWaveThumbnail()
    {
        yield return LoadControlledScene("Assets/Editor/CatProneReview/RoamSitChainCandidate/CatLifeSitSniffReview.unity");
        Time.captureFramerate=60;
        var cat=GameObject.Find("CatLifeMobileCat");
        var animator=cat.GetComponent<Animator>();
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var app=GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var views=(System.Collections.Generic.Dictionary<string,GameObject>)app.GetType()
            .GetField("views",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app);
        views["ReviewWave"].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        const string state="CL_CAT_CrouchPawWave_v01_loop_96f";
        float started=Time.time;
        while((!animator.GetCurrentAnimatorStateInfo(0).IsName(state) || animator.IsInTransition(0)) && Time.time-started<6) yield return null;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(state),Is.True);
        var clip=animator.GetCurrentAnimatorClipInfo(0)[0].clip;
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/wp06-wave-thumbnail-final-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(output);
        var cameraObject=new GameObject("RuntimeWaveThumbnail",typeof(Camera));
        var camera=cameraObject.GetComponent<Camera>();
        camera.enabled=false; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.clear;
        camera.cullingMask=1<<31; camera.orthographic=true; camera.rect=new Rect(0,0,1,1);
        var layers=cat.GetComponentsInChildren<Transform>(true).Select(t=>(t.gameObject,t.gameObject.layer)).ToArray();
        foreach(var item in layers) item.gameObject.layer=31;
        var target=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);
        camera.targetTexture=target;
        var previousActive=RenderTexture.active;
        var rows=new StringBuilder("SourceScene=RoamSitChainCandidate/CatLifeSitSniffReview.unity\nSourceClip="+UnityEditor.AssetDatabase.GetAssetPath(clip)+"\nClipSeconds="+clip.length.ToString("R")+"\nActual PlayMode frame, runtime material/mesh and existing paw action; no Blender or animation edits.\n");
        try
        {
            int sample=0;
            foreach(float phase in new[]{.58f})
            {
                while(animator.GetCurrentAnimatorStateInfo(0).normalizedTime<phase) yield return null;
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(state),Is.True);
                // Read a completed runtime frame, including its LateUpdate terrain correction.
                yield return null;
                Bounds bounds=skin.bounds;
                for(int view=1;view<2;view++)
                {
                    camera.orthographicSize=bounds.size.y*.64f;
                    camera.transform.position=bounds.center+new Vector3(view==0 ? -3f : 3f,1.15f,4f);
                    camera.transform.LookAt(bounds.center);
                    camera.Render(); RenderTexture.active=target;
                    var pixels=new Texture2D(512,512,TextureFormat.RGBA32,false);
                    pixels.ReadPixels(new Rect(0,0,512,512),0,0); pixels.Apply();
                    string name=$"wave-{sample}-view-{view}.png";
                    File.WriteAllBytes(Path.Combine(output,name),pixels.EncodeToPNG());
                    Assert.That(pixels.GetPixel(0,0).a,Is.LessThan(.01f),"Thumbnail corners must remain genuinely transparent.");
                    rows.AppendLine($"file={name}; phase={animator.GetCurrentAnimatorStateInfo(0).normalizedTime:R}; root={cat.transform.position:F4}; bounds={bounds}; camera={camera.transform.position:F4}");
                    Object.Destroy(pixels);
                }
                sample++;
            }
            File.WriteAllText(Path.Combine(output,"source.txt"),rows.ToString());
            Debug.Log("CATLIFE_WAVE_THUMBNAILS="+output);
        }
        finally
        {
            foreach(var item in layers) item.gameObject.layer=item.layer;
            RenderTexture.active=previousActive;
            Object.Destroy(cameraObject); target.Release(); Object.Destroy(target);
        }
    }

    [UnityTest, Explicit("Historical observational probe: the >40-degree hypothesis failed; not a product acceptance test.")]
    public IEnumerator IdentifyNearVerticalHomePoseWithLiveOwners()
    {
        EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return new WaitForSeconds(1);
        Camera.main.aspect = 1080f / 2424f;
        var cat = GameObject.Find("CatLifeMobileCat");
        var driver = cat.GetComponent("CatBehaviorDriver");
        var currentState = driver.GetType().GetProperty("CurrentState");
        var actionHold = driver.GetType().GetProperty("ActionHoldRemaining");
        var animator = cat.GetComponent<Animator>();
        var contact = cat.GetComponent("CatFootTerrainContact");
        var bodyOffset = contact.GetType().GetProperty("BodyOffset");
        var correctedFeet = contact.GetType().GetProperty("CorrectedFeet");
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        Transform Bone(string name) => skin.bones.Single(bone => bone.name == name);
        var shoulderLeft = Bone("frontleg"); var shoulderRight = Bone("R_frontleg");
        var hipLeft = Bone("backleg"); var hipRight = Bone("R_backleg");
        var foreLeft = Bone("frontleg2"); var foreRight = Bone("R_frontleg2");
        var hindLeft = Bone("backleg2"); var hindRight = Bone("R_backleg2");
        var side = new GameObject("UprightTraceSideCamera").AddComponent<Camera>();
        side.orthographic = true; side.orthographicSize = 1.3f;
        side.clearFlags = CameraClearFlags.SolidColor; side.backgroundColor = Camera.main.backgroundColor;
        float start = Time.time, maxPitch = float.NegativeInfinity;
        string peakClip = "", peakBehavior = "";
        bool sawSniff = false, returned = false;
        int frame = 0, captures = 0;
        var rows = new StringBuilder("frame,seconds,behavior,hold,clip,phase,next_clip,next_phase,root_x,root_y,root_z,root_pitch,root_yaw,root_roll,torso_pitch,fore_bone_y,hind_bone_y,contact_body_offset,corrected_feet,active_shapes\n");
        var probe = new GameObject("UprightActualLateFrameProbe").AddComponent<CatTerrainLateProbe>();
        probe.sample = () =>
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            var next = animator.GetNextAnimatorStateInfo(0);
            string clip = string.Join("+", animator.GetCurrentAnimatorClipInfo(0).Select(info => info.clip.name));
            string nextClip = animator.IsInTransition(0) ? string.Join("+", animator.GetNextAnimatorClipInfo(0).Select(info => info.clip.name)) : "";
            Vector3 shoulders = (shoulderLeft.position + shoulderRight.position) * .5f;
            Vector3 hips = (hipLeft.position + hipRight.position) * .5f;
            Vector3 torso = shoulders - hips;
            float pitch = Mathf.Atan2(torso.y, new Vector2(torso.x, torso.z).magnitude) * Mathf.Rad2Deg;
            string shapes = string.Join("|", Enumerable.Range(0, skin.sharedMesh.blendShapeCount)
                .Where(index => skin.GetBlendShapeWeight(index) > .1f)
                .Select(index => skin.sharedMesh.GetBlendShapeName(index) + ":" + skin.GetBlendShapeWeight(index).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));
            Vector3 p = cat.transform.position, e = cat.transform.eulerAngles;
            rows.AppendLine(FormattableString.Invariant($"{frame},{Time.time-start:F4},{currentState.GetValue(driver)},{(float)actionHold.GetValue(driver):F4},{clip},{state.normalizedTime:F4},{nextClip},{next.normalizedTime:F4},{p.x:F5},{p.y:F5},{p.z:F5},{e.x:F3},{e.y:F3},{e.z:F3},{pitch:F3},{(foreLeft.position.y+foreRight.position.y)*.5f:F5},{(hindLeft.position.y+hindRight.position.y)*.5f:F5},{bodyOffset.GetValue(contact)},{correctedFeet.GetValue(contact)},{shapes}"));
            if (pitch > maxPitch) { maxPitch = pitch; peakClip = clip; peakBehavior = currentState.GetValue(driver).ToString(); }
            bool sniff = state.IsName("CL_CAT_SitCuriousSniff_v01_loop_112f");
            sawSniff |= sniff;
            returned |= sawSniff && !sniff && !animator.IsInTransition(0);
            if (sniff && state.normalizedTime > .15f && captures < 3)
            {
                side.transform.SetPositionAndRotation(cat.transform.position + new Vector3(4, 1.1f, 0),
                    Quaternion.LookRotation(cat.transform.position + Vector3.up * .7f - (cat.transform.position + new Vector3(4, 1.1f, 0))));
                CatLifeVisiblePawTests.Capture(Camera.main, frame, Group + "/home", 540, 1212);
                CatLifeVisiblePawTests.Capture(side, frame, Group + "/side", 1000, 700);
                captures++;
            }
            frame++;
        };
        while (Time.time - start < 60 && (!returned || Time.time - start < 10)) yield return null;
        probe.sample = null;
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp03-r23-upright-state-20261017"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "live-late-frames.csv"), rows.ToString());
        File.WriteAllText(Path.Combine(directory, "identification.txt"), FormattableString.Invariant(
            $"Scene={ScenePath}\nFrames={frame}; observedSniff={sawSniff}; returned={returned}; captures={captures}\nPeak torso pitch={maxPitch:F3}; behavior={peakBehavior}; clip={peakClip}\nAll normal owners, gravity and terrain contact remained enabled; diagnostic coverage, not product acceptance.\n"));
        Object.Destroy(side.gameObject); Object.Destroy(probe.gameObject);
        Assert.That(sawSniff && returned, Is.True, "Need the actual autonomous sniff entry and exit.");
        Assert.That(maxPitch, Is.GreaterThan(40), "The target near-vertical failure was not reproduced.");
    }

    [UnityTest] public IEnumerator SitSniffSequenceFinishesBeforeStandingHandoff()
    {
        const string sequenceName = "CL_CAT_SitSniffStand_v01_280f";
        Time.captureFramerate = 60;
        yield return LoadControlledScene("Assets/Editor/CatProneReview/RoamSitChainCandidate/CatLifeSitSniffReview.unity");
        var cat = GameObject.Find("CatLifeMobileCat");
        var driver = cat.GetComponent("CatBehaviorDriver");
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var control = cat.GetComponent("CatAnimationController");
        var animator = cat.GetComponent<Animator>();
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        driver.GetType().GetField("actionHoldUntil", fields).SetValue(driver, 0f);
        control.GetType().GetMethod("ForceLocomotion").Invoke(control, new object[] { false });
        yield return new WaitForSeconds(.4f);
        var apply = driver.GetType().GetMethod("ApplyState", fields);
        var sniffState = Enum.Parse(apply.GetParameters()[0].ParameterType, "CuriousSniff");
        apply.Invoke(driver, new[] { sniffState });
        var holdProperty = driver.GetType().GetProperty("ActionHoldRemaining");
        float initialHold = (float)holdProperty.GetValue(driver);
        var router = cat.GetComponent("CatActionRouter");
        Assert.That(initialHold, Is.GreaterThan(0), "Request must be routed before measuring its playback: " +
            router.GetType().GetProperty("LastDecision").GetValue(router));
        var clip = animator.runtimeAnimatorController.animationClips.Single(item => item.name == sequenceName);
        string output = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../../Reports/wp03-sit-chain-review-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(output);
        Debug.Log("CATLIFE_SIT_CHAIN_REPORT=" + output);

        string[] feet = { "frontleg2", "R_frontleg2", "backleg2", "R_backleg2" };
        var subsets = feet.Select(_ => new System.Collections.Generic.List<int>()).ToArray();
        using (var counts = skin.sharedMesh.GetBonesPerVertex())
        using (var weights = skin.sharedMesh.GetAllBoneWeights())
        {
            int offset = 0;
            int[] indices = feet.Select(name => Array.FindIndex(skin.bones, bone => bone.name == name)).ToArray();
            for (int vertex = 0; vertex < counts.Length; vertex++)
            {
                for (int f = 0; f < 4; f++)
                {
                    float weight = 0;
                    for (int j = 0; j < counts[vertex]; j++)
                        if (weights[offset + j].boneIndex == indices[f]) weight += weights[offset + j].weight;
                    if (weight >= .99f) subsets[f].Add(vertex);
                }
                offset += counts[vertex];
            }
        }
        var side = new GameObject("SitChainActualSide").AddComponent<Camera>();
        side.enabled = false; side.orthographic = true; side.orthographicSize = 1.4f;
        side.transform.position = cat.transform.position + new Vector3(4, 1.1f, 0);
        side.transform.LookAt(cat.transform.position + Vector3.up * .85f);
        Camera.main.aspect = 1080f / 2424f;
        var baked = new Mesh();
        var rows = new StringBuilder("frame,time,clip,phase,hold,min_gap,nearest_support_gap,root_y,root_pitch,root_roll\n");
        float start = Time.time, maxPhase = 0, minGap = float.PositiveInfinity, worstSupport = 0;
        bool returned = false;
        int frame = 0;
        var probe = new GameObject("SitChainActualLateProbe").AddComponent<CatTerrainLateProbe>();
        probe.sample = () =>
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            string actualClip = string.Join("+", animator.GetCurrentAnimatorClipInfo(0).Select(info => info.clip.name));
            if (state.IsName(sequenceName)) maxPhase = Mathf.Max(maxPhase, state.normalizedTime);
            returned |= Time.time - start > clip.length && state.IsName("CL_CAT_StandIdle_v01_loop_96f") && !animator.IsInTransition(0);
            skin.BakeMesh(baked, false);
            var vertices = baked.vertices;
            float localMin = float.PositiveInfinity, nearest = float.PositiveInfinity;
            for (int f = 0; f < 4; f++)
            {
                Vector3 sole = subsets[f].Select(index => skin.transform.position + skin.transform.rotation * vertices[index]).OrderBy(point => point.y).First();
                var ground = Physics.RaycastAll(sole + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore)
                    .Where(hit => !hit.transform.IsChildOf(cat.transform)).OrderBy(hit => hit.distance).First();
                float gap = sole.y - ground.point.y;
                localMin = Mathf.Min(localMin, gap); nearest = Mathf.Min(nearest, gap);
            }
            minGap = Mathf.Min(minGap, localMin); worstSupport = Mathf.Max(worstSupport, nearest);
            var euler = cat.transform.eulerAngles;
            rows.AppendLine(FormattableString.Invariant($"{frame},{Time.time-start:F5},{actualClip},{state.normalizedTime:F5},{(float)holdProperty.GetValue(driver):F5},{localMin:R},{nearest:R},{cat.transform.position.y:R},{euler.x:R},{euler.z:R}"));
            CatLifeExitActionTests.Capture(side, Path.Combine(output, "side"), frame, 800, 560);
            if (frame % 6 == 0) CatLifeExitActionTests.Capture(Camera.main, Path.Combine(output, "home"), frame, 540, 1212);
            frame++;
        };
        while (Time.time - start < clip.length + .65f) yield return null;
        probe.sample = null;
        File.WriteAllText(Path.Combine(output, "late-frames.csv"), rows.ToString());
        File.WriteAllText(Path.Combine(output, "result.txt"), FormattableString.Invariant(
            $"Clip={sequenceName}; seconds={clip.length:R}; initialHold={initialHold:R}\nFrames={frame}; maxPhase={maxPhase:R}; returned={returned}; minGap={minGap:R}; nearestSupportMax={worstSupport:R}\n") +
            "Controlled ambient request with live driver/Animator/physics/contact; autonomous decisions held. Fixed 60fps Unity simulation, not emulator/performance acceptance.\n");
        Object.Destroy(probe.gameObject); Object.Destroy(side.gameObject); Object.Destroy(baked);
        Assert.That(initialHold, Is.GreaterThanOrEqualTo(clip.length), "The driver must own the complete non-looping sequence.");
        Assert.That(maxPhase, Is.GreaterThanOrEqualTo(.99f), "Do not cut the chain back to Stand during its seated phase.");
        Assert.That(returned, Is.True);
        Assert.That(minGap, Is.GreaterThanOrEqualTo(0), "The actual baked paw surface must not penetrate the sampled ground.");
        Assert.That(worstSupport, Is.LessThan(.015f), "At least one supporting paw must remain grounded.");
    }

    [UnityTest] public IEnumerator LegacyLoopHoldAndNaturalArrivalRemainUnchanged()
    {
        yield return LoadControlledScene(ScenePath);
        var cat = GameObject.Find("CatLifeMobileCat");
        var driver = cat.GetComponent("CatBehaviorDriver");
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var animator = cat.GetComponent<Animator>();
        var navigation = cat.GetComponent("CatNavigationAgent");
        var apply = driver.GetType().GetMethod("ApplyState", fields);
        var sniffState = Enum.Parse(apply.GetParameters()[0].ParameterType, "CuriousSniff");
        Assert.That((float)driver.GetType().GetMethod("GetHoldSeconds", fields).Invoke(driver, new[] { sniffState, (object)false }),
            Is.EqualTo(1.4f).Within(.0001f), "The original loop policy must not silently become a longer action.");
        var groundTap = driver.GetType().GetMethod("NotifyGroundTapped");
        Assert.That((bool)groundTap.Invoke(driver, new object[] { GameObject.Find("Interest_Left_Garden").transform.position }), Is.True);
        float wait = Time.time;
        while (!(bool)driver.GetType().GetField("pathWasInProgress", fields).GetValue(driver) && Time.time - wait < 2) yield return null;
        Assert.That((bool)driver.GetType().GetField("pathWasInProgress", fields).GetValue(driver), Is.True);
        apply.Invoke(driver, new[] { sniffState });
        yield return new WaitForSeconds(.3f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_SitCuriousSniff_v01_loop_112f"), Is.True,
            "A deliberately stopped route must not reclaim the Animator from the newly routed action.");
        yield return new WaitForSeconds(1.4f);

        Assert.That((bool)groundTap.Invoke(driver, new object[] { GameObject.Find("Interest_Left_Garden").transform.position }), Is.True,
            "Resume the same accepted destination after the interrupting action.");
        var inProgress = navigation.GetType().GetProperty("IsPathInProgress");
        wait = Time.time;
        while ((bool)inProgress.GetValue(navigation) && Time.time - wait < 12) yield return null;
        Assert.That((bool)inProgress.GetValue(navigation), Is.False);
        yield return new WaitForSeconds(.25f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"), Is.True,
            "Natural arrival still has to return to standing, not sitting.");
    }

    [UnityTest] public IEnumerator CompletedSitSniffCanWalkToValidatedLanding()
    {
        Time.captureFramerate = 60;
        yield return BeginCandidateSniff(.98f);
        var cat = GameObject.Find("CatLifeMobileCat");
        var driver = cat.GetComponent("CatBehaviorDriver");
        var animator = cat.GetComponent<Animator>();
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var hold = driver.GetType().GetProperty("ActionHoldRemaining");
        while ((float)hold.GetValue(driver) > 0) yield return null;
        yield return new WaitForSeconds(.2f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"), Is.True);
        Assert.That((bool)driver.GetType().GetMethod("NotifyGroundTapped").Invoke(driver,
            new object[] { GameObject.Find("Interest_Left_Garden").transform.position }), Is.True);
        var agent = cat.GetComponent<UnityEngine.AI.NavMeshAgent>();
        while (agent.pathPending) yield return null;
        var destination = agent.destination;
        float pathLength = 0;
        var corners = agent.path.corners;
        for (int i = 1; i < corners.Length; i++) pathLength += Vector3.Distance(corners[i-1], corners[i]);
        var navigation = cat.GetComponent("CatNavigationAgent");
        var active = navigation.GetType().GetProperty("IsPathInProgress");
        Vector3 origin = cat.transform.position;
        bool sawMovingWalk = false;
        float maxDistance = 0, minimumGap = float.PositiveInfinity;
        int frame = 0;
        string output = ReviewOutput("walk-handoff");
        var baked = new Mesh();
        int[][] feet = FindPawVertices(skin);
        var rows = new StringBuilder("frame,time,clip,phase,path_active,x,y,z,minimum_paw_gap\n");
        var probe = new GameObject("SitToRealWalkLateProbe").AddComponent<CatTerrainLateProbe>();
        float started = Time.time;
        probe.sample = () =>
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            Vector3 delta = cat.transform.position - origin; delta.y = 0;
            maxDistance = Mathf.Max(maxDistance, delta.magnitude);
            float gap = MeasurePawGap(cat, skin, baked, feet);
            minimumGap = Mathf.Min(minimumGap, gap);
            sawMovingWalk |= state.IsName("CL_CAT_Walk_v01_loop_60f") && delta.magnitude > .2f;
            Vector3 p = cat.transform.position;
            string clip = string.Join("+", animator.GetCurrentAnimatorClipInfo(0).Select(info => info.clip.name));
            rows.AppendLine(FormattableString.Invariant($"{frame},{Time.time-started:R},{clip},{state.normalizedTime:R},{active.GetValue(navigation)},{p.x:R},{p.y:R},{p.z:R},{gap:R}"));
            frame++;
        };
        while ((bool)active.GetValue(navigation) && Time.time - started < pathLength / agent.speed * 2 + 3) yield return null;
        yield return new WaitForSeconds(.3f);
        probe.sample = null;
        Vector3 error = cat.transform.position - destination; error.y = 0;
        File.WriteAllText(Path.Combine(output, "late-frames.csv"), rows.ToString());
        File.WriteAllText(Path.Combine(output, "result.txt"), FormattableString.Invariant(
            $"frames={frame}; realMovingWalk={sawMovingWalk}; maxRootDistance={maxDistance:R}; landingError={error.magnitude:R}; minPawGap={minimumGap:R}\n"));
        Object.Destroy(probe.gameObject); Object.Destroy(baked);
        Assert.That(sawMovingWalk, Is.True, "A looping Walk without actual displacement is not a handoff.");
        Assert.That((bool)active.GetValue(navigation), Is.False);
        Assert.That(error.magnitude, Is.LessThan(agent.stoppingDistance + .2f));
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"), Is.True);
        Assert.That(minimumGap, Is.GreaterThanOrEqualTo(0));
    }

    [UnityTest] public IEnumerator TransitionTakesOverSitDownWithoutSkippingStanding()
    { yield return ReviewSessionTakeover(.18f); }
    [UnityTest] public IEnumerator TransitionTakesOverSniffWithoutSkippingStanding()
    { yield return ReviewSessionTakeover(.55f); }
    [UnityTest] public IEnumerator TransitionTakesOverRiseWithoutSkippingStanding()
    { yield return ReviewSessionTakeover(.82f); }

    [UnityTest] public IEnumerator CancellingAutoTransitionCannotResumeOldLieDown()
    {
        Time.captureFramerate = 60;
        var configured = CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key));
        configured.settings.localBehaviorStatsEnabled = true;
        configured.settings.autoFocusAdaptationSeconds = 30;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(configured));
        yield return BeginCandidateSniff(.55f);
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var phase = app.GetType().GetProperty("CurrentPhase");
        var evaluate = app.GetType().GetMethod("EvaluateAutoFocus");
        var snapshotType = evaluate.GetParameters()[0].ParameterType;
        object snapshot = snapshotType.GetMethod("CreateDefault").Invoke(null, null);
        snapshotType.GetField("focusConfidence").SetValue(snapshot, .82f);
        var attention = snapshotType.GetField("attentionBand");
        attention.SetValue(snapshot, Enum.Parse(attention.FieldType, "Stable"));
        evaluate.Invoke(app, new object[] { snapshot, 31f });
        Assert.That(phase.GetValue(app), Is.EqualTo(CatLifeSessionPhase.Transition));
        yield return new WaitForSeconds(.2f);
        GameObject.Find("AutoFocusCancel").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Assert.That(phase.GetValue(app), Is.EqualTo(CatLifeSessionPhase.Normal));
        var cat = GameObject.Find("CatLifeMobileCat");
        var animator = cat.GetComponent<Animator>();
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        var owns = coordinator.GetType().GetProperty("BehaviorOwnsAnimator");
        int errors = 0, frame = 0;
        float started = Time.time;
        string output = ReviewOutput("auto-cancel-handoff");
        var rows = new StringBuilder("frame,time,phase,clip,behavior_owner\n");
        var probe = new GameObject("CancelledSessionLateProbe").AddComponent<CatTerrainLateProbe>();
        probe.sample = () =>
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool owner = (bool)owns.GetValue(coordinator);
            if (!owner || (CatLifeSessionPhase)phase.GetValue(app) != CatLifeSessionPhase.Normal ||
                state.IsName("CL_CAT_StandToLie_v11_150f") || state.IsName("CL_CAT_ProneBreath_v11_loop_240f")) errors++;
            string clip = string.Join("+", animator.GetCurrentAnimatorClipInfo(0).Select(info => info.clip.name));
            rows.AppendLine(FormattableString.Invariant($"{frame++},{Time.time-started:R},{phase.GetValue(app)},{clip},{owner}"));
        };
        yield return new WaitForSeconds(4);
        probe.sample = null;
        var saved = CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key));
        File.WriteAllText(Path.Combine(output, "late-frames.csv"), rows.ToString());
        File.WriteAllText(Path.Combine(output, "result.txt"), $"frames={frame}; staleDownOrOwnershipErrors={errors}; records={saved.records.Count}; phase={phase.GetValue(app)}\nSynthetic stable aggregate starts auto-transition; real cancel button used; no AI request.\n");
        CatLifeExitActionTests.Capture(Camera.main, Path.Combine(output, "final-world"), 0, 540, 1212);
        Object.Destroy(probe.gameObject);
        Assert.That(errors, Is.Zero, "Cancellation must stop the pending recovery-to-Down callback.");
        Assert.That(saved.records.Count, Is.Zero);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"), Is.True);
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
            Assert.That(skin.GetBlendShapeWeight(i), Is.LessThan(1f), "Standing must not retain " + skin.sharedMesh.GetBlendShapeName(i));
    }

    [UnityTest] public IEnumerator SessionTakeoverDoesNotTeleportTheRoamingRoot()
    {
        yield return BeginCandidateSniff(.55f);
        var cat = GameObject.Find("CatLifeMobileCat");
        var body = cat.GetComponent<Rigidbody>();
        body.position += new Vector3(.25f, 0, .5f);
        body.rotation = Quaternion.identity;
        Physics.SyncTransforms();
        Vector3 before = body.position;
        Quaternion heading = body.rotation;
        GameObject.Find("StartButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        GameObject.Find("SetupStart").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        float jump = Vector3.Distance(before, body.position);
        float turn = Quaternion.Angle(heading, body.rotation);
        Debug.Log(FormattableString.Invariant($"SESSION_ENTRY_ROOT immediateJump={jump:R} immediateTurn={turn:R}"));
        Assert.That(jump, Is.LessThan(.0001f), "Starting preparation must not teleport a roaming cat to its focus anchor.");
        Assert.That(turn, Is.LessThan(.01f), "Starting preparation must not snap the roaming heading.");
    }

    [UnityTest] public IEnumerator SessionPreparationWalksToAnchorBeforeStartingClock()
    { yield return ReviewAnchorApproach(new Vector3(-.001f,0,4.077f),171.88f); }

    [UnityTest] public IEnumerator R28ObservedStartCanReachAnchorBeforeStartingClock()
    { yield return ReviewAnchorApproach(new Vector3(-.029f,0,4.521f),348.65f); }

    [UnityTest] public IEnumerator R28PortraitStartCanReachAnchorBeforeStartingClock()
    { yield return ReviewAnchorApproach(new Vector3(-.029f,0,4.521f),348.65f, true); }

    [UnityTest] public IEnumerator TurnCandidateReachesFocusWithoutPoseOverride()
    { yield return ReviewAnchorApproach(new Vector3(-.029f,0,4.521f),348.65f,true,
        "Assets/Editor/CatProneReview/NavigationTurnCandidate/CatLifeNavigationTurnReview.unity"); }

    [UnityTest] public IEnumerator FigmaPortraitStartCanReachAnchorBeforeStartingClock()
    { yield return ReviewAnchorApproach(new Vector3(-.029f,0,4.521f),348.65f,true,
        "Assets/Editor/CatProneReview/FigmaUiCandidate/CatLifeFigmaUiReview.unity"); }

    [UnityTest] public IEnumerator MainPortraitStartCanReachAnchorBeforeStartingClock()
    { yield return ReviewAnchorApproach(new Vector3(-.029f,0,4.521f),348.65f,true,
        "Assets/Scenes/CatLifeMobile.unity"); }

    IEnumerator ReviewAnchorApproach(Vector3 observedPosition, float observedYaw, bool portrait = false, string scene = null)
    {
        Time.captureFramerate = 60;
        if (scene == null) yield return BeginCandidateSniff(.55f);
        else { yield return LoadControlledScene(scene); yield return new WaitForSeconds(.5f); }
        if (portrait) Camera.main.aspect = 1080f / 2424f;
        var cat = GameObject.Find("CatLifeMobileCat");
        var body = cat.GetComponent<Rigidbody>();
        var animator = cat.GetComponent<Animator>();
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        // Seed an observed device start; this is a fixture, not product motion.
        body.position = new Vector3(observedPosition.x, body.position.y, observedPosition.z);
        body.rotation = Quaternion.Euler(0, observedYaw, 0);
        Physics.SyncTransforms();
        yield return null;
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var phase = app.GetType().GetProperty("CurrentPhase");
        var presenter = cat.GetComponent("CatLifeMobileCatPresenter");
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        Vector3 anchor = (Vector3)presenter.GetType().GetField("sessionAnchorPosition", fields).GetValue(presenter);
        Quaternion heading = (Quaternion)presenter.GetType().GetField("sessionAnchorRotation", fields).GetValue(presenter);
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        var owns = coordinator.GetType().GetProperty("BehaviorOwnsAnimator");
        string output = ReviewOutput("session-anchor-walk");
        WriteAnchorDiagnostic(cat, anchor, heading, Path.Combine(output, "before.txt"));
        var side = new GameObject("SessionAnchorWalkAuditCamera").AddComponent<Camera>();
        side.enabled = false; side.orthographic = true; side.orthographicSize = 1.7f;
        side.transform.position = new Vector3(4, 1.3f, 4.5f);
        side.transform.LookAt(new Vector3(0, 1, 4.2f));
        var mesh = new Mesh();
        var feet = FindPawVertices(skin);
        var rows = new StringBuilder("frame,time,phase,clip,x,z,yaw,step,minimum_paw_gap,next_clip,in_transition,current_phase,next_phase\n");
        int frame = 0, capture = 0, ownerErrors = 0;
        float started = Time.time, distance = 0, maxStep = 0, minGap = float.PositiveInfinity;
        bool walked = false;
        Vector3 previous = body.position;
        var probe = new GameObject("SessionAnchorWalkLateProbe").AddComponent<CatTerrainLateProbe>();
        probe.sample = () =>
        {
            Vector3 delta = body.position - previous; delta.y = 0;
            distance += delta.magnitude; maxStep = Mathf.Max(maxStep, delta.magnitude); previous = body.position;
            string clip = string.Join("+", animator.GetCurrentAnimatorClipInfo(0).Select(info => info.clip.name));
            if (clip.Contains("CL_CAT_Walk_v01_loop_60f")) walked = true;
            if ((CatLifeSessionPhase)phase.GetValue(app) != CatLifeSessionPhase.Normal && (bool)owns.GetValue(coordinator)) ownerErrors++;
            float gap = MeasurePawGap(cat, skin, mesh, feet); minGap = Mathf.Min(minGap, gap);
            if (gap < 0)
            {
                var contact = cat.GetComponent("CatFootTerrainContact");
                var legs = (Array)contact.GetType().GetField("legs", fields).GetValue(contact);
                var diagnostic = new StringBuilder($"frame={frame}; bodyY={body.position.y:R}; corrected={contact.GetType().GetProperty("CorrectedFeet").GetValue(contact)}; offset={contact.GetType().GetProperty("BodyOffset").GetValue(contact)}\n");
                foreach (var leg in legs)
                {
                    var type = leg.GetType();
                    var positions = (Vector3[])type.GetField("positions").GetValue(leg);
                    Vector3 goal = (Vector3)type.GetField("goal").GetValue(leg);
                    diagnostic.AppendLine($"correct={type.GetField("correct").GetValue(leg)}; correctionY={goal.y-positions[3].y:R}; goal={goal:R}");
                }
                File.WriteAllText(Path.Combine(output,$"negative-{frame}.txt"),diagnostic.ToString());
                CatLifeExitActionTests.Capture(side,Path.Combine(output,"negative"),frame,800,560);
            }
            string nextClip = string.Join("+", animator.GetNextAnimatorClipInfo(0).Select(info => info.clip.name));
            rows.AppendLine(FormattableString.Invariant($"{frame},{Time.time-started:R},{phase.GetValue(app)},{clip},{body.position.x:R},{body.position.z:R},{body.rotation.eulerAngles.y:R},{delta.magnitude:R},{gap:R},{nextClip},{animator.IsInTransition(0)},{animator.GetCurrentAnimatorStateInfo(0).normalizedTime:R},{animator.GetNextAnimatorStateInfo(0).normalizedTime:R}"));
            if (frame++ % 6 == 0) CatLifeExitActionTests.Capture(side, Path.Combine(output, "side"), capture++, 800, 560);
        };
        GameObject.Find("StartButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        GameObject.Find("SetupStart").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        while ((CatLifeSessionPhase)phase.GetValue(app) == CatLifeSessionPhase.Transition && Time.time-started < 15) yield return null;
        yield return new WaitForSeconds(.3f);
        probe.sample = null;
        Vector3 remaining = anchor - body.position; remaining.y = 0;
        bool failed = (bool)presenter.GetType().GetProperty("SessionPreparationFailed").GetValue(presenter);
        WriteAnchorDiagnostic(cat, anchor, heading, Path.Combine(output, "after.txt"));
        File.WriteAllText(Path.Combine(output,"late-frames.csv"), rows.ToString());
        File.WriteAllText(Path.Combine(output,"result.txt"), FormattableString.Invariant($"frames={frame}; captures={capture}; phase={phase.GetValue(app)}; failed={failed}; walked={walked}; distance={distance:R}; maxStep={maxStep:R}; minGap={minGap:R}; anchorError={remaining.magnitude:R}; headingError={Quaternion.Angle(body.rotation,heading):R}; ownershipErrors={ownerErrors}\nActual Unity frames and existing Setup buttons; seed={observedPosition}; yaw={observedYaw}; portrait={portrait}; scene={scene}. Not Android evidence.\n"));
        Object.Destroy(probe.gameObject); Object.Destroy(side.gameObject); Object.Destroy(mesh);
        Assert.That((CatLifeSessionPhase)phase.GetValue(app), Is.EqualTo(CatLifeSessionPhase.Focus), "Preparation must finish, not silently return Home or wait forever.");
        Assert.That(walked, Is.True);
        Assert.That(distance, Is.GreaterThan(.4f), "Both observed starts require real navigation, not an in-place Walk clip.");
        Assert.That(maxStep, Is.LessThan(.06f), "A root step must fit the existing 1.15m/s physical navigation, not an anchor teleport.");
        Assert.That(remaining.magnitude, Is.LessThan(.03f));
        Assert.That(Quaternion.Angle(body.rotation, heading), Is.LessThan(.5f));
        Assert.That(minGap, Is.GreaterThanOrEqualTo(0));
        Assert.That(ownerErrors, Is.Zero);
        Assert.That(CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key)).records.Count, Is.Zero);
    }

    [UnityTest] public IEnumerator CancellingPreparationWalkStopsItsPathAndOldCallbacks()
    {
        Time.captureFramerate = 60;
        var configured = CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key));
        configured.settings.localBehaviorStatsEnabled = true;
        configured.settings.autoFocusAdaptationSeconds = 30;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(configured));
        yield return BeginCandidateSniff(.55f);
        var cat = GameObject.Find("CatLifeMobileCat");
        var body = cat.GetComponent<Rigidbody>();
        var animator = cat.GetComponent<Animator>();
        body.position = new Vector3(-.001f, body.position.y, 4.077f);
        body.rotation = Quaternion.Euler(0,171.88f,0);
        Physics.SyncTransforms();
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var evaluate = app.GetType().GetMethod("EvaluateAutoFocus");
        var type = evaluate.GetParameters()[0].ParameterType;
        object snapshot = type.GetMethod("CreateDefault").Invoke(null,null);
        type.GetField("focusConfidence").SetValue(snapshot,.82f);
        var band = type.GetField("attentionBand");
        band.SetValue(snapshot,Enum.Parse(band.FieldType,"Stable"));
        evaluate.Invoke(app,new object[]{snapshot,31f});
        var phase = app.GetType().GetProperty("CurrentPhase");
        float started = Time.time;
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_Walk_v01_loop_60f") && Time.time-started<8 &&
            (CatLifeSessionPhase)phase.GetValue(app)==CatLifeSessionPhase.Transition) yield return null;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_Walk_v01_loop_60f"),Is.True,"Cancel must occur during actual preparation walking.");
        Vector3 stopped = body.position;
        GameObject.Find("AutoFocusCancel").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Assert.That(Vector3.Distance(stopped,body.position),Is.LessThan(.0001f));
        int stale = 0;
        for(int frame=0;frame<240;frame++)
        {
            yield return null;
            var state=animator.GetCurrentAnimatorStateInfo(0);
            if ((CatLifeSessionPhase)phase.GetValue(app)!=CatLifeSessionPhase.Normal ||
                state.IsName("CL_CAT_StandToLie_v11_150f") || state.IsName("CL_CAT_ProneBreath_v11_loop_240f")) stale++;
        }
        Vector3 drift=body.position-stopped;drift.y=0;
        Assert.That(stale,Is.Zero);
        Assert.That(drift.magnitude,Is.LessThan(.002f));
        Assert.That(cat.GetComponent<UnityEngine.AI.NavMeshAgent>().hasPath,Is.False);
        Assert.That(CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key)).records.Count,Is.Zero);
        Debug.Log(FormattableString.Invariant($"SESSION_WALK_CANCEL actualWalk=True; observedFrames=240; stale={stale}; drift={drift.magnitude:R}; records=0"));
    }

    static void WriteAnchorDiagnostic(GameObject cat, Vector3 anchor, Quaternion heading, string output)
    {
        var planner = cat.GetComponent("CatDestinationPlanner");
        const BindingFlags methods = BindingFlags.NonPublic | BindingFlags.Instance;
        var rows = new StringBuilder($"Actual focus-point diagnostic; no planner setting changed. Camera aspect={Camera.main.aspect:R}\n");
        var offsets = new[] {new Vector3(-.14f,0,.45f),new Vector3(.14f,0,.45f),new Vector3(-.25f,0,-.33f),new Vector3(.25f,0,-.33f)};
        foreach (var target in new[]{anchor, anchor-heading*Vector3.forward*.6f})
        {
            bool sampled = UnityEngine.AI.NavMesh.SamplePosition(target, out var hit, 2, UnityEngine.AI.NavMesh.AllAreas);
            bool visible = (bool)planner.GetType().GetMethod("IsWholeBodyVisible", methods).Invoke(planner,new object[]{hit.position});
            bool valid = (bool)planner.GetType().GetMethod("IsSessionDestinationValid",methods).Invoke(planner,new object[]{hit.position,cat.transform.position,false});
            rows.AppendLine($"requested={target:R}; sampled={sampled}; projected={hit.position:R}; visible={visible}; valid={valid}; origin={cat.transform.position:R}; camera={Camera.main.transform.position:R}; fov={Camera.main.fieldOfView:R}");
            foreach(var offset in offsets)
            {
                Vector3 foot=hit.position+heading*offset;
                var surfaces=Physics.RaycastAll(foot+Vector3.up*2,Vector3.down,5,~0,QueryTriggerInteraction.Ignore)
                    .Where(item=>!item.transform.IsChildOf(cat.transform)).OrderBy(item=>item.distance);
                rows.AppendLine($"foot={foot:R}; hits="+string.Join(";",surfaces.Select(item=>item.collider.name+"@"+item.point.y.ToString("R"))));
            }
        }
        File.WriteAllText(output, rows.ToString());
    }

    IEnumerator ReviewSessionTakeover(float phase)
    {
        Time.captureFramerate = 60;
        yield return BeginCandidateSniff(phase);
        var cat = GameObject.Find("CatLifeMobileCat");
        var animator = cat.GetComponent<Animator>();
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var driver = (Behaviour)cat.GetComponent("CatBehaviorDriver");
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        var owns = coordinator.GetType().GetProperty("BehaviorOwnsAnimator");
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var session = (CatLifeSessionController)app.GetType().GetField("session", fields).GetValue(app);
        var data = (CatLifeAppData)app.GetType().GetField("data", fields).GetValue(app);
        var side = new GameObject("SessionHandoffAuditCamera").AddComponent<Camera>();
        side.enabled = false; side.orthographic = true; side.orthographicSize = 1.5f;
        side.transform.position = cat.transform.position + new Vector3(4, 1.1f, 0);
        side.transform.LookAt(cat.transform.position + Vector3.up * .8f);
        string output = ReviewOutput("session-" + Mathf.RoundToInt(phase*100));
        int frame = 0, captures = 0, prematureDown = 0, ownershipErrors = 0;
        int stride = Mathf.Approximately(phase, .55f) ? 2 : 6;
        float started = Time.time, minimumGap = float.PositiveInfinity;
        bool requested = false;
        var baked = new Mesh();
        int[][] feet = FindPawVertices(skin);
        var rows = new StringBuilder("frame,time,session,current_clip,phase,next_clip,next_phase,behavior_owner,minimum_paw_gap\n");
        var probe = new GameObject("SitSessionHandoffLateProbe").AddComponent<CatTerrainLateProbe>();
        probe.sample = () =>
        {
            var current = animator.GetCurrentAnimatorStateInfo(0);
            var next = animator.GetNextAnimatorStateInfo(0);
            string clip = string.Join("+", animator.GetCurrentAnimatorClipInfo(0).Select(info => info.clip.name));
            string nextClip = animator.IsInTransition(0) ? string.Join("+", animator.GetNextAnimatorClipInfo(0).Select(info => info.clip.name)) : "";
            bool owner = (bool)owns.GetValue(coordinator);
            if (requested && (owner || driver.enabled)) ownershipErrors++;
            if (animator.IsInTransition(0) && next.IsName("CL_CAT_StandToLie_v11_150f") &&
                current.IsName("CL_CAT_SitSniffStand_v01_280f") && current.normalizedTime < 268f/280)
                prematureDown++;
            float gap = MeasurePawGap(cat, skin, baked, feet);
            minimumGap = Mathf.Min(minimumGap, gap);
            rows.AppendLine(FormattableString.Invariant($"{frame},{Time.time-started:R},{session.Phase},{clip},{current.normalizedTime:R},{nextClip},{next.normalizedTime:R},{owner},{gap:R}"));
            if (frame % stride == 0) CatLifeExitActionTests.Capture(side, Path.Combine(output, "side"), captures++, 800, 560);
            frame++;
        };
        yield return null;
        GameObject.Find("StartButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        GameObject.Find("SetupStart").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        requested = true;
        Assert.That(session.Phase, Is.EqualTo(CatLifeSessionPhase.Transition));
        while (session.Phase == CatLifeSessionPhase.Transition && Time.time-started < 10) yield return null;
        yield return new WaitForSeconds(1);
        probe.sample = null;
        File.WriteAllText(Path.Combine(output, "late-frames.csv"), rows.ToString());
        File.WriteAllText(Path.Combine(output, "result.txt"), FormattableString.Invariant(
            $"requestedPhase={phase:R}; frames={frame}; captureFps={60/stride}; captures={captures}; prematureStandingDown={prematureDown}; ownershipErrors={ownershipErrors}; endPhase={session.Phase}; records={data.records.Count}; minPawGap={minimumGap:R}\n"));
        Object.Destroy(probe.gameObject); Object.Destroy(side.gameObject); Object.Destroy(baked);
        Assert.That(prematureDown, Is.Zero, "StandToLie must not be entered from a still-seated source pose.");
        Assert.That(ownershipErrors, Is.Zero);
        Assert.That(session.Phase, Is.EqualTo(CatLifeSessionPhase.Focus));
        Assert.That(data.records.Count, Is.Zero, "Entering focus must not settle a session.");
        Assert.That(minimumGap, Is.GreaterThanOrEqualTo(0));
    }

    IEnumerator BeginCandidateSniff(float phase)
    {
        yield return LoadControlledScene("Assets/Editor/CatProneReview/RoamSitChainCandidate/CatLifeSitSniffReview.unity");
        yield return new WaitForSeconds(.4f);
        var cat = GameObject.Find("CatLifeMobileCat");
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        var respond = coordinator.GetType().GetMethod("RespondToLandmark");
        respond.Invoke(coordinator, new[] { Enum.Parse(respond.GetParameters()[0].ParameterType, "FishShop") });
        var animator = cat.GetComponent<Animator>();
        float started = Time.time;
        while ((!animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_SitSniffStand_v01_280f") ||
            animator.GetCurrentAnimatorStateInfo(0).normalizedTime < phase) && Time.time-started < 6) yield return null;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_SitSniffStand_v01_280f"), Is.True);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(phase));
    }

    static string ReviewOutput(string name)
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../../Reports/wp03-" + name + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(output);
        Debug.Log("CATLIFE_HANDOFF_REPORT=" + output);
        return output;
    }

    static int[][] FindPawVertices(SkinnedMeshRenderer skin)
    {
        string[] names = { "frontleg2", "R_frontleg2", "backleg2", "R_backleg2" };
        var groups = names.Select(_ => new System.Collections.Generic.List<int>()).ToArray();
        int[] bones = names.Select(name => Array.FindIndex(skin.bones, bone => bone.name == name)).ToArray();
        using (var counts = skin.sharedMesh.GetBonesPerVertex())
        using (var weights = skin.sharedMesh.GetAllBoneWeights())
        {
            int offset = 0;
            for (int vertex = 0; vertex < counts.Length; vertex++)
            {
                for (int f = 0; f < 4; f++)
                {
                    float weight = 0;
                    for (int j = 0; j < counts[vertex]; j++)
                        if (weights[offset+j].boneIndex == bones[f]) weight += weights[offset+j].weight;
                    if (weight >= .99f) groups[f].Add(vertex);
                }
                offset += counts[vertex];
            }
        }
        return groups.Select(group => group.ToArray()).ToArray();
    }

    static float MeasurePawGap(GameObject cat, SkinnedMeshRenderer skin, Mesh mesh, int[][] feet)
    {
        skin.BakeMesh(mesh, false);
        var vertices = mesh.vertices;
        float minimum = float.PositiveInfinity;
        foreach (var group in feet)
        {
            Vector3 sole = group.Select(index => skin.transform.position + skin.transform.rotation * vertices[index]).OrderBy(point => point.y).First();
            var ground = Physics.RaycastAll(sole + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore)
                .Where(hit => !hit.transform.IsChildOf(cat.transform)).OrderBy(hit => hit.distance).First();
            minimum = Mathf.Min(minimum, sole.y - ground.point.y);
        }
        return minimum;
    }

    IEnumerator LoadControlledScene(string path)
    {
        // LoadSceneInPlayMode defers the load. Freeze decisions in sceneLoaded,
        // after Awake but before the first Update can seed an action cooldown.
        freezeAmbient = (scene, mode) =>
        {
            var driver = GameObject.Find("CatLifeMobileCat").GetComponent("CatBehaviorDriver");
            driver.GetType().GetField("nextDecisionTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(driver, float.PositiveInfinity);
        };
        SceneManager.sceneLoaded += freezeAmbient;
        EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        SceneManager.sceneLoaded -= freezeAmbient;
        freezeAmbient = null;
    }
}
#endif

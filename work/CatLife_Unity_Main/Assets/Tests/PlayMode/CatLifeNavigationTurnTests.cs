using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CatLifeNavigationTurnTests
{
    const string Key = "CatLife.Mobile.Data.v1";
    string saved;
    int captureRate;
    [SetUp] public void Preserve()
    {
        saved = PlayerPrefs.GetString(Key, ""); captureRate = Time.captureFramerate;
        var data = new CatLifeAppData();
        data.settings.catAdopted = true; data.settings.aiEnabled = false;
        data.settings.autoFocusAdaptationSeconds = 0;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
    }
    [UnityTearDown] public IEnumerator Restore()
    {
        Time.captureFramerate = captureRate;
        var scene = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("TurnCleanup"));
        yield return SceneManager.UnloadSceneAsync(scene);
        if (saved.Length == 0) PlayerPrefs.DeleteKey(Key); else PlayerPrefs.SetString(Key, saved);
    }
    [UnityTest] public IEnumerator LeftTurnThenWalkAndStand() { yield return Route(270, false); }
    [UnityTest] public IEnumerator RightTurnThenWalkAndStand() { yield return Route(90, false); }
    [UnityTest] public IEnumerator CancelTurnReleasesPoseAndStopsRoot() { yield return Route(270, true); }

    [UnityTest] public IEnumerator PlannerExecutesTheScoredSupportedRoute()
    {
        EditorSceneManager.LoadSceneInPlayMode("Assets/Editor/CatProneReview/NavigationTurnCandidate/CatLifeNavigationTurnReview.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        var cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        var nav = cat.GetComponent("CatNavigationAgent");
        nav.GetType().GetMethod("StopSoft").Invoke(nav, null);
        yield return new WaitForSeconds(.5f);
        Camera.main.aspect = 1080f / 2424f;
        var planner = cat.GetComponent("CatDestinationPlanner");
        var type = planner.GetType();
        var method = Array.Find(type.GetMethods(), m => m.Name == "TryPlanNext" && m.GetParameters().Length == 4);
        var parameters = method.GetParameters();
        var snapshot = parameters[0].ParameterType.GetMethod("CreateDefault").Invoke(null, null);
        var state = Enum.Parse(parameters[1].ParameterType, "Roam");
        var args = new object[] { snapshot, state, cat.transform.position, Vector3.zero };
        Assert.That((bool)method.Invoke(planner, args), Is.True);
        var landing = (Vector3)args[3];
        var selected = (NavMeshPath)type.GetField("plannedRoamingPath", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(planner);
        Assert.That(selected, Is.Not.Null);
        Assert.That(Vector3.Distance(selected.corners[selected.corners.Length-1], landing), Is.LessThan(.001f));
        var score = type.GetMethod("ScoreRoamingRoute", BindingFlags.NonPublic | BindingFlags.Static);
        var direction = selected.corners[1] - selected.corners[0]; direction.y = 0;
        float forward = (float)score.Invoke(null, new object[] { selected, direction });
        float reverse = (float)score.Invoke(null, new object[] { selected, -direction });
        Assert.That(forward, Is.GreaterThan(reverse + 4), "Score must include the turn needed for the actual first path segment.");
        Assert.That((bool)nav.GetType().GetMethod("TryMoveTo").Invoke(nav, new object[] { landing }), Is.True);
        var agent = cat.GetComponent<NavMeshAgent>();
        Assert.That(Vector3.Distance(agent.pathEndPosition, landing), Is.LessThan(.001f), "Execution silently replaced the scored landing.");
        Assert.That(type.GetField("plannedRoamingPath", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(planner), Is.Null);
    }

    [UnityTest] public IEnumerator AutonomousRoamingMakesForwardProgress()
    {
        var randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(32);
        EditorSceneManager.LoadSceneInPlayMode("Assets/Editor/CatProneReview/NavigationTurnCandidate/CatLifeNavigationTurnReview.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        Time.captureFramerate = 60;
        var cat = GameObject.Find("CatLifeMobileCat");
        var nav = cat.GetComponent("CatNavigationAgent");
        var body = cat.GetComponent<Rigidbody>();
        var agent = cat.GetComponent<NavMeshAgent>();
        var animator = cat.GetComponent<Animator>();
        var origin = body.position;
        Camera.main.aspect = 1080f / 2424f;
        float maximumDistance = 0;
        int turnFrames = 0, walkFrames = 0;
        var rows = new StringBuilder("time,x,z,yaw,turn,path,destinationX,destinationZ,desiredX,desiredZ,corners,clip\n");
        var probe = new GameObject("Autonomous late capture").AddComponent<CatTerrainLateProbe>();
        probe.sample = () =>
        {
            maximumDistance = Mathf.Max(maximumDistance, Vector2.Distance(new Vector2(origin.x,origin.z),new Vector2(body.position.x,body.position.z)));
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            if (clips.Length > 0 && clips[0].clip.name.Contains("TurnLocal")) turnFrames++;
            if (clips.Length > 0 && clips[0].clip.name.Contains("Walk")) walkFrames++;
            rows.AppendLine(FormattableString.Invariant($"{Time.time:F3},{body.position.x:F5},{body.position.z:F5},{body.rotation.eulerAngles.y:F3},{nav.GetType().GetProperty("IsTurning").GetValue(nav)},{nav.GetType().GetProperty("IsPathInProgress").GetValue(nav)},{agent.destination.x:F5},{agent.destination.z:F5},{agent.desiredVelocity.x:F5},{agent.desiredVelocity.z:F5},{agent.path.corners.Length},{(clips.Length == 0 ? "none" : clips[0].clip.name)}"));
        };
        yield return new WaitForSeconds(90);
        probe.sample = null;
        UnityEngine.Object.Destroy(probe.gameObject);
        UnityEngine.Random.state = randomState;
        string output = Path.GetFullPath("../../Reports/wp03-autonomous-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".csv");
        File.WriteAllText(output,rows.ToString());
        Debug.Log("AUTONOMOUS_REVIEW " + output + " maximumDistance=" + maximumDistance);
        Assert.That(maximumDistance, Is.GreaterThan(.5f), "Real ambient driver must move beyond repeated turns in the starting patch.");
        Assert.That(turnFrames, Is.LessThan(walkFrames * 3), "Roaming must not spend most movement time turning between tiny routes.");
        Assert.That(walkFrames, Is.GreaterThan(600), "Do not pass the turn ratio by leaving the cat idle with no selectable route.");
    }

    IEnumerator Route(float yaw, bool cancel)
    {
        EditorSceneManager.LoadSceneInPlayMode("Assets/Editor/CatProneReview/NavigationTurnCandidate/CatLifeNavigationTurnReview.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        var cat = GameObject.Find("CatLifeMobileCat");
        ((Behaviour)cat.GetComponent("CatBehaviorDriver")).enabled = false;
        var nav = cat.GetComponent("CatNavigationAgent"); var type = nav.GetType();
        var animation = cat.GetComponent("CatAnimationController"); var animType = animation.GetType();
        var animator = cat.GetComponent<Animator>(); var body = cat.GetComponent<Rigidbody>();
        var agent = cat.GetComponent<NavMeshAgent>();
        type.GetMethod("StopSoft").Invoke(nav, null);
        yield return new WaitForSeconds(1);
        Time.captureFramerate = 60;
        var origin = new Vector3(0, .278f, 4.5f);
        Assert.That(NavMesh.SamplePosition(origin, out var hit, 1, NavMesh.AllAreas), Is.True);
        agent.Warp(hit.position); body.position = origin; cat.transform.position = origin;
        body.rotation = Quaternion.Euler(0, yaw, 0); cat.transform.rotation = body.rotation;
        body.linearVelocity = Vector3.zero; Physics.SyncTransforms();
        animType.GetMethod("ForceLocomotion").Invoke(animation, new object[] { false });
        yield return new WaitForSeconds(.2f);
        Assert.That((bool)type.GetMethod("TryMoveToSessionPoint").Invoke(nav, new object[] { new Vector3(0,.278f,4), null }), Is.True);
        string output = Path.GetFullPath("../../Reports/wp03-navigation-turn-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + yaw + (cancel ? "-cancel" : ""));
        Directory.CreateDirectory(output);
        var camera = new GameObject("Turn review camera").AddComponent<Camera>();
        camera.CopyFrom(Camera.main); camera.enabled = false; camera.rect = new Rect(0,0,1,1);
        camera.fieldOfView = 40;
        camera.transform.position = new Vector3(3,1.6f,7); camera.transform.LookAt(new Vector3(0,.65f,4.2f));
        var target = new RenderTexture(720,540,24); camera.targetTexture = target;
        var pixels = new Texture2D(720,540,TextureFormat.RGB24,false);
        var rows = new StringBuilder("frame,turn,path,x,y,z,yaw,clip,phase,speed\n");
        bool sawTurn=false, sawTurnClip=false, sawWalk=false; int turnFrames=0, frame=0;
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>(); var mesh=new Mesh();
        var feet=(int[][])typeof(CatLifeRoamUprightTests).GetMethod("FindPawVertices",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{skin});
        var gapMethod=typeof(CatLifeRoamUprightTests).GetMethod("MeasurePawGap",BindingFlags.NonPublic|BindingFlags.Static);
        float minGap=float.PositiveInfinity;
        float maxSlide=0, previousPhase=-1; int segment=0;
        var anchors=new Vector3[4][]; var anchorKeys=new int[]{-1,-1,-1,-1};
        var contacts=new StringBuilder("frame,paw_gap,maximum_support_slide\n");
        var probe=new GameObject("Turn late contact capture").AddComponent<CatTerrainLateProbe>();
        probe.sample=()=>
        {
            float gap=(float)gapMethod.Invoke(null,new object[]{cat,skin,mesh,feet});
            minGap=Mathf.Min(minGap,gap);
            var state=animator.GetCurrentAnimatorStateInfo(0);
            bool left=state.IsName("CL_CAT_TurnLocalLeft"), right=state.IsName("CL_CAT_TurnLocalRight");
            if ((left||right) && !animator.IsInTransition(0))
            {
                float phase=Mathf.Clamp01(state.normalizedTime);
                if (phase<previousPhase) segment++;
                previousPhase=phase;
                var vertices=mesh.vertices;
                int cycle=Mathf.Min(1,Mathf.FloorToInt(phase*2)); float u=phase*2-cycle;
                for(int f=0;f<4;f++)
                {
                    int order=left ? new[]{1,3,0,2}[f] : new[]{3,1,2,0}[f];
                    float begin=.08f+order*.21f,end=begin+.19f;
                    if(u>begin && u<end) continue;
                    int key=segment*4+cycle*2+(u>=end ? 1 : 0);
                    if(anchorKeys[f]!=key)
                    {
                        anchorKeys[f]=key; anchors[f]=new Vector3[feet[f].Length];
                        for(int j=0;j<feet[f].Length;j++) anchors[f][j]=skin.transform.position+skin.transform.rotation*vertices[feet[f][j]];
                    }
                    for(int j=0;j<feet[f].Length;j++)
                    {
                        Vector3 delta=skin.transform.position+skin.transform.rotation*vertices[feet[f][j]]-anchors[f][j]; delta.y=0;
                        maxSlide=Mathf.Max(maxSlide,delta.magnitude);
                    }
                }
            }
            contacts.AppendLine(FormattableString.Invariant($"{frame},{gap:R},{maxSlide:R}"));
            camera.Render(); var previous=RenderTexture.active; RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,720,540),0,0); pixels.Apply(); RenderTexture.active=previous;
            File.WriteAllBytes(Path.Combine(output,$"frame-{frame:D5}.png"),pixels.EncodeToPNG());
        };
        float started=Time.time; Vector3 walkStart=body.position; float walkDistance=0;
        while (Time.time-started < 20)
        {
            bool path=(bool)type.GetProperty("IsPathInProgress").GetValue(nav);
            bool turning=(bool)type.GetProperty("IsTurning").GetValue(nav);
            float speed=(float)type.GetProperty("Speed01").GetValue(nav);
            animType.GetMethod("Tick").Invoke(animation,new object[]{speed,path,false,0f});
            yield return null;
            var clips=animator.GetCurrentAnimatorClipInfo(0);
            string clip=clips.Length==0 ? "none" : clips[0].clip.name;
            if (turning) { sawTurn=true; turnFrames++; }
            sawTurnClip |= turning && clip.Contains("TurnLocal");
            if (!turning && clip.Contains("Walk") && speed>.05f)
            { sawWalk=true; walkDistance+=Vector3.Distance(walkStart,body.position); }
            walkStart=body.position;
            rows.AppendLine(FormattableString.Invariant($"{frame},{turning},{path},{body.position.x:F5},{body.position.y:F5},{body.position.z:F5},{body.rotation.eulerAngles.y:F3},{clip},{animator.GetCurrentAnimatorStateInfo(0).normalizedTime:F4},{animator.speed:F4}"));
            frame++;
            if (cancel && turnFrames==45)
            {
                type.GetMethod("StopSoft").Invoke(nav,null);
                animType.GetMethod("ForceLocomotion").Invoke(animation,new object[]{false});
                Vector3 stopped=body.position; Quaternion heading=body.rotation;
                yield return new WaitForSeconds(.6f);
                Assert.That((bool)animType.GetProperty("NavigationOwnsPose").GetValue(animation),Is.False);
                Assert.That(Vector2.Distance(new Vector2(stopped.x,stopped.z),new Vector2(body.position.x,body.position.z)),Is.LessThan(.005f));
                Assert.That(Quaternion.Angle(heading,body.rotation),Is.LessThan(1));
                break;
            }
            if (sawTurn && !path && !turning) break;
        }
        File.WriteAllText(Path.Combine(output,"frames.csv"),rows.ToString());
        probe.sample=null; UnityEngine.Object.Destroy(probe.gameObject); UnityEngine.Object.Destroy(mesh);
        File.WriteAllText(Path.Combine(output,"foot-contact.csv"),contacts.ToString());
        File.WriteAllText(Path.Combine(output,"contact.txt"),FormattableString.Invariant($"minimumPawGap={minGap:R}; maximumTurnSupportSlide={maxSlide:R}; walkDistance={walkDistance:R}; capturedEveryLateFrame=true\n"));
        UnityEngine.Object.Destroy(camera.gameObject); target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
        Assert.That(sawTurn,Is.True);
        Assert.That(sawTurnClip,Is.True,"Navigation must play the real turn clip, not a frozen Walk/Stand.");
        Assert.That(minGap,Is.GreaterThanOrEqualTo(-.001f),"Town surface penetration exceeds 1mm.");
        Assert.That(maxSlide,Is.LessThanOrEqualTo(.005f),"Planted turn paw drifts over 5mm.");
        if (!cancel)
        {
            Assert.That(sawWalk,Is.True);
            Assert.That(walkDistance,Is.GreaterThan(.25f));
            Assert.That((bool)type.GetProperty("IsPathInProgress").GetValue(nav),Is.False,"Route did not finish");
            animType.GetMethod("ForceLocomotion").Invoke(animation,new object[]{false});
            yield return new WaitForSeconds(.3f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"),Is.True);
        }
        Debug.Log("NAVIGATION_TURN_REVIEW " + output);
    }
}

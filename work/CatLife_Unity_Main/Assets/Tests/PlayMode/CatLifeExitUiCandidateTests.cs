using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

// The real App wiring, isolated serialized UI candidate and candidate model in memory.
public sealed class CatLifeExitUiCandidateTests
{
    const string Key = "CatLife.Mobile.Data.v1";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    string saved;
    bool hadData;
    RenderTexture target;
    [SetUp] public void Preserve() { hadData = PlayerPrefs.HasKey(Key); saved = PlayerPrefs.GetString(Key); }
    [UnityTearDown] public IEnumerator Restore()
    {
        var scene = SceneManager.GetSceneByName("CatLifeMobile");
        if (scene.isLoaded)
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("ExitUiCleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
        if (hadData) PlayerPrefs.SetString(Key, saved); else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    [UnityTest]
    public IEnumerator PointerDrivesTheRealPresenterAndOnlyConsumesOneIntervention()
    {
#if UNITY_EDITOR
        var seedData = new CatLifeAppData();
        seedData.settings.catAdopted = true;
        seedData.settings.aiEnabled = false;
        var seed = new CatLifeSessionController(seedData);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        seed.BeginTransition(900, now); seed.EnterFocus(now);
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(seedData));
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(4);
        var app = UnityEngine.Object.FindObjectsByType<MonoBehaviour>()
            .Single(c => c.GetType().Name == "CatLifeMobileApp");
        var session = (CatLifeSessionController)app.GetType().GetField("session", Private).GetValue(app);
        var data = (CatLifeAppData)app.GetType().GetField("data", Private).GetValue(app);
        long deadline = data.activeSession.targetEndAt;
        var root = (GameObject)app.GetType().GetField("viewRoot", Private).GetValue(app);
        var views = ((System.Collections.Generic.Dictionary<string, GameObject>)app.GetType().GetField("views", Private).GetValue(app))
            .ToDictionary(pair => pair.Key, pair => pair.Value.transform);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = .5f;
        root.GetComponent<CanvasScaler>().enabled = false;
        canvas.scaleFactor = Mathf.Sqrt(2424f / 2400f);
        var safe = views["SafeArea"];
        ((MonoBehaviour)safe.GetComponent("CatLifeSafeArea")).enabled = false;
        ((RectTransform)safe).anchorMin = Vector2.zero; ((RectTransform)safe).anchorMax = Vector2.one;
        target = new RenderTexture(1080, 2424, 24);
        Camera.main.targetTexture = target;
        Camera.main.aspect = 1080f / 2424f;
        Canvas.ForceUpdateCanvases();
        var cat = GameObject.Find("CatLifeMobileCat");
        var presenter = cat.GetComponent("CatLifeMobileCatPresenter");
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var animator = cat.GetComponent<Animator>();
        const string candidatePath = "Assets/Editor/CatProneReview/ExitCandidate/RuntimeIntegration/PF_CL_CAT_ExitIntegration_candidate.prefab";
        var candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(candidatePath);
        var candidateSkin = candidate.GetComponentInChildren<SkinnedMeshRenderer>();
        var bones = cat.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
        skin.bones = candidateSkin.bones.Select(b => bones[b.name]).ToArray();
        skin.sharedMesh = candidateSkin.sharedMesh;
        for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) skin.SetBlendShapeWeight(i, 0);
        animator.runtimeAnimatorController = candidate.GetComponent<Animator>().runtimeAnimatorController;
        animator.speed = 0;
        animator.Play("CL_CAT_ProneExitHold_v01_loop_48f", 0, 0); animator.Update(0);
        var probe = new GameObject("Exit UI late capture").AddComponent<CatTerrainLateProbe>();
        bool requested = true;
        Vector3 pad = Vector3.zero;
        var pawPoints = new Vector3[2][];
        string[] footNames = { "frontleg2", "R_frontleg2" };
        var mesh = new Mesh();
        probe.sample = () =>
        {
            if (!requested) return;
            requested = false;
            skin.BakeMesh(mesh, false);
            var weights = skin.sharedMesh.boneWeights;
            for (int foot = 0; foot < 2; foot++)
            {
                var points = mesh.vertices.Where((v, i) => weights[i].weight0 > .99f &&
                    skin.bones[weights[i].boneIndex0].name == footNames[foot])
                    .Select(v => skin.transform.position + skin.transform.rotation * v).ToArray();
                var bone = bones[footNames[foot]];
                var localPoints = points.Select(bone.InverseTransformPoint).ToArray();
                var extremes = new System.Collections.Generic.List<Vector3>();
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && y == 0 && z == 0) continue;
                    var direction = new Vector3(x, y, z);
                    extremes.Add(localPoints.OrderByDescending(point => Vector3.Dot(point, direction)).First());
                }
                pawPoints[foot] = extremes.Distinct().ToArray();
                if (foot != 0) continue;
                float bottom = points.Min(v => v.y);
                var sole = points.Where(v => v.y < bottom + .012f).ToArray();
                pad = sole.Aggregate(Vector3.zero, (a, b) => a + b) / sole.Length;
            }
        };
        while (requested) yield return null;
        Vector2 screenPad = Camera.main.WorldToScreenPoint(pad);
        Type.GetType("CatLifeExitActionCandidate, Assembly-CSharp-Editor").GetMethod("SaveUiContactCandidate")
            .Invoke(null, new object[] { cat.transform.InverseTransformPoint(pad), pawPoints[0], pawPoints[1],
                UnityEditor.AnimationUtility.CalculateTransformPath(bones[footNames[0]], cat.transform),
                UnityEditor.AnimationUtility.CalculateTransformPath(bones[footNames[1]], cat.transform) });
        root.SetActive(false);
        UnityEngine.Object.Destroy(root);
        root = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Editor/CatProneReview/ExitCandidate/PF_CL_UI_ExitCandidate.prefab"));
        app.GetType().GetField("viewRoot", Private).SetValue(app, root);
        ((System.Collections.Generic.Dictionary<string, GameObject>)app.GetType().GetField("views", Private).GetValue(app)).Clear();
        app.GetType().GetMethod("BindView", Private).Invoke(app, null);
        views = ((System.Collections.Generic.Dictionary<string, GameObject>)app.GetType().GetField("views", Private).GetValue(app))
            .ToDictionary(pair => pair.Key, pair => pair.Value.transform);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main; canvas.planeDistance = .5f;
        root.GetComponent<CanvasScaler>().enabled = false; canvas.scaleFactor = Mathf.Sqrt(2424f / 2400f);
        safe = views["SafeArea"];
        ((MonoBehaviour)safe.GetComponent("CatLifeSafeArea")).enabled = false;
        ((RectTransform)safe).anchorMin = Vector2.zero; ((RectTransform)safe).anchorMax = Vector2.one;
        app.GetType().GetMethod("ShowFocus", Private).Invoke(app, new object[] { true });
        var track = (RectTransform)views["SwipeTrack"];
        var parent = (RectTransform)track.parent;
        Assert.That(track.GetComponent("CatLifePawContactView"), Is.Not.Null);
        presenter.GetType().GetMethod("ShowPhase", new[] { typeof(CatLifeSessionPhase) })
            .Invoke(presenter, new object[] { CatLifeSessionPhase.Focus });
        yield return new WaitForSeconds(.3f);
        float width = track.rect.width;
        Assert.That(track.GetComponent<Image>().material.GetVector("_Paw0").z, Is.GreaterThan(0));
        var swipe = views["SwipeTrack"].GetComponent("CatLifeSwipeToEnd");
        int consumed = 0;
        swipe.GetType().GetEvent("InterventionStarted").AddEventHandler(swipe, (Action)(() =>
        {
            Assert.That(data.activeSession.exitInterventionShown, Is.True);
            consumed++;
        }));
        var eventData = new PointerEventData(EventSystem.current) { pointerId = 41,
            pointerPressRaycast = new RaycastResult { module = root.GetComponent<GraphicRaycaster>() } };
        Canvas.ForceUpdateCanvases();
        Vector2 start = RectTransformUtility.WorldToScreenPoint(Camera.main, views["SwipeHandle"].position);
        Vector2 delta = RectTransformUtility.WorldToScreenPoint(Camera.main, track.TransformPoint(Vector3.right * width * .84f))
            - RectTransformUtility.WorldToScreenPoint(Camera.main, track.position);
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp04-ui-paw-points-20261017"));
        string tag = "before";
        bool capture = true;
        int frame = 0;
        int captureWidth = 1080, captureHeight = 2424;
        float actualContactError = float.NaN;
        probe.sample = () =>
        {
            if (!capture) return;
            capture = false;
            if (tag == "holding")
            {
                // Test-only measurement after terrain correction, not the runtime mask implementation.
                skin.BakeMesh(mesh, false);
                var weights = skin.sharedMesh.boneWeights;
                var points = mesh.vertices.Where((v, i) => weights[i].weight0 > .99f &&
                    skin.bones[weights[i].boneIndex0].name == "frontleg2")
                    .Select(v => skin.transform.position + skin.transform.rotation * v).ToArray();
                float bottom = points.Min(v => v.y);
                var sole = points.Where(v => v.y < bottom + .012f).ToArray();
                Vector2 actualPad = Camera.main.WorldToScreenPoint(sole.Aggregate(Vector3.zero, (a, b) => a + b) / sole.Length);
                actualContactError = Vector2.Distance(actualPad, RectTransformUtility.WorldToScreenPoint(Camera.main, views["SwipeHandle"].position));
            }
            CatLifeExitActionTests.Capture(Camera.main, Path.Combine(output, tag), frame++, captureWidth, captureHeight);
        };
        while (capture) yield return null;
        eventData.position = start;
        ExecuteEvents.Execute(track.gameObject, eventData, ExecuteEvents.pointerDownHandler);
        for (int i = 1; i <= 8; i++)
        {
            eventData.position = start + delta * (.35f * i / 8);
            ExecuteEvents.Execute(track.gameObject, eventData, ExecuteEvents.dragHandler);
            tag = "reaching"; capture = true; while (capture) yield return null;
        }
        // Cross the contact boundary even if coordinate conversion rounds the last point down.
        eventData.position = start + delta * .5f;
        ExecuteEvents.Execute(track.gameObject, eventData, ExecuteEvents.dragHandler);
        Assert.That(consumed, Is.EqualTo(1));
        yield return new WaitForSecondsRealtime(.45f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_ProneExitHold_v01_loop_48f"), Is.True);
        coordinator.GetType().GetMethod("NudgeCat").Invoke(coordinator, null);
        tag = "holding"; capture = true; while (capture) yield return null;
        Assert.That(actualContactError, Is.LessThan(2f), "The actual ground-corrected paw must meet the handle, not just the saved calibration point.");
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_ProneExitHold_v01_loop_48f"), Is.True);
        Assert.That(data.activeSession.targetEndAt, Is.EqualTo(deadline));
        Vector2 contactHandle = RectTransformUtility.WorldToScreenPoint(Camera.main, views["SwipeHandle"].position);
        Assert.That(Vector2.Distance(contactHandle, screenPad), Is.LessThan(1f), "The 35% handle must meet the projected paw pad.");
        Assert.That(CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key)).activeSession.exitInterventionShown, Is.True);
        yield return new WaitForSecondsRealtime(.85f);
        Assert.That((CatLifeExitGesturePhase)swipe.GetType().GetProperty("GesturePhase").GetValue(swipe), Is.EqualTo(CatLifeExitGesturePhase.Released));
        Assert.That((float)swipe.GetType().GetProperty("Progress").GetValue(swipe), Is.EqualTo(.35f).Within(.001));
        tag = "released"; capture = true; while (capture) yield return null;
        eventData.position = start + delta * .86f;
        ExecuteEvents.Execute(track.gameObject, eventData, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(track.gameObject, eventData, ExecuteEvents.pointerUpHandler);
        Assert.That(views["ExitConfirm"].gameObject.activeSelf, Is.True);
        yield return new WaitForSeconds(.6f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_ProneBreath_v11_loop_240f"), Is.True);
        tag = "confirm"; capture = true; while (capture) yield return null;
        views["InterruptCancel"].GetComponent<Button>().onClick.Invoke();
        Assert.That(session.Phase, Is.EqualTo(CatLifeSessionPhase.Focus));
        Assert.That(data.records.Count, Is.Zero);
        eventData.position = start;
        ExecuteEvents.Execute(track.gameObject, eventData, ExecuteEvents.pointerDownHandler);
        eventData.position = start + delta * .75f;
        ExecuteEvents.Execute(track.gameObject, eventData, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(track.gameObject, eventData, ExecuteEvents.pointerUpHandler);
        Assert.That(consumed, Is.EqualTo(1));
        Assert.That(views["ExitConfirm"].gameObject.activeSelf, Is.True);
        views["InterruptCancel"].GetComponent<Button>().onClick.Invoke();
        var oldTarget = target;
        target = new RenderTexture(720, 1280, 24);
        Camera.main.targetTexture = target; Camera.main.aspect = 720f / 1280;
        oldTarget.Release(); UnityEngine.Object.Destroy(oldTarget);
        canvas.scaleFactor = Mathf.Sqrt((720f / 1080) * (1280f / 2400));
        captureWidth = 720; captureHeight = 1280;
        yield return new WaitForSeconds(.1f);
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4]; track.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            Vector2 point = Camera.main.WorldToScreenPoint(corner);
            Assert.That(point.x, Is.InRange(0f, 720f)); Assert.That(point.y, Is.InRange(0f, 1280f));
        }
        tag = "wide"; capture = true; while (capture) yield return null;
        Debug.Log($"EXIT_UI_CANDIDATE consumed={consumed} deadlineUnchanged={data.activeSession.targetEndAt == deadline} actualContactErrorPx={actualContactError:R} pawPoints={pawPoints[0].Length}+{pawPoints[1].Length} wideTrackInside=true");
        probe.sample = null;
        UnityEngine.Object.Destroy(probe.gameObject); UnityEngine.Object.Destroy(mesh);
#else
        yield break;
#endif
    }
}

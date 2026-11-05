using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// D09: bone movement alone cannot demonstrate that the rendered paw moves.
public sealed class CatLifeVisiblePawTests
{
    const string Key = "CatLife.Mobile.Data.v1";
    string saved;
    bool hadData;
    Mesh baked;

    [SetUp] public void PreserveData()
    {
        hadData = PlayerPrefs.HasKey(Key);
        saved = PlayerPrefs.GetString(Key);
    }

    [TearDown] public void RestoreData()
    {
        if (hadData) PlayerPrefs.SetString(Key, saved); else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
        if (baked != null) UnityEngine.Object.Destroy(baked);
    }

    [UnityTest] public IEnumerator SettledRewardHasGroundedSupportPaws()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        var session = new CatLifeSessionController(data);
        session.BeginTransition(60, now - 59);
        session.EnterFocus(now - 59);
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(8);
        Assert.That(GameObject.Find("RewardCard"), Is.Not.Null);
        var cat = GameObject.Find("CatLifeMobileCat");
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var animator = cat.GetComponent<Animator>();
        string[] names = { "frontleg2", "R_frontleg2", "backleg2", "R_backleg2" };
        var points = new List<int>[4];
        using (var counts = skin.sharedMesh.GetBonesPerVertex())
        using (var weights = skin.sharedMesh.GetAllBoneWeights())
        {
            for (int f = 0; f < 4; f++)
            {
                points[f] = new List<int>();
                int bone = Array.FindIndex(skin.bones, b => b.name == names[f]);
                int offset = 0;
                for (int v = 0; v < counts.Length; v++)
                {
                    float influence = 0;
                    for (int j = 0; j < counts[v]; j++)
                        if (weights[offset + j].boneIndex == bone) influence += weights[offset + j].weight;
                    if (influence >= .99f) points[f].Add(v);
                    offset += counts[v];
                }
                Assert.That(points[f].Count, Is.GreaterThan(0));
            }
        }
        baked = new Mesh();
        var probe = new GameObject("Reward support probe").AddComponent<CatTerrainLateProbe>();
        float maxSupportGap = 0;
        float lowestGap = float.PositiveInfinity, highestGap = float.NegativeInfinity;
        for (int frame = 0; frame < 20; frame++)
        {
            bool sampled = false;
            probe.sample = () =>
            {
                skin.BakeMesh(baked, false);
                var vertices = baked.vertices;
                float nearestSupport = float.PositiveInfinity;
                for (int f = 0; f < 4; f++)
                {
                    Vector3 sole = new Vector3(0, float.PositiveInfinity, 0);
                    foreach (int v in points[f])
                    {
                        Vector3 world = skin.transform.position + skin.transform.rotation * vertices[v];
                        if (world.y < sole.y) sole = world;
                    }
                    float distance = float.PositiveInfinity, floor = 0;
                    string surface = "missing";
                    foreach (var hit in Physics.RaycastAll(sole + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore))
                        if (!hit.transform.IsChildOf(cat.transform) && hit.distance < distance)
                        { distance = hit.distance; floor = hit.point.y; surface = hit.collider.name; }
                    Assert.That(float.IsPositiveInfinity(distance), Is.False);
                    float gap = sole.y - floor;
                    lowestGap = Mathf.Min(lowestGap, gap);
                    highestGap = Mathf.Max(highestGap, gap);
                    nearestSupport = Mathf.Min(nearestSupport, Mathf.Abs(gap));
                    Debug.Log($"REWARD_SUPPORT frame={frame}; foot={names[f]}; gap={gap:F5}; floor={floor:F5}; rootY={cat.transform.position.y:F5}; surface={surface}; clip={animator.GetCurrentAnimatorClipInfo(0)[0].clip.name}");
                }
                maxSupportGap = Mathf.Max(maxSupportGap, nearestSupport);
                if (frame == 0)
                {
                    var detail = new GameObject("Reward contact side camera").AddComponent<Camera>();
                    detail.CopyFrom(Camera.main);
                    detail.enabled = false;
                    detail.rect = new Rect(0, 0, 1, 1);
                    detail.orthographic = true;
                    detail.orthographicSize = 1.1f;
                    detail.transform.position = cat.transform.position + cat.transform.right * 4 + Vector3.up * .8f;
                    detail.transform.LookAt(cat.transform.position + Vector3.up * .45f);
                    Capture(detail, 0, "reward-support-frames", 1000, 700);
                    UnityEngine.Object.Destroy(detail.gameObject);
                    Debug.Log($"REWARD_RENDER shader={skin.sharedMaterial.shader.name}; cast={skin.shadowCastingMode}; receive={skin.receiveShadows}");
                    foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                        Debug.Log($"REWARD_LIGHT name={light.name}; shadows={light.shadows}; bias={light.shadowBias}; normalBias={light.shadowNormalBias}");
                }
                sampled = true;
            };
            while (!sampled) yield return null;
            probe.sample = null;
            yield return new WaitForSeconds(.1f);
        }
        UnityEngine.Object.Destroy(probe.gameObject);
        Debug.Log($"REWARD_GAP_RANGE min={lowestGap:F5}; max={highestGap:F5}; nearestSupportMax={maxSupportGap:F5}");
        Assert.That(maxSupportGap, Is.LessThanOrEqualTo(.025f), "The settled reward cat has no paw within 2.5 cm of the ground.");
        Assert.That(lowestGap, Is.GreaterThanOrEqualTo(-.001f), "A settled support paw penetrates the road by more than 1mm.");
        Assert.That(highestGap, Is.LessThanOrEqualTo(.003f), "A settled support paw floats more than 3mm above the road.");
    }

    [UnityTest] public IEnumerator FullRewardCardKeepsWavingPawAboveItsTopEdge()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        var session = new CatLifeSessionController(data);
        session.BeginTransition(60, now - 59);
        session.EnterFocus(now - 59);
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(5);
        var card = GameObject.Find("RewardCard").GetComponent<RectTransform>();
        Assert.That(card.gameObject.activeInHierarchy, Is.True);
        var camera = Camera.main;
        var cat = GameObject.Find("CatLifeMobileCat");
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var paw = Array.Find(skin.bones, bone => bone.name == "frontleg2");
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        Assert.That((bool)coordinator.GetType().GetMethod("PlayAiReaction").Invoke(coordinator, new object[] { "paw_wave" }), Is.True);
        for (int frame = 0; frame < 7; frame++)
        {
            yield return new WaitForSeconds(.4f);
            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            float cardTop = corners[1].y / Screen.height;
            Vector3 pawViewport = camera.WorldToViewportPoint(paw.position);
            float pawScreenY = camera.rect.y + pawViewport.y * camera.rect.height;
            Debug.Log($"REWARD_CLEARANCE frame={frame}; pawY={pawScreenY:F4}; cardTop={cardTop:F4}");
            Assert.That(pawScreenY, Is.GreaterThan(cardTop), "The full result card covers the waving paw.");
        }
    }


    [UnityTest] public IEnumerator RewardWaveMovesSkinnedPawInCameraSpace()
    {
        PlayerPrefs.DeleteKey(Key);
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        var cat = GameObject.Find("CatLifeMobileCat");
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Reward });
        var director = Camera.main.GetComponent("CatLifeCameraDirector");
        Assert.That(director, Is.Not.Null);
        director.GetType().GetMethod("Show").Invoke(director, new object[] { CatLifeSessionPhase.Reward, true });
        yield return new WaitForSeconds(2.3f);
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        int pawBone = Array.FindIndex(skin.bones, bone => bone.name == "frontleg2");
        Assert.That(pawBone, Is.GreaterThanOrEqualTo(0));
        var points = new List<int>();
        using (var counts = skin.sharedMesh.GetBonesPerVertex())
        using (var weights = skin.sharedMesh.GetAllBoneWeights())
        {
            int offset = 0;
            for (int vertex = 0; vertex < counts.Length; vertex++)
            {
                float influence = 0;
                for (int j = 0; j < counts[vertex]; j++)
                    if (weights[offset + j].boneIndex == pawBone) influence += weights[offset + j].weight;
                if (influence >= .5f) points.Add(vertex);
                offset += counts[vertex];
            }
        }
        Assert.That(points.Count, Is.GreaterThan(0), "No rendered vertices follow the waving paw.");
        baked = new Mesh();
        var camera = Camera.main;
        Assert.That(camera, Is.Not.Null);
        Vector3 initial = PawCenter(skin, points);
        Debug.Log($"PAW_SPACE scale={skin.transform.lossyScale}; meshCenter={initial}; bone={skin.bones[pawBone].position}; rendererBounds={skin.bounds}");
        Assert.That(Vector3.Distance(initial, skin.bones[pawBone].position), Is.LessThan(.3f), "The baked paw must be close to its actual paw bone before measuring motion.");
        Vector3 screenStart = camera.WorldToViewportPoint(initial);
        float maxWorld = 0, maxScreen = 0;
        Assert.That((bool)coordinator.GetType().GetMethod("PlayAiReaction").Invoke(coordinator, new object[] { "paw_wave" }), Is.True);
        float end = Time.time + 2.8f;
        float nextFrame = Time.time;
        int frame = 0;
        while (Time.time < end)
        {
            yield return null;
            Vector3 current = PawCenter(skin, points);
            var screen = camera.WorldToViewportPoint(current);
            maxWorld = Mathf.Max(maxWorld, Vector3.Distance(initial, current));
            maxScreen = Mathf.Max(maxScreen, Vector2.Distance(screenStart, screen));
            Assert.That(screen.z, Is.GreaterThan(0), "Paw is behind the camera.");
            Assert.That(screen.x, Is.InRange(0f, 1f));
            Assert.That(screen.y, Is.InRange(0f, 1f));
            if (Time.time >= nextFrame)
            {
                Capture(camera, frame++);
                nextFrame = Time.time + .4f;
            }
        }
        Debug.Log($"VISIBLE_PAW vertices={points.Count}; world={maxWorld:F5}; viewport={maxScreen:F5}");
        Assert.That(maxWorld, Is.GreaterThan(.10f), "The skinned paw, not only its bone, must move 10cm.");
        Assert.That(maxScreen, Is.GreaterThan(.04f), "Reward close-up must show at least 4% viewport paw movement (previous wide framing: 2.85%).");
    }

    [UnityTest] public IEnumerator FixedAiResponsesCompleteAndProduceReviewFrames()
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var session = new CatLifeSessionController(data);
        session.BeginTransition(60, now-61); session.EnterFocus(now-61);
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(5);
        var cat = GameObject.Find("CatLifeMobileCat");
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        var presenter = cat.GetComponent("CatLifeMobileCatPresenter");
        var camera = Camera.main;
        foreach (string reaction in new[] { "paw_wave", "tail_wag", "focus_rest", "stretch" })
        {
            Assert.That((bool)coordinator.GetType().GetMethod("PlayAiReaction").Invoke(coordinator, new object[] { reaction }), Is.True);
            var samples = new System.Text.StringBuilder("seconds,state,normalized_time,root_x,root_y,root_z\n");
            float start = Time.time;
            int frame = 0;
            do
            {
                yield return new WaitForSeconds(1f / 6f);
                var state = cat.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0);
                Vector3 p = cat.transform.position;
                samples.AppendLine(FormattableString.Invariant($"{Time.time-start:F3},{state.shortNameHash},{state.normalizedTime:F3},{p.x:F4},{p.y:F4},{p.z:F4}"));
                Capture(camera, frame++, "../wp01-r20-actions-20261013/" + reaction);
            } while (!(bool)presenter.GetType().GetProperty("AiReactionCompleted").GetValue(presenter) && Time.time - start < 14f);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp01-r20-actions-20261013", reaction));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "samples.csv"), samples.ToString());
            Assert.That((bool)presenter.GetType().GetProperty("AiReactionCompleted").GetValue(presenter), Is.True, reaction);
            if (reaction == "paw_wave") Assert.That(Time.time-start, Is.GreaterThanOrEqualTo(2.4f));
            yield return new WaitForSeconds(.3f);
            Assert.That(cat.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"), Is.True, reaction);
        }
    }

    [UnityTest] public IEnumerator RoamingDecisionsExposePhysicalProgressAndRestReasons()
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        data.settings.autoFocusAdaptationSeconds = 0;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        var cat = GameObject.Find("CatLifeMobileCat");
        var telemetry = cat.GetComponent("CatBehaviorTelemetry");
        if (telemetry == null) telemetry = cat.AddComponent(Type.GetType("CatLife.Cat.CatBehaviorTelemetry, Assembly-CSharp"));
        var rows = new System.Text.StringBuilder();
        float start = Time.time;
        for (int sample = 0; sample < 90; sample++)
        {
            yield return new WaitForSeconds(1);
            rows.AppendLine(FormattableString.Invariant($"seconds={Time.time-start:F3}; ") + telemetry.GetType().GetMethod("BuildDebugReport").Invoke(telemetry, null));
        }
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp03-diagnostics-20261009"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "roaming-decisions.txt"), rows.ToString());
        Assert.That(cat.activeInHierarchy, Is.True);
    }

    [UnityTest] public IEnumerator HomeActionBackViewProducesIdentificationFrames()
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        data.settings.autoFocusAdaptationSeconds = 0;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(1);
        var cat = GameObject.Find("CatLifeMobileCat");
        foreach (var component in cat.GetComponents<MonoBehaviour>()) component.enabled = false;
        var systems = GameObject.Find("CatLifeRuntimeSystems");
        foreach (var component in systems.GetComponents<MonoBehaviour>()) component.enabled = false;
        cat.GetComponent<Rigidbody>().isKinematic = true;
        cat.transform.rotation = Quaternion.Euler(0, 180, 0);
        var animator = cat.GetComponent<Animator>();
        animator.speed = 0;
        string[] states = {
            "CL_CAT_StandIdle_v01_loop_96f", "CL_CAT_SitCuriousSniff_v01_loop_112f",
            "CL_CAT_CrouchLookBack_v01_loop_112f", "CL_CAT_SitTailGreeting_v01_loop_96f",
            "CL_CAT_StandStretchYawn_v01_144f"
        };
        foreach (string state in states)
        {
            Assert.That(animator.HasState(0, Animator.StringToHash("Base Layer." + state)), Is.True, state);
            for (int frame = 0; frame < 5; frame++)
            {
                animator.Play(state, 0, frame * .2f);
                animator.Update(0);
                Capture(Camera.main, frame, "../wp03-action-identification-r16/" + state);
            }
        }
        // Diagnostic snapshots identify clip shapes; they do not validate natural transitions.
    }

    [UnityTest] public IEnumerator StandToSitProducesSideAndBackReviewFrames()
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        data.settings.autoFocusAdaptationSeconds = 0;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(1);
        var cat = GameObject.Find("CatLifeMobileCat");
        foreach (var component in cat.GetComponents<MonoBehaviour>()) component.enabled = false;
        foreach (var component in GameObject.Find("CatLifeRuntimeSystems").GetComponents<MonoBehaviour>()) component.enabled = false;
        cat.GetComponent<Rigidbody>().isKinematic = true;
        var animator = cat.GetComponent<Animator>();
        var clip = System.Linq.Enumerable.First(animator.runtimeAnimatorController.animationClips,
            c => c.name == "CL_CAT_StandToSit_v01_72f");
        animator.enabled = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = 0;
        Vector3 origin = cat.transform.position;
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        string[] feet = { "frontleg2", "R_frontleg2", "backleg2", "R_backleg2" };
        var points = new List<int>[4];
        using (var counts = skin.sharedMesh.GetBonesPerVertex())
        using (var weights = skin.sharedMesh.GetAllBoneWeights())
        {
            for (int f = 0; f < feet.Length; f++)
            {
                points[f] = new List<int>();
                int bone = Array.FindIndex(skin.bones, b => b.name == feet[f]);
                int offset = 0;
                for (int v = 0; v < counts.Length; v++)
                {
                    float influence = 0;
                    for (int j = 0; j < counts[v]; j++)
                        if (weights[offset + j].boneIndex == bone) influence += weights[offset + j].weight;
                    if (influence >= .99f) points[f].Add(v);
                    offset += counts[v];
                }
                Assert.That(points[f].Count, Is.GreaterThan(0), feet[f]);
            }
        }
        baked = new Mesh();
        var rows = new System.Text.StringBuilder("yaw,seconds,foot,sole_y,floor_y,gap,surface\n");
        foreach (var renderer in cat.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            Debug.Log($"SIT_RENDERER name={renderer.name}; enabled={renderer.enabled}; mesh={renderer.sharedMesh.name}; vertices={renderer.sharedMesh.vertexCount}; bones={renderer.bones.Length}");
        float lowestGap = float.PositiveInfinity;
        var detail = new GameObject("Sit support side camera").AddComponent<Camera>();
        detail.CopyFrom(Camera.main);
        detail.enabled = false;
        detail.rect = new Rect(0, 0, 1, 1);
        detail.orthographic = true;
        detail.orthographicSize = 1.1f;
        foreach (int yaw in new[] { 90, 180 })
        {
            for (int frame = 0; frame <= 12; frame++)
            {
                animator.Play("Base Layer." + clip.name, 0, frame / 12f);
                animator.Update(0);
                cat.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, yaw, 0));
                yield return null;
                Capture(Camera.main, frame, "../wp03-sit-refresh-r16/yaw-" + yaw);
                skin.BakeMesh(baked, false);
                var vertices = baked.vertices;
                Debug.Log($"SIT_SAMPLE yaw={yaw}; frame={frame}; state={animator.GetCurrentAnimatorStateInfo(0).shortNameHash}; phase={animator.GetCurrentAnimatorStateInfo(0).normalizedTime:F3}; meshMin={System.Linq.Enumerable.Min(vertices, v => v.y):F5}; frontBoneY={Array.Find(skin.bones, b => b.name == "frontleg2").position.y:F5}");
                for (int f = 0; f < feet.Length; f++)
                {
                    Vector3 sole = new Vector3(0, float.PositiveInfinity, 0);
                    foreach (int v in points[f])
                    {
                        Vector3 world = skin.transform.position + skin.transform.rotation * vertices[v];
                        if (world.y < sole.y) sole = world;
                    }
                    float distance = float.PositiveInfinity, floor = 0;
                    string surface = "missing";
                    foreach (var hit in Physics.RaycastAll(sole + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore))
                        if (!hit.transform.IsChildOf(cat.transform) && hit.distance < distance)
                        { distance = hit.distance; floor = hit.point.y; surface = hit.collider.name; }
                    Assert.That(float.IsPositiveInfinity(distance), Is.False);
                    float gap = sole.y - floor;
                    lowestGap = Mathf.Min(lowestGap, gap);
                    rows.AppendLine(FormattableString.Invariant($"{yaw},{clip.length * frame / 12f:F3},{feet[f]},{sole.y:F5},{floor:F5},{gap:F5},{surface}"));
                }
                if (yaw == 90)
                {
                    detail.transform.position = origin + cat.transform.right * 4 + Vector3.up * .65f;
                    detail.transform.LookAt(origin + Vector3.up * .45f);
                    Capture(detail, frame, "../wp03-sit-refresh-r16/low-side", 1000, 700);
                }
            }
        }
        Assert.That(clip.length, Is.EqualTo(1.2f).Within(.02f));
        UnityEngine.Object.Destroy(detail.gameObject);
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp03-sit-refresh-r16/foot-gaps.csv")), rows.ToString());
        Assert.That(lowestGap, Is.GreaterThanOrEqualTo(-.001f), "The isolated sit transition penetrates the road by more than 1mm.");
        // Isolated clip samples are visual evidence, not a runtime transition acceptance test.
    }

    [UnityTest] public IEnumerator RewardTurnCandidateOnFormalMeshProducesReviewFrames()
    {
#if UNITY_EDITOR
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        long finishedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1;
        var session = new CatLifeSessionController(data);
        session.BeginTransition(60, finishedAt - 60);
        session.EnterFocus(finishedAt - 60);
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(5);
        var cat = GameObject.Find("CatLifeMobileCat");
        foreach (var component in cat.GetComponents<MonoBehaviour>()) component.enabled = false;
        foreach (var component in GameObject.Find("CatLifeRuntimeSystems").GetComponents<MonoBehaviour>()) component.enabled = false;
        GameObject.Find("CatLifeMobileApp").GetComponent<MonoBehaviour>().enabled = false;
        cat.GetComponent<Rigidbody>().isKinematic = true;
        var animator = cat.GetComponent<Animator>();
        var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Editor/CatProneReview/CL_CAT_RewardSequence_candidate.anim");
        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.length, Is.EqualTo(6.4f).Within(.02f));
        var overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrides.GetOverrides(pairs);
        int index = pairs.FindIndex(p => p.Key.name == "CL_CAT_StandIdle_v01_loop_96f");
        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        pairs[index] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[index].Key, clip);
        overrides.ApplyOverrides(pairs);
        animator.runtimeAnimatorController = overrides;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = 0;
        yield return null;
        var director = Camera.main.GetComponent("CatLifeCameraDirector");
        director.GetType().GetMethod("Show").Invoke(director, new object[] { CatLifeSessionPhase.Reward, true });
        var detail = new GameObject("Turn support camera").AddComponent<Camera>();
        detail.CopyFrom(Camera.main); detail.enabled = false;
        detail.rect = new Rect(0, 0, 1, 1);
        detail.orthographic = true; detail.orthographicSize = 1.2f;
        detail.transform.position = cat.transform.position + cat.transform.right * 4 + Vector3.up * .6f;
        detail.transform.LookAt(cat.transform.position + Vector3.up * .5f);
        for (int frame = 0; frame <= 384; frame += 12)
        {
            animator.Play("CL_CAT_StandIdle_v01_loop_96f", 0, frame/384f);
            animator.Update(0);
            yield return null;
            Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip, Is.EqualTo(clip));
            Capture(Camera.main, frame, "../wp01-sequence-outward-20261013/reward");
            Capture(detail, frame, "../wp01-sequence-outward-20261013/side", 1000, 700);
        }
        UnityEngine.Object.Destroy(detail.gameObject);
#else
        Assert.Ignore("Editor-isolated candidate asset review only.");
        yield return null;
#endif
    }

    [UnityTest] public IEnumerator PawVisibleCandidateProducesEveryRewardFrame()
    {
#if UNITY_EDITOR
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var session = new CatLifeSessionController(data);
        session.BeginTransition(60, now - 61);
        session.EnterFocus(now - 61);
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(5);
        var cat = GameObject.Find("CatLifeMobileCat");
        foreach (var component in cat.GetComponents<MonoBehaviour>()) component.enabled = false;
        foreach (var component in GameObject.Find("CatLifeRuntimeSystems").GetComponents<MonoBehaviour>()) component.enabled = false;
        GameObject.Find("CatLifeMobileApp").GetComponent<MonoBehaviour>().enabled = false;
        cat.GetComponent<Rigidbody>().isKinematic = true;
        var animator = cat.GetComponent<Animator>();
        var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Editor/CatProneReview/CL_CAT_PawVisible_candidate.anim");
        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.length, Is.EqualTo(2.4f).Within(.02f));
        var overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
        overrides["CL_CAT_StandIdle_v01_loop_96f"] = clip;
        animator.runtimeAnimatorController = overrides;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = 0;
        yield return null;
        var director = Camera.main.GetComponent("CatLifeCameraDirector");
        director.GetType().GetMethod("Show").Invoke(director, new object[] { CatLifeSessionPhase.Reward, true });
        for (int frame = 0; frame <= 144; frame++)
        {
            animator.Play("CL_CAT_StandIdle_v01_loop_96f", 0, frame/144f);
            animator.Update(0);
            yield return null;
            Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip, Is.EqualTo(clip));
            Capture(Camera.main, frame, "../wp01-paw-unity-20261013/reward");
        }
#else
        Assert.Ignore("Editor-isolated candidate asset review only.");
        yield return null;
#endif
    }

    [UnityTest] public IEnumerator RewardTailSequencePlaysCompletelyAndReturnsToStand()
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true; data.settings.aiEnabled = false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var session = new CatLifeSessionController(data);
        session.BeginTransition(60, now-61); session.EnterFocus(now-61);
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(5);
        var cat = GameObject.Find("CatLifeMobileCat");
        var animator = cat.GetComponent<Animator>();
        var presenter = cat.GetComponent("CatLifeMobileCatPresenter");
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        Assert.That((bool)coordinator.GetType().GetMethod("PlayAiReaction").Invoke(coordinator,new object[]{"tail_wag"}), Is.True);
        float started = Time.time;
        int frame = 0;
        bool seen = false;
        while (!(bool)presenter.GetType().GetProperty("AiReactionCompleted").GetValue(presenter) && Time.time-started < 10)
        {
            yield return null;
            seen |= animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_RewardSideTailReturn_v01_384f");
            if (frame++ % 4 == 0) Capture(Camera.main,frame,"../wp01-sequence-live-20261013");
        }
        Assert.That(seen, Is.True);
        Assert.That((bool)presenter.GetType().GetProperty("AiReactionCompleted").GetValue(presenter), Is.True);
        Assert.That(Time.time-started, Is.GreaterThanOrEqualTo(6.4f));
        yield return new WaitForSeconds(.3f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"), Is.True);
    }

    [UnityTest] public IEnumerator RewardTailSequenceKeepsSupportOnTownSurface()
    { yield return ReviewReactionContact(false); }

    [UnityTest] public IEnumerator PawVisibleCandidateKeepsThreeSupportsAndReturnsToStand()
    { yield return ReviewReactionContact(true); }

    [UnityTest] public IEnumerator LocalLeftTurnKeepsTownSupportAndReturnsToStand()
    { yield return ReviewReactionContact(false, "Left"); }

    [UnityTest] public IEnumerator LocalRightTurnKeepsTownSupportAndReturnsToStand()
    { yield return ReviewReactionContact(false, "Right"); }

    [UnityTest] public IEnumerator LocalLeftTurnChainsNinetyDegreesWithoutPlantedSlide()
    { yield return ReviewReactionContact(false, "Left", 3); }

    [UnityTest] public IEnumerator LocalRightTurnChainsHalfTurnWithoutPlantedSlide()
    { yield return ReviewReactionContact(false, "Right", 6); }

    [UnityTest] public IEnumerator LocalLeftTurnCanStopDuringSwingWithoutRootDrift()
    { yield return ReviewReactionContact(false, "Left", 1, 43); }

    [UnityTest] public IEnumerator LocalRightTurnCanStopDuringSwingWithoutRootDrift()
    { yield return ReviewReactionContact(false, "Right", 1, 97); }

    [UnityTest] public IEnumerator LocalRightTurnCanResumeMovingWalkAndStopStanding()
    { yield return ReviewReactionContact(false, "Right", 1, 120, true); }

    [Serializable] sealed class TurnRootSample { public float yaw_degrees; public float[] root_translation; }
    [Serializable] sealed class TurnRootSchedule { public TurnRootSample[] samples; }

    IEnumerator ReviewReactionContact(bool pawCandidate, string turnDirection = null, int turnCount = 1, int stopAt = 120, bool walkAfter = false)
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true; data.settings.aiEnabled = false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var session = new CatLifeSessionController(data);
        session.BeginTransition(60, now-61); session.EnterFocus(now-61);
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(5);
        var cat = GameObject.Find("CatLifeMobileCat");
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var animator = cat.GetComponent<Animator>();
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        string output = pawCandidate ? "wp01-paw-contact-20261013" : "wp01-tail-contact-20261013";
        if (turnDirection != null) output = "wp03-town-turn-20261013/"+turnDirection;
        if (turnCount > 1) output = "wp03-town-turn-chain-synced-20261013/"+turnDirection+"-"+(turnCount*30);
        if (stopAt != 120) output = "wp03-town-turn-stop-20261013/"+turnDirection+"-"+stopAt;
        if (walkAfter) output = "wp03-town-turn-walk-20261013/"+turnDirection;
#if UNITY_EDITOR
        AnimationClip candidate = null;
        if (pawCandidate || turnDirection != null)
        {
            candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Editor/CatProneReview/CL_CAT_"+
                (pawCandidate ? "PawVisible" : "TurnLocal"+turnDirection)+"_candidate.anim");
            Assert.That(candidate, Is.Not.Null);
            var overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
            overrides[pawCandidate ? "CL_CAT_CrouchPawWave_v01_loop_96f" : "CL_CAT_RewardSideTailReturn_v01_384f"] = candidate;
            animator.runtimeAnimatorController = overrides;
            yield return null;
        }
#endif
        string[] feet = { "frontleg2", "R_frontleg2", "backleg2", "R_backleg2" };
        var points = new List<int>[4];
        using (var counts = skin.sharedMesh.GetBonesPerVertex())
        using (var weights = skin.sharedMesh.GetAllBoneWeights())
        {
            for (int f=0; f<4; f++)
            {
                points[f] = new List<int>();
                int bone = Array.FindIndex(skin.bones, b => b.name == feet[f]);
                int offset = 0;
                for (int v=0; v<counts.Length; v++)
                {
                    float influence = 0;
                    for (int j=0; j<counts[v]; j++)
                        if (weights[offset+j].boneIndex == bone) influence += weights[offset+j].weight;
                    if (influence >= .99f) points[f].Add(v);
                    offset += counts[v];
                }
                Assert.That(points[f].Count, Is.GreaterThan(0), feet[f]);
            }
        }
        baked = new Mesh();
        var detail = new GameObject("Tail support camera").AddComponent<Camera>();
        detail.CopyFrom(Camera.main); detail.enabled = false;
        detail.rect = new Rect(0,0,1,1); detail.orthographic = true; detail.orthographicSize = 1.1f;
        detail.transform.position = cat.transform.position + cat.transform.right*4 + Vector3.up*.6f;
        detail.transform.LookAt(cat.transform.position + Vector3.up*.45f);
        var rows = new System.Text.StringBuilder("frame,phase,foot,gap,surface\n");
        float lowest = float.PositiveInfinity, worstSupport = 0, worstPlanted = 0;
        float worstSlide = 0;
        int turnSegment = 0;
        var anchors = new Vector3[4][];
        var anchorKeys = new int[]{-1,-1,-1,-1};
        var slides = new System.Text.StringBuilder("frame,segment,phase,foot,support_key,horizontal_drift\n");
        bool sawCandidate = false;
        int frame = 0;
        bool turnSampleReady = turnDirection == null;
        var probe = new GameObject("Tail contact probe").AddComponent<CatTerrainLateProbe>();
        probe.sample = () =>
        {
            if (!turnSampleReady) return;
            skin.BakeMesh(baked, false);
            var vertices = baked.vertices;
            float nearest = float.PositiveInfinity;
            for (int f=0; f<4; f++)
            {
                Vector3 sole = new Vector3(0,float.PositiveInfinity,0);
                foreach (int v in points[f])
                {
                    Vector3 world = skin.transform.position + skin.transform.rotation*vertices[v];
                    if (world.y < sole.y) sole = world;
                }
                float distance = float.PositiveInfinity, floor = 0;
                string surface = "missing";
                foreach (var hit in Physics.RaycastAll(sole+Vector3.up*2,Vector3.down,5,~0,QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(cat.transform) && hit.distance < distance)
                    { distance = hit.distance; floor = hit.point.y; surface = hit.collider.name; }
                Assert.That(surface, Is.Not.EqualTo("missing"));
                float gap = sole.y-floor;
                lowest = Mathf.Min(lowest,gap); nearest = Mathf.Min(nearest,Mathf.Abs(gap));
                if (pawCandidate && f != 0) worstPlanted = Mathf.Max(worstPlanted, Mathf.Abs(gap));
                rows.AppendLine(FormattableString.Invariant($"{frame},{animator.GetCurrentAnimatorStateInfo(0).normalizedTime:F4},{feet[f]},{gap:F6},{surface}"));
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (turnDirection != null && !animator.IsInTransition(0) && state.IsName("CL_CAT_RewardSideTailReturn_v01_384f"))
                {
                    float phase = Mathf.Clamp01(state.normalizedTime);
                    int cycle = Mathf.Min(1,Mathf.FloorToInt(phase*2));
                    float u = phase*2-cycle;
                    int order = turnDirection == "Left" ? new[]{1,3,0,2}[f] : new[]{3,1,2,0}[f];
                    float begin = .08f+order*.21f, end = begin+.19f;
                    if (u <= begin || u >= end)
                    {
                        int key = turnSegment*4+cycle*2+(u >= end ? 1 : 0);
                        if (anchorKeys[f] != key)
                        {
                            anchorKeys[f] = key; anchors[f] = new Vector3[points[f].Count];
                            for (int j=0;j<points[f].Count;j++) anchors[f][j] = skin.transform.position+skin.transform.rotation*vertices[points[f][j]];
                        }
                        float drift = 0;
                        for (int j=0;j<points[f].Count;j++)
                        {
                            var delta = skin.transform.position+skin.transform.rotation*vertices[points[f][j]]-anchors[f][j];
                            delta.y = 0; drift = Mathf.Max(drift,delta.magnitude);
                        }
                        worstSlide = Mathf.Max(worstSlide,drift);
                        slides.AppendLine(FormattableString.Invariant($"{frame},{turnSegment},{phase:F6},{feet[f]},{key},{drift:F6}"));
                    }
                }
            }
            worstSupport = Mathf.Max(worstSupport,nearest);
#if UNITY_EDITOR
            if ((pawCandidate || turnDirection != null) && Array.Exists(animator.GetCurrentAnimatorClipInfo(0), c => c.clip == candidate)) sawCandidate = true;
#endif
            if (frame++ % 12 == 0)
            {
                Capture(detail,frame,"../"+output+"/side",1000,700);
                if (pawCandidate || turnDirection != null) Capture(Camera.main,frame,"../"+output+"/reward");
            }
        };
        if (turnDirection == null)
        {
            Assert.That((bool)coordinator.GetType().GetMethod("PlayAiReaction").Invoke(coordinator,new object[]{pawCandidate ? "paw_wave" : "tail_wag"}), Is.True);
            yield return new WaitForSeconds(pawCandidate ? 3.5f : 7.5f);
        }
        else
        {
            // Isolate root ownership, retaining the production terrain adapter and mesh.
            var terrainContact = cat.GetComponent("CatFootTerrainContact");
            Assert.That(terrainContact, Is.Not.Null);
            foreach (var behaviour in cat.GetComponentsInChildren<MonoBehaviour>())
                if (behaviour != terrainContact)
                { behaviour.StopAllCoroutines(); behaviour.enabled = false; }
            var runtime = (MonoBehaviour)coordinator;
            runtime.StopAllCoroutines(); runtime.enabled = false;
            var body = cat.GetComponent<Rigidbody>();
            body.isKinematic = true; body.interpolation = RigidbodyInterpolation.None;
            Quaternion initialRotation = body.rotation;
            var schedule = JsonUtility.FromJson<TurnRootSchedule>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../Reports/wp03-turn-"+turnDirection.ToLowerInvariant()+"-local-20261013/root-schedule.json"))));
            animator.speed = 0;
            for (turnSegment=0;turnSegment<turnCount;turnSegment++)
            {
                Vector3 start = body.position; Quaternion rotation = body.rotation;
                for (int i=0;i<=stopAt;i++)
                {
                    var sample = schedule.samples[i]; var p = sample.root_translation;
                    body.MovePosition(start+rotation*(new Vector3(-p[0],p[2],-p[1])*.045f));
                    body.MoveRotation(rotation*Quaternion.Euler(0,-sample.yaw_degrees,0));
                    yield return new WaitForFixedUpdate();
                    animator.Play("CL_CAT_RewardSideTailReturn_v01_384f",0,i/120f);
                    animator.Update(0);
                    turnSampleReady = true;
                    yield return null;
                    turnSampleReady = false;
                }
            }
            float finalYaw = (Quaternion.Inverse(initialRotation)*body.rotation).eulerAngles.y;
            float expectedYaw = -schedule.samples[stopAt].yaw_degrees*turnCount;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(expectedYaw,finalYaw)), Is.LessThan(.1f));
            Vector3 stoppedPosition = body.position;
            Quaternion stoppedRotation = body.rotation;
            animator.speed = 1;
            animator.CrossFadeInFixedTime("CL_CAT_StandIdle_v01_loop_96f",.2f);
            turnSampleReady = true;
            yield return new WaitForSeconds(stopAt == 120 ? .6f : 1.2f);
            Assert.That(Vector3.Distance(stoppedPosition,body.position), Is.LessThan(.0001f), "Root moves after its turn schedule stops.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(stoppedRotation.eulerAngles.y,body.rotation.eulerAngles.y)), Is.LessThan(.1f));
            Assert.That(sawCandidate, Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"), Is.True);
            if (walkAfter)
            {
                Vector3 forward = body.rotation*Vector3.forward;
                Vector3 walkStart = body.position;
                animator.CrossFadeInFixedTime("CL_CAT_Walk_v01_loop_60f",.2f);
                bool sawWalk = false;
                int steps = Mathf.RoundToInt(2f/Time.fixedDeltaTime);
                for (int i=0;i<steps;i++)
                {
                    body.MovePosition(body.position+forward*(.405f*Time.fixedDeltaTime));
                    yield return new WaitForFixedUpdate();
                    sawWalk |= animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_Walk_v01_loop_60f");
                }
                yield return null;
                Assert.That(sawWalk, Is.True);
                Assert.That(Vector3.Dot(body.position-walkStart,forward), Is.GreaterThan(.79f), "Walk must move along the new heading, not play in place.");
                animator.CrossFadeInFixedTime("CL_CAT_StandIdle_v01_loop_96f",.2f);
                Vector3 walkEnd = body.position;
                yield return new WaitForSeconds(.6f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"), Is.True);
                Assert.That(Vector3.Distance(walkEnd,body.position), Is.LessThan(.0001f));
                Debug.Log($"TURN_WALK displacement={Vector3.Distance(walkStart,walkEnd):F6}; heading={body.rotation.eulerAngles.y:F3}");
            }
        }
        probe.sample = null;
        UnityEngine.Object.Destroy(probe.gameObject); UnityEngine.Object.Destroy(detail.gameObject);
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/"+output+"/foot-gaps.csv")),rows.ToString());
        if (turnDirection != null) File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/"+output+"/support-slide.csv")),slides.ToString());
        Debug.Log($"REACTION_CONTACT frames={frame}; lowest={lowest:F6}; worstSupport={worstSupport:F6}; worstPlanted={worstPlanted:F6}; worstSlide={worstSlide:F6}");
        Assert.That(frame, Is.GreaterThan(60));
        Assert.That(lowest, Is.GreaterThanOrEqualTo(-.001f), "Foot penetrates the town surface by over 1mm.");
        Assert.That(worstSupport, Is.LessThanOrEqualTo(.025f), "No support paw within the existing 2.5cm contact tolerance.");
        if (turnDirection != null) Assert.That(worstSlide, Is.LessThanOrEqualTo(.005f), "Planted paw vertices slide more than 5mm within an authored support interval.");
        if (pawCandidate)
        {
            Assert.That(sawCandidate, Is.True);
            Assert.That(worstPlanted, Is.LessThanOrEqualTo(.025f), "One of the three planted paws lost support.");
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"), Is.True);
        }
    }

    [UnityTest] public IEnumerator LeavingRewardDuringTailResponseCancelsAndRestoresHomeOwnership()
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true; data.settings.aiEnabled = false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var session = new CatLifeSessionController(data);
        session.BeginTransition(60, now-61); session.EnterFocus(now-61);
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(5);
        var cat = GameObject.Find("CatLifeMobileCat");
        var animator = cat.GetComponent<Animator>();
        var presenter = cat.GetComponent("CatLifeMobileCatPresenter");
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        Assert.That((bool)coordinator.GetType().GetMethod("PlayAiReaction").Invoke(coordinator,new object[]{"tail_wag"}), Is.True);
        yield return new WaitForSeconds(3);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_RewardSideTailReturn_v01_384f"), Is.True);
        app.GetType().GetMethod("ReturnToTown",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(app,null);
        yield return new WaitForSeconds(1.4f);
        Assert.That((bool)coordinator.GetType().GetProperty("BehaviorOwnsAnimator").GetValue(coordinator), Is.True);
        for (int i=0;i<20;i++)
        {
            yield return new WaitForSeconds(.2f);
            Assert.That((bool)presenter.GetType().GetProperty("IsAiReactionPlaying").GetValue(presenter), Is.False);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_RewardSideTailReturn_v01_384f"), Is.False);
            Assert.That((string)app.GetType().GetProperty("CurrentView").GetValue(app), Is.EqualTo("HomeHudLayer"));
        }
        var stored = CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key));
        Assert.That(stored.records.Count, Is.EqualTo(1));
        Assert.That(stored.records[0].GrowthAwarded, Is.EqualTo(1));
        Assert.That(stored.records[0].PawsAwarded, Is.EqualTo(1));
    }

    Vector3 PawCenter(SkinnedMeshRenderer skin, List<int> points)
    {
        skin.BakeMesh(baked, false);
        var vertices = baked.vertices;
        Vector3 center = Vector3.zero;
        // This rig's baked vertices already contain its 4.5x inherited scale.
        // Match the terrain-contact audit; TransformPoint would apply it twice.
        foreach (int index in points) center += skin.transform.position + skin.transform.rotation * vertices[index];
        return center / points.Count;
    }

    internal static void Capture(Camera camera, int frame, string group = "implementation-paw-frames", int width = 540, int height = 1200)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/interview-demo-20261008", group));
        Directory.CreateDirectory(folder);
        var target = new RenderTexture(width, height, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(folder, $"paw-{frame:D2}.png"), image.EncodeToPNG());
        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        target.Release();
        UnityEngine.Object.Destroy(target);
        UnityEngine.Object.Destroy(image);
    }
}

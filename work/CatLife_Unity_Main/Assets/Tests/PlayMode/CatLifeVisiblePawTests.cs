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
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        var cat = GameObject.Find("CatLifeMobileCat");
        var coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        var presenter = cat.GetComponent("CatLifeMobileCatPresenter");
        var camera = Camera.main;
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Reward });
        camera.GetComponent("CatLifeCameraDirector").GetType().GetMethod("Show").Invoke(
            camera.GetComponent("CatLifeCameraDirector"), new object[] { CatLifeSessionPhase.Reward, true });
        yield return new WaitForSeconds(2.5f);
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
                Capture(camera, frame++, "../wp01-20261009-frames/" + reaction);
            } while (!(bool)presenter.GetType().GetProperty("AiReactionCompleted").GetValue(presenter) && Time.time - start < 14f);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp01-20261009-frames", reaction));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "samples.csv"), samples.ToString());
            Assert.That((bool)presenter.GetType().GetProperty("AiReactionCompleted").GetValue(presenter), Is.True, reaction);
            yield return new WaitForSeconds(.3f);
            Assert.That(cat.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"), Is.True, reaction);
        }
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

    static void Capture(Camera camera, int frame, string group = "implementation-paw-frames", int width = 540, int height = 1200)
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

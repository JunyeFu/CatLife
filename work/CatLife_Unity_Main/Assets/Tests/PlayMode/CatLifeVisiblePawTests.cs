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

    static void Capture(Camera camera, int frame)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/interview-demo-20261008/implementation-paw-frames"));
        Directory.CreateDirectory(folder);
        var target = new RenderTexture(540, 1200, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var image = new Texture2D(540, 1200, TextureFormat.RGB24, false);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 540, 1200), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(folder, $"paw-{frame:D2}.png"), image.EncodeToPNG());
        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        target.Release();
        UnityEngine.Object.Destroy(target);
        UnityEngine.Object.Destroy(image);
    }
}

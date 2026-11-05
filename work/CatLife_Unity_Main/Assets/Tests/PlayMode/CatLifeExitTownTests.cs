using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CatLifeExitTownTests
{
    const string Key = "CatLife.Mobile.Data.v1";
    string saved;
    bool hadData;
    [SetUp] public void PreserveData() { hadData = PlayerPrefs.HasKey(Key); saved = PlayerPrefs.GetString(Key); }
    [UnityTearDown] public IEnumerator RestoreData()
    {
        var scene = SceneManager.GetSceneByName("CatLifeMobile");
        if (scene.isLoaded)
        {
            var empty = SceneManager.CreateScene("ExitTownCleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        if (hadData) PlayerPrefs.SetString(Key, saved); else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    [UnityTest]
    public IEnumerator TownSurfaceSupportsReachAndPartialCancellation()
    {
#if UNITY_EDITOR
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var seed = new CatLifeSessionController(data);
        seed.BeginTransition(900, now); seed.EnterFocus(now);
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(5);
        var cat = GameObject.Find("CatLifeMobileCat");
        var terrain = cat.GetComponent("CatFootTerrainContact");
        Assert.That(terrain, Is.Not.Null);
        foreach (var behaviour in cat.GetComponentsInChildren<MonoBehaviour>())
            if (behaviour != terrain) { behaviour.CancelInvoke(); behaviour.StopAllCoroutines(); behaviour.enabled = false; }
        var coordinator = (MonoBehaviour)GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        coordinator.StopAllCoroutines(); coordinator.enabled = false;
        var body = cat.GetComponent<Rigidbody>();
        body.isKinematic = true; body.interpolation = RigidbodyInterpolation.None;
        Vector3 anchor = cat.transform.position;
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var animator = cat.GetComponent<Animator>();
        const string folder = "Assets/Editor/CatProneReview/ExitCandidate/RuntimeIntegration/";
        var candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(folder + "PF_CL_CAT_ExitIntegration_candidate.prefab");
        var candidateSkin = candidate.GetComponentInChildren<SkinnedMeshRenderer>();
        var bones = cat.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
        skin.bones = candidateSkin.bones.Select(b => bones[b.name]).ToArray();
        skin.sharedMesh = candidateSkin.sharedMesh;
        for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) skin.SetBlendShapeWeight(i, 0);
        animator.runtimeAnimatorController = candidate.GetComponent<Animator>().runtimeAnimatorController;
        animator.speed = 0;
        animator.Play("CL_CAT_ProneBreath_v11_loop_240f", 0, 0); animator.Update(0);
        yield return null;
        string[] feet = { "frontleg2", "R_frontleg2", "backleg2", "R_backleg2" };
        var weights = skin.sharedMesh.boneWeights;
        var footPoints = feet.Select(name => Enumerable.Range(0, weights.Length).Where(i =>
            weights[i].weight0 > .99f && skin.bones[weights[i].boneIndex0].name == name).ToArray()).ToArray();
        Assert.That(footPoints.All(p => p.Length > 0), Is.True);
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp04-town-exit-fixed-20261017"));
        Directory.CreateDirectory(output);
        var rows = new StringBuilder("sample,tag,state,phase,foot,gap,surface\n");
        var baked = new Mesh();
        var camera = new GameObject("Exit town side camera").AddComponent<Camera>();
        camera.CopyFrom(Camera.main); camera.enabled = false; camera.rect = new Rect(0, 0, 1, 1);
        camera.orthographic = true; camera.orthographicSize = 1.1f;
        camera.transform.position = anchor + cat.transform.right * 4 + Vector3.up * .6f;
        camera.transform.LookAt(anchor + Vector3.up * .45f);
        var probe = new GameObject("Exit town contact probe").AddComponent<CatTerrainLateProbe>();
        bool requested = false;
        int sample = 0, missing = 0;
        string tag = "baseline";
        float lowest = float.PositiveInfinity, worstSupport = 0, worstCancelJump = 0;
        Vector3[] latest = null;
        probe.sample = () =>
        {
            if (!requested) return;
            requested = false;
            skin.BakeMesh(baked, false);
            latest = baked.vertices.Select(v => skin.transform.position + skin.transform.rotation * v).ToArray();
            var state = animator.GetCurrentAnimatorStateInfo(0);
            foreach (int f in Enumerable.Range(0, 4))
            {
                Vector3 sole = footPoints[f].Select(i => latest[i]).OrderBy(p => p.y).First();
                var hits = Physics.RaycastAll(sole + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.transform.IsChildOf(cat.transform)).OrderBy(h => h.distance).ToArray();
                if (hits.Length == 0) { missing++; continue; }
                float gap = sole.y - hits[0].point.y;
                lowest = Mathf.Min(lowest, gap);
                if (f != 0) worstSupport = Mathf.Max(worstSupport, Mathf.Abs(gap));
                rows.AppendLine(FormattableString.Invariant($"{sample},{tag},{animator.GetCurrentAnimatorClipInfo(0)[0].clip.name},{state.normalizedTime:F6},{feet[f]},{gap:F7},{hits[0].collider.name}"));
            }
            CatLifeExitActionTests.Capture(camera, Path.Combine(output, "side"), sample);
            CatLifeExitActionTests.Capture(Camera.main, Path.Combine(output, "focus-world"), sample, 540, 1200);
            sample++;
        };
        try
        {
            requested = true; while (requested) yield return null;
            string[] states = { "CL_CAT_ProneExitReach_v01_24f", "CL_CAT_ProneExitHold_v01_loop_48f", "CL_CAT_ProneExitRelease_v01_24f" };
            for (int c = 0; c < states.Length; c++)
            {
                tag = states[c];
                int frames = c == 1 ? 48 : 24;
                for (int frame = 0; frame <= frames; frame++)
                {
                    animator.Play(states[c], 0, c == 1 && frame == frames ? .999999f : (float)frame / frames);
                    animator.Update(0);
                    requested = true; while (requested) yield return null;
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(states[c]), Is.True);
                }
            }
            foreach (float phase in new[] { .25f, .5f, .75f })
            {
                tag = "cancel-reach-" + phase;
                animator.Play(states[0], 0, phase); animator.Update(0);
                requested = true; while (requested) yield return null;
                var before = latest;
                animator.Play(states[2], 0, 1-phase); animator.Update(0);
                requested = true; while (requested) yield return null;
                worstCancelJump = Mathf.Max(worstCancelJump, latest.Select((v, i) => Vector3.Distance(v, before[i])).Max());
                animator.speed = 1;
                tag = "cancel-release-" + phase;
                while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1)
                { requested = true; while (requested) yield return null; }
                animator.speed = 0;
                Assert.That(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("CL_CAT_ProneExitReachCorrective_v01")), Is.LessThan(.01f));
                Assert.That(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("CL_CAT_ProneExitBridgeCorrective_v01")), Is.LessThan(.01f));
            }
            Debug.Log($"EXIT_TOWN_METRICS samples={sample} lowest={lowest:R} support={worstSupport:R} cancelJump={worstCancelJump:R} missing={missing} rootDrift={Vector3.Distance(cat.transform.position, anchor):R}");
            Assert.That(missing, Is.Zero);
            Assert.That(lowest, Is.GreaterThanOrEqualTo(-.001f));
            Assert.That(worstSupport, Is.LessThanOrEqualTo(.0025f));
            Assert.That(worstCancelJump, Is.LessThan(.0002f));
            Assert.That(Vector3.Distance(cat.transform.position, anchor), Is.LessThan(.0002f));
        }
        finally
        {
            probe.sample = null;
            File.WriteAllText(Path.Combine(output, "feet.csv"), rows.ToString());
            UnityEngine.Object.Destroy(probe.gameObject); UnityEngine.Object.Destroy(camera.gameObject); UnityEngine.Object.Destroy(baked);
        }
#else
        yield break;
#endif
    }
}

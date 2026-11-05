using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CatLifeExitActionTests
{
    [System.Serializable] sealed class SourceFrame { public string action; public int frame; public Vector3 minimum; public Vector3 maximum; }
    [System.Serializable] sealed class SourceReference { public SourceFrame[] samples; }

    [UnityTest]
    public IEnumerator ExtendedMeshPreservesExistingStatesAndClearsExitShapes()
    {
#if UNITY_EDITOR
        const string root = "Assets/Editor/CatProneReview/ExitCandidate/RuntimeIntegration/";
        var original = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MobileRuntime/Art/Cat/PF_CL_CAT_Runtime_v12.prefab"));
        var candidate = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(root + "PF_CL_CAT_ExitIntegration_candidate.prefab"));
        var cats = new[] { original, candidate };
        var skins = cats.Select(c => c.GetComponentInChildren<SkinnedMeshRenderer>()).ToArray();
        var animators = cats.Select(c => c.GetComponent<Animator>()).ToArray();
        var meshes = new[] { new Mesh(), new Mesh() };
        var oldWeights = QualitySettings.skinWeights;
        var rows = new StringBuilder("state,phase,max_mesh_difference\n");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp04-unity-exit-20261017/legacy-mesh-compatibility.csv"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        try
        {
            QualitySettings.skinWeights = SkinWeights.Unlimited;
            foreach (var cat in cats)
            {
                cat.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                cat.transform.localScale = Vector3.one * .045f;
                foreach (var behaviour in cat.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            }
            foreach (var skin in skins) { skin.enabled = false; skin.quality = SkinQuality.Auto; }
            foreach (var animator in animators) { animator.speed = 0; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            yield return null;
            Assert.That(skins[0].sharedMesh.vertexCount, Is.EqualTo(skins[1].sharedMesh.vertexCount));
            var controller = (UnityEditor.Animations.AnimatorController)animators[0].runtimeAnimatorController;
            float worst = 0;
            foreach (var entry in controller.layers[0].stateMachine.states)
                foreach (float phase in new[] { 0f, .5f, .999999f })
                {
                    foreach (var animator in animators) { animator.Play(entry.state.name, 0, phase); animator.Update(0); }
                    yield return null;
                    float difference = MeshDifference(skins, meshes);
                    worst = Mathf.Max(worst, difference);
                    rows.AppendLine($"{entry.state.name},{phase:R},{difference:R}");
                }
            animators[1].Play("CL_CAT_ProneExitReach_v01_24f", 0, 1); animators[1].Update(0);
            yield return null;
            Assert.That(skins[1].GetBlendShapeWeight(skins[1].sharedMesh.GetBlendShapeIndex("CL_CAT_ProneExitReachCorrective_v01")), Is.GreaterThan(99));
            animators[1].Play("CL_CAT_ProneExitRelease_v01_24f", 0, 1); animators[1].Update(0);
            yield return null;
            foreach (var animator in animators) { animator.Play("CL_CAT_StandIdle_v01_loop_96f", 0, 0); animator.Update(0); }
            yield return null;
            float afterExit = MeshDifference(skins, meshes);
            foreach (string name in new[] { "CL_CAT_ProneExitReachCorrective_v01", "CL_CAT_ProneExitBridgeCorrective_v01" })
                Assert.That(skins[1].GetBlendShapeWeight(skins[1].sharedMesh.GetBlendShapeIndex(name)), Is.Zero);
            rows.AppendLine($"after_exit_stand,0,{afterExit:R}");
            Debug.Log($"EXIT_LEGACY_COMPATIBILITY states={controller.layers[0].stateMachine.states.Length} sampledPhases=3 worst={worst:R} afterExit={afterExit:R}");
            Assert.That(worst, Is.LessThan(.0002f));
            Assert.That(afterExit, Is.LessThan(.0002f));
        }
        finally
        {
            File.WriteAllText(output, rows.ToString());
            QualitySettings.skinWeights = oldWeights;
            foreach (var cat in cats) Object.Destroy(cat);
            foreach (var mesh in meshes) Object.Destroy(mesh);
        }
#else
        yield break;
#endif
    }

    static float MeshDifference(SkinnedMeshRenderer[] skins, Mesh[] meshes)
    {
        for (int i = 0; i < 2; i++) skins[i].BakeMesh(meshes[i]);
        var a = meshes[0].vertices; var b = meshes[1].vertices;
        return a.Select((v, i) => Vector3.Distance(v, b[i])).Max();
    }

    [UnityTest]
    public IEnumerator ReachHoldReleaseKeepSupportAndJoinWithoutPoseJumps()
    {
#if UNITY_EDITOR
        const string folder = "Assets/Editor/CatProneReview/ExitCandidate/";
        string[] names = { "CL_CAT_ProneExitReach_v01_24f", "CL_CAT_ProneExitHold_v01_loop_48f", "CL_CAT_ProneExitRelease_v01_24f" };
        var cat = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(folder + "PF_CL_CAT_ExitCandidate.prefab"));
        cat.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var transform in cat.GetComponentsInChildren<Transform>()) transform.gameObject.layer = 31;
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var animator = cat.GetComponent<Animator>();
        var baked = new Mesh();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp04-unity-exit-20261017"));
        Directory.CreateDirectory(output);
        var rows = new StringBuilder("clip,frame,minimum,support_drift,join_error\n");
        var oldSkinWeights = QualitySettings.skinWeights;
        var cameraObject = new GameObject("Exit candidate review camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.cullingMask = 1 << 31;
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.87f, .88f, .84f);
        var lamp = new GameObject("Exit candidate review light");
        var light = lamp.AddComponent<Light>();
        light.type = LightType.Directional;
        light.cullingMask = 1 << 31;
        light.intensity = 1.5f;
        lamp.transform.rotation = Quaternion.Euler(40, -30, 0);
        var reference = JsonUtility.FromJson<SourceReference>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../../Reports/wp04-exit-actions-20261013/unity-reference.json"))));
        try
        {
            QualitySettings.skinWeights = SkinWeights.Unlimited;
            animator.speed = 0;
            yield return null;
            var weights = skin.sharedMesh.boneWeights;
            var support = Enumerable.Range(0, weights.Length).Where(i => weights[i].weight0 > .99f &&
                new[] { "R_frontleg2", "backleg2", "R_backleg2" }.Contains(skin.bones[weights[i].boneIndex0].name)).ToArray();
            Assert.That(support.Length, Is.GreaterThan(0));
            Vector3[] first = null, previous = null, holdStart = null;
            int captureIndex = 0;
            float lowest = float.PositiveInfinity, maxSupport = 0, maxJoin = 0, holdDelta = 0;
            for (int c = 0; c < names.Length; c++)
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + names[c] + ".anim");
                Assert.That(clip.length, Is.EqualTo(c == 1 ? .8f : .4f).Within(.0001));
                int frames = c == 1 ? 48 : 24;
                for (int frame = 0; frame <= frames; frame++)
                {
                    // A looping clip's repeated endpoint is sampled just before wrap.
                    float phase = frame == frames && c == 1 ? .999999f : (float)frame / frames;
                    animator.Play(names[c], 0, phase);
                    animator.Update(0);
                    yield return null;
                    Assert.That(animator.GetCurrentAnimatorClipInfo(0).Single().clip.name, Is.EqualTo(names[c]));
                    skin.BakeMesh(baked);
                    // Match the accepted v12 sampling convention: BakeMesh has
                    // already included inherited rig scale in these vertices.
                    var vertices = baked.vertices.Select(v => skin.transform.position + skin.transform.rotation * v).ToArray();
                    if (first == null) first = vertices;
                    var bounds = new Bounds(vertices[0], Vector3.zero);
                    foreach (var vertex in vertices) bounds.Encapsulate(vertex);
                    if (captureIndex == 0)
                    {
                        camera.orthographicSize = bounds.size.magnitude * .45f;
                        camera.farClipPlane = bounds.size.magnitude * 10;
                        camera.nearClipPlane = .01f;
                    }
                    foreach (string view in new[] { "side", "front" })
                    {
                        var center = new Vector3(0, .65f, 0);
                        camera.transform.position = center + (view == "side" ? new Vector3(4, .2f, 0) : new Vector3(0, .2f, 4));
                        camera.transform.LookAt(center);
                        Capture(camera, Path.Combine(output, view), captureIndex);
                    }
                    captureIndex++;
                    var sourceFrame = reference.samples.Single(s => s.action == names[c] && s.frame == frame);
                    Assert.That(Vector3.Distance(bounds.min, sourceFrame.minimum), Is.LessThan(.0002f), "Source/import bounds minimum differs");
                    Assert.That(Vector3.Distance(bounds.max, sourceFrame.maximum), Is.LessThan(.0002f), "Source/import bounds maximum differs");
                    float minimum = vertices.Min(v => v.y);
                    float drift = support.Max(i => Vector3.Distance(vertices[i], first[i]));
                    float join = frame == 0 && previous != null ? vertices.Select((v, i) => Vector3.Distance(v, previous[i])).Max() : 0;
                    if (c == 1 && frame == 0) holdStart = vertices;
                    if (c == 1) holdDelta = Mathf.Max(holdDelta, vertices.Select((v, i) => Vector3.Distance(v, holdStart[i])).Max());
                    lowest = Mathf.Min(lowest, minimum); maxSupport = Mathf.Max(maxSupport, drift); maxJoin = Mathf.Max(maxJoin, join);
                    rows.AppendLine($"{names[c]},{frame},{minimum:R},{drift:R},{join:R}");
                    previous = vertices;
                }
            }
            File.WriteAllText(Path.Combine(output, "frames.csv"), rows.ToString());
            float closure = previous.Select((v, i) => Vector3.Distance(v, first[i])).Max();
            Debug.Log($"EXIT_ACTION_METRICS min={lowest:R} support={maxSupport:R} join={maxJoin:R} closure={closure:R} holdBodyDelta={holdDelta:R} bounds={baked.bounds.size}");
            Assert.That(lowest, Is.GreaterThanOrEqualTo(-.0002f), "Imported mesh penetrates the flat reference floor");
            Assert.That(maxSupport, Is.LessThan(.0002f));
            Assert.That(maxJoin, Is.LessThan(.0002f));
            Assert.That(closure, Is.LessThan(.0002f));
            Assert.That(holdDelta, Is.GreaterThan(.000001f), "Hold must contain real breathing deformation");
        }
        finally
        {
            File.WriteAllText(Path.Combine(output, "frames.csv"), rows.ToString());
            QualitySettings.skinWeights = oldSkinWeights;
            Object.Destroy(cameraObject); Object.Destroy(lamp); Object.Destroy(cat); Object.Destroy(baked);
        }
#else
        yield break;
#endif
    }

    internal static void Capture(Camera camera, string folder, int frame, int width = 800, int height = 600)
    {
        Directory.CreateDirectory(folder);
        var target = new RenderTexture(width, height, 24);
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, $"{frame:D4}.png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            target.Release();
            Object.Destroy(target); Object.Destroy(image);
        }
    }
}

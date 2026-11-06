using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CatLifeInsightRevealTests
{
    private const string Key = "CatLife.Mobile.Data.v1";
    private bool had;
    private string saved;
    private Component ui, effect;
    private Text text;
    private CatLifeAppData data;
    private bool asyncShaderCompilation;
    [SetUp] public void Preserve()
    {
        had = PlayerPrefs.HasKey(Key); saved = PlayerPrefs.GetString(Key);
        asyncShaderCompilation = UnityEditor.ShaderUtil.allowAsyncCompilation;
        UnityEditor.ShaderUtil.allowAsyncCompilation = false;
    }
    [TearDown] public void Restore()
    {
        if (had) PlayerPrefs.SetString(Key, saved); else PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save();
        UnityEditor.ShaderUtil.allowAsyncCompilation = asyncShaderCompilation;
    }
    private static Type RevealType => Type.GetType("CatLifeInsightReveal, Assembly-CSharp");
    private static Transform View(string name) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == name);
    private T Field<T>(string name) => (T)ui.GetType().GetField(name).GetValue(ui);
    private bool Revealing => (bool)effect.GetType().GetProperty("IsRevealing").GetValue(effect);
    private void Present(CatLifeSessionRecord record, string value) => ui.GetType().GetMethod("RenderReward")
        .Invoke(ui, new object[] { record, value, CatLifeStats.Derive(data, DateTimeOffset.UtcNow.ToUnixTimeSeconds()) });
    private void Freeze(float seconds) => effect.GetType().GetMethod("SetReviewElapsed").Invoke(effect, new object[] { seconds });
    private IEnumerator Load()
    {
        data = new CatLifeAppData(); data.settings.catAdopted = true; data.settings.aiEnabled = false;
        data.settings.autoFocusAdaptationSeconds = 0;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        var installer = Type.GetType("CatLifeUiStyleInstaller, Assembly-CSharp-Editor");
        yield return (AsyncOperation)installer.GetMethod("LoadReviewForCapture").Invoke(null, null);
        yield return null;
        ui = GameObject.Find("CatLifeMobileView").GetComponent("CatLifeMobileUiPresentation");
        text = Field<Text>("rewardInsight"); effect = text.GetComponent(RevealType);
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        app.GetType().GetMethod("ShowOnly", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, new object[] { "RewardPanel" });
        View("RewardCard").gameObject.SetActive(true);
        Assert.That(effect, Is.Not.Null);
        Assert.That(text.GetComponents<BaseMeshEffect>().Length, Is.EqualTo(1), "No Shadow/Outline quads may precede glyph mapping.");
    }

    [Test]
    public void UnicodeElementsShareAnOrderAndSourceSkipsTheClock()
    {
        var args = new object[] { "A\u0301 甲\U0001F600乙\n来源：本地洞察", 0 };
        int[] orders = (int[])RevealType.GetMethod("BuildQuadOrders").Invoke(null, args);
        Assert.That((int)args[1], Is.EqualTo(4));
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 2, 2, 3, -1, -1, -1, -1, -1, -1, -1 }, orders);
    }

    [UnityTest]
    public IEnumerator FullLayoutAndRepeatedRenderStayStableWhileSourceIsImmediate()
    {
        yield return Load();
        var record = new CatLifeSessionRecord();
        const string value = "先喝一口水，再选一个可以完成的小任务，慢慢开始。\n来源：本地洞察";
        Present(record, value); Freeze(0); Canvas.ForceUpdateCanvases(); yield return null;
        var material = text.material;
        Assert.That(material.shader.name, Is.EqualTo("CatLife/UI/InsightReveal"));
        Assert.That(material.shader.isSupported, Is.True);
        Vector2 size = Field<RectTransform>("rewardContent").rect.size;
        string full = text.text;
        int[] lines = text.cachedTextGenerator.lines.Select(line => line.startCharIdx).ToArray();
        Present(record, value); yield return null;
        Assert.That(text.material, Is.SameAs(material), "Repeated AI action/status Render must not restart reveal.");
        Freeze(.09f); yield return null;
        Assert.That(text.text, Is.EqualTo(full));
        Assert.That(Field<RectTransform>("rewardContent").rect.size, Is.EqualTo(size));
        CollectionAssert.AreEqual(lines, text.cachedTextGenerator.lines.Select(line => line.startCharIdx));
        Freeze(5); yield return null;
        Assert.That(Revealing, Is.False);
        Assert.That(text.material.shader.name, Is.Not.EqualTo("CatLife/UI/InsightReveal"));
        Present(record, value); Assert.That(Revealing, Is.False);
    }

    [UnityTest]
    public IEnumerator AppStatusRerenderDoesNotRestoreTheLegacySummaryOrRestart()
    {
        yield return Load();
        var record = new CatLifeSessionRecord { localInsight = "先喝一口水，再开始眼前的小任务。" };
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var render = app.GetType().GetMethod("RenderReward", BindingFlags.NonPublic | BindingFlags.Instance);
        render.Invoke(app, new object[] { record }); Freeze(.03f); yield return null;
        string full = text.text; Material material = text.material;
        render.Invoke(app, new object[] { record }); yield return null;
        Assert.That(text.text, Is.EqualTo(full));
        Assert.That(text.material, Is.SameAs(material));
        Assert.That(text.text, Does.Not.Contain("成长 +"));
        Assert.That(text.material.GetFloat("_Elapsed"), Is.EqualTo(.03f));
    }

    [UnityTest]
    public IEnumerator HiddenWakeUpWaitsThenPageLeaveAndBackgroundSuppressReplay()
    {
        yield return Load();
        var record = new CatLifeSessionRecord();
        View("RewardCard").gameObject.SetActive(false);
        Present(record, "这一小步已经完成。\n来源：本地洞察");
        Assert.That((bool)effect.GetType().GetProperty("AwaitingVisibility").GetValue(effect), Is.True);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(Revealing, Is.False);
        View("RewardCard").gameObject.SetActive(true); Freeze(0); yield return null;
        Assert.That(Revealing, Is.True);
        View("RewardCard").gameObject.SetActive(false);
        Assert.That(Revealing, Is.False);
        Present(record, "MiMo 生成 · 仅使用会话聚合值\n温和提醒：慢慢开始。");
        View("RewardCard").gameObject.SetActive(true); yield return null;
        Assert.That(Revealing, Is.False, "Late results must not replay after the page was left.");
        var next = new CatLifeSessionRecord { aiRequestStatus = "requesting" };
        Present(next, "正在生成陪伴建议……"); Assert.That(Revealing, Is.False);
        next.aiRequestStatus = "succeeded";
        Present(next, "MiMo 生成 · 仅使用会话聚合值\n下一小步：喝口水。"); Freeze(0);
        Assert.That(Revealing, Is.True);
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        app.GetType().GetMethod("OnApplicationPause", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, new object[] { true });
        Assert.That(Revealing, Is.False);
        Present(next, text.text); Assert.That(Revealing, Is.False);
    }

    [UnityTest]
    public IEnumerator CaptureRealGlyphBlurAndScrolledMask()
    {
        string output = Environment.GetEnvironmentVariable("CATLIFE_UI_CAPTURE_DIR");
        if (string.IsNullOrEmpty(output)) Assert.Ignore("Opt-in real framebuffer evidence output not configured.");
        Directory.CreateDirectory(output);
        var builder = Type.GetType("CatLifePreviewSceneBuilder, Assembly-CSharp-Editor");
        builder.GetMethod("SetGameViewFixedResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { 1080, 2424 });
        yield return Load();
        var record = new CatLifeSessionRecord();
        const string value = "先喝一口水，再选择可以完成的小任务。保持自己的节奏，慢慢开始。\n来源：本地洞察";
        Present(record, value); Freeze(0);
        Canvas.ForceUpdateCanvases();
        UnityEditor.ShaderUtil.CompilePass(text.materialForRendering, 0, true);
        Assert.That(UnityEditor.ShaderUtil.IsPassCompiled(text.materialForRendering, 0), Is.True);
        // CanvasRenderer can select the clip variant only at draw time, after materialForRendering.
        foreach (bool clipped in new[] { false, true }) foreach (bool alphaClip in new[] { false, true })
        {
            var variant = new Material(text.material);
            if (clipped) variant.EnableKeyword("UNITY_UI_CLIP_RECT");
            if (alphaClip) variant.EnableKeyword("UNITY_UI_ALPHACLIP");
            UnityEditor.ShaderUtil.CompilePass(variant, 0, true);
            Assert.That(UnityEditor.ShaderUtil.IsPassCompiled(variant, 0), Is.True);
            UnityEngine.Object.Destroy(variant);
        }
        foreach (float elapsed in new[] { 0f, .03f, .09f, .27f, 2f })
        {
            Freeze(elapsed); yield return null; yield return null;
            yield return Capture(output, $"reveal-{elapsed * 1000:0000}ms");
        }
        string longText = string.Join("\n", Enumerable.Repeat("把大任务拆成眼前这一小步，按自己的节奏继续。", 12)) + "\n来源：本地洞察";
        Present(new CatLifeSessionRecord(), longText); Freeze(.27f); yield return null;
        yield return Capture(output, "mask-top-revealing");
        Freeze(30); yield return null;
        Field<ScrollRect>("rewardScroll").verticalNormalizedPosition = 0; yield return null;
        yield return Capture(output, "mask-bottom-complete");
        File.WriteAllText(Path.Combine(output, "SOURCE.txt"), "Actual Unity Game view, r42 isolated scene. Synthetic local insight; controlled display clock. No cloud request, no Android evidence.\n");
    }
    private static IEnumerator Capture(string output, string name)
    {
        string path = Path.Combine(output, name + ".png");
        ScreenCapture.CaptureScreenshot(path);
        double until = Time.realtimeSinceStartupAsDouble + 10;
        while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < until) yield return null;
        Assert.That(File.Exists(path), Is.True);
        yield return null;
    }
}

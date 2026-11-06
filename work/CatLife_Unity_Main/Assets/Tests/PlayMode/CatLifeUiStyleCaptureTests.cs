using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Opt-in real Game view capture, separate from functional regression counts.
public sealed class CatLifeUiStyleCaptureTests
{
    private const string Key = "CatLife.Mobile.Data.v1";
    private bool hadData;
    private string savedData;
    [SetUp] public void Preserve() { hadData = PlayerPrefs.HasKey(Key); savedData = PlayerPrefs.GetString(Key); }
    [TearDown] public void Restore()
    {
        if (hadData) PlayerPrefs.SetString(Key, savedData); else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    [UnityTest]
    public IEnumerator CaptureHomePilot()
    {
        string output = Environment.GetEnvironmentVariable("CATLIFE_UI_CAPTURE_DIR");
        if (string.IsNullOrEmpty(output)) Assert.Ignore("Set CATLIFE_UI_CAPTURE_DIR for an explicit visual capture.");
        Directory.CreateDirectory(output);
        var data = new CatLifeAppData(); data.settings.catAdopted = true;
        data.settings.aiEnabled = false; data.settings.autoFocusAdaptationSeconds = 0;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        Type installer = Type.GetType("CatLifeUiStyleInstaller, Assembly-CSharp-Editor");
        yield return (AsyncOperation)installer.GetMethod("LoadReviewForCapture").Invoke(null, null);
        yield return null;
        Type builder = Type.GetType("CatLifePreviewSceneBuilder, Assembly-CSharp-Editor");
        builder.GetMethod("SetGameViewFixedResolution", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { 1080, 2424 });
        for (int i = 0; i < 30; i++) yield return null;
        Assert.That(Screen.width, Is.EqualTo(1080)); Assert.That(Screen.height, Is.EqualTo(2424));
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CatLifeUiStyleReview"));
        string path = Path.Combine(output, "home-1080x2424.png");
        ScreenCapture.CaptureScreenshot(path, 1);
        float until = Time.realtimeSinceStartup + 10;
        while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(File.Exists(path), Is.True, "No actual framebuffer capture was written.");
        for (int i = 0; i < 5; i++) yield return null;
        Debug.Log("CATLIFE_UI_CAPTURE " + path + "; editor runtime with synthetic local data, not Android acceptance.");
    }

    private static Transform View(string name) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == name);
    private static Component App => GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
    private static void Call(string name) => App.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
        .Single(m => m.Name == name && m.GetParameters().Length == 0).Invoke(App, null);
    private static string CurrentView => (string)App.GetType().GetProperty("CurrentView").GetValue(App);
    private static IEnumerator LoadFixture(string mode)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var data = new CatLifeAppData(); data.settings.catAdopted = true;
        data.settings.aiEnabled = false; data.settings.autoFocusAdaptationSeconds = 0;
        for (int i = 0; i < 3; i++) data.records.Add(new CatLifeSessionRecord {
            completed = true, plannedSeconds = 1500, actualSeconds = 1500,
            startedAt = now - 4000 - i * 86400, endedAt = now - 2500 - i * 86400,
            longestStableSeconds = 1500
        });
        if (mode != "normal")
        {
            var controller = new CatLifeSessionController(data);
            controller.BeginTransition(mode == "reward" ? 60 : 1500, now);
            controller.EnterFocus(mode == "reward" ? now - 60 : now - 20);
        }
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        Type installer = Type.GetType("CatLifeUiStyleInstaller, Assembly-CSharp-Editor");
        yield return (AsyncOperation)installer.GetMethod("LoadReviewForCapture").Invoke(null, null);
        yield return null;
    }

    private static IEnumerator Capture(string output, string name, string expectedView)
    {
        Assert.That(CurrentView, Is.EqualTo(expectedView), name);
        yield return new WaitForSecondsRealtime(.8f);
        string path = Path.Combine(output, name + $"-{Screen.width}x{Screen.height}.png");
        ScreenCapture.CaptureScreenshot(path, 1);
        float until = Time.realtimeSinceStartup + 10;
        while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(File.Exists(path), Is.True, name);
        for (int i = 0; i < 5; i++) yield return null;
    }

    [UnityTest]
    public IEnumerator CaptureSevenPagesAndInputStates()
    {
        string output = Environment.GetEnvironmentVariable("CATLIFE_UI_CAPTURE_DIR");
        if (string.IsNullOrEmpty(output)) Assert.Ignore("Set CATLIFE_UI_CAPTURE_DIR for an explicit visual capture.");
        Directory.CreateDirectory(output);
        foreach (Vector2Int size in new[] { new Vector2Int(1080, 2424), new Vector2Int(720, 1280) })
        {
            Type builder = Type.GetType("CatLifePreviewSceneBuilder, Assembly-CSharp-Editor");
            builder.GetMethod("SetGameViewFixedResolution", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { size.x, size.y });
            yield return LoadFixture("normal");
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(size));
            yield return Capture(output, "home", "HomeHudLayer");
            View("AdoptionPanel").gameObject.SetActive(true);
            yield return Capture(output, "name-unfocused", "HomeHudLayer");
            var name = View("CatNameInput").GetComponent<InputField>(); name.ActivateInputField();
            yield return null; yield return null;
            yield return Capture(output, "name-focused", "HomeHudLayer");
            name.DeactivateInputField(); View("AdoptionPanel").gameObject.SetActive(false);
            View("StartButton").GetComponent<Button>().onClick.Invoke();
            yield return Capture(output, "setup", "SetupPanel");
            var minutes = View("CustomMinutes").GetComponent<InputField>(); minutes.text = "25"; minutes.ActivateInputField();
            yield return null; yield return null;
            yield return Capture(output, "minutes-focused", "SetupPanel");
            minutes.DeactivateInputField();
            Call("ShowHome"); Call("ShowRecords"); yield return Capture(output, "records", "RecordsPanel");
            Call("ShowHome"); Call("ShowGrowth"); yield return Capture(output, "growth", "GrowthPanel");
            Call("ShowHome"); Call("ShowSettings"); yield return Capture(output, "settings", "SettingsPanel");
            yield return LoadFixture("focus");
            yield return Capture(output, "focus", "FocusPanel");
            yield return LoadFixture("reward");
            float until = Time.realtimeSinceStartup + 15;
            while ((CurrentView != "RewardPanel" || !View("RewardCard").gameObject.activeInHierarchy) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(View("RewardCard").gameObject.activeInHierarchy, Is.True, "Reward view is still awaiting the real wake-up animation.");
            yield return Capture(output, "reward", "RewardPanel");
        }
        File.WriteAllText(Path.Combine(output, "SOURCE.txt"), "Unity Game view, UiStyleCandidate scene; synthetic local records and restored/expired focus fixtures; AI disabled. Not Android or real-session acceptance.\n");
    }

    [UnityTest]
    public IEnumerator CaptureSettingsBottomAtBothResolutions()
    {
        string output = Environment.GetEnvironmentVariable("CATLIFE_UI_CAPTURE_DIR");
        if (string.IsNullOrEmpty(output)) Assert.Ignore("Set CATLIFE_UI_CAPTURE_DIR for an explicit visual capture.");
        Directory.CreateDirectory(output);
        foreach (Vector2Int size in new[] { new Vector2Int(1080, 2424), new Vector2Int(720, 1280) })
        {
            Type builder = Type.GetType("CatLifePreviewSceneBuilder, Assembly-CSharp-Editor");
            builder.GetMethod("SetGameViewFixedResolution", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { size.x, size.y });
            yield return LoadFixture("normal"); Call("ShowSettings"); yield return null;
            Canvas.ForceUpdateCanvases();
            View("SettingsSheet").GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
            yield return Capture(output, "settings-bottom", "SettingsPanel");
        }
    }

    [UnityTest]
    public IEnumerator CapturePressedAndDisabledButton()
    {
        string output = Environment.GetEnvironmentVariable("CATLIFE_UI_CAPTURE_DIR");
        if (string.IsNullOrEmpty(output)) Assert.Ignore("Set CATLIFE_UI_CAPTURE_DIR for an explicit visual capture.");
        Directory.CreateDirectory(output);
        Type builder = Type.GetType("CatLifePreviewSceneBuilder, Assembly-CSharp-Editor");
        builder.GetMethod("SetGameViewFixedResolution", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { 1080, 2424 });
        yield return LoadFixture("normal");
        var button = View("StartButton").GetComponent<Button>();
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        button.OnPointerEnter(pointer); button.OnPointerDown(pointer);
        yield return Capture(output, "home-pressed", "HomeHudLayer");
        button.OnPointerUp(pointer); button.OnPointerExit(pointer); button.interactable = false;
        yield return Capture(output, "home-disabled", "HomeHudLayer");
    }
}

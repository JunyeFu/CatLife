using System.IO;
using CatLife.CameraControls;
using CatLife.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CatLifePreviewSceneBuilder
{
    private const int ReferenceWidth = 1080;
    private const int ReferenceHeight = 2400;
    private const string UiFolder = "Assets/UI/CatLifeHome";
    private const string EnvironmentFolder = "Assets/Art/Environment";
    private const string VolumeProfilePath = "Assets/Settings/CatLifePreviewVolumeProfile.asset";
    private const string SkyTexturePath = "Assets/Art/Environment/CatLifePreviewSky.png";
    private const string SkyMaterialPath = "Assets/Art/Environment/CatLifePreviewSky_Unlit.mat";
    private const string ScreenshotPath = "Assets/Screenshots/catlife-home-preview-match.png";
    private const string CatModelPath = "Assets/Art/Cat/Animations/CatLife_cat_10_actions_final_state.fbx";
    private const string CatMaterialPath = "Assets/Art/Cat/Materials/CatLife_OrangeCat_Textured.mat";
    private const string CatBaseMapPath = "Assets/Art/Cat/Textures/Meshy_AI_Low_Poly_Orange_Cat_quadruped_texture_0.png";
    private const string CatNormalMapPath = "Assets/Art/Cat/Textures/Meshy_AI_Low_Poly_Orange_Cat_quadruped_texture_0_normal.png";
    private const string CatMetallicMapPath = "Assets/Art/Cat/Textures/Meshy_AI_Low_Poly_Orange_Cat_quadruped_texture_0_metallic.png";
    private const string CatRoughnessMapPath = "Assets/Art/Cat/Textures/Meshy_AI_Low_Poly_Orange_Cat_quadruped_texture_0_roughness.png";
    private const string CatMetallicSmoothnessPath = "Assets/Art/Cat/Textures/CatLife_OrangeCat_MetallicSmoothness.png";

    private static readonly Color White = new Color(1f, 1f, 1f, 1f);
    private static readonly Color WarmGold = Hex("F6C443");
    private static readonly Color AccentGold = Hex("FFC72F");
    private static readonly Color AccentOrange = Hex("FF9824");
    private static readonly Color UiTextCream = Hex("FFF8E8");
    private static readonly Color StartTextOutline = new Color(0.78f, 0.34f, 0.08f, 0.42f);
    private static readonly Color MenuButtonFill = new Color(1f, 0.70f, 0.18f, 0.22f);
    private static readonly Color MenuButtonOutline = new Color(1f, 0.90f, 0.46f, 1f);
    private static readonly Color MenuTextShadow = new Color(0.46f, 0.27f, 0.06f, 0.34f);
    private static readonly Color SliderGlass = new Color(1f, 1f, 1f, 0.22f);
    private static readonly Color SliderHighlight = new Color(1f, 1f, 1f, 0.24f);
    private static readonly Color SliderOutline = new Color(1f, 0.92f, 0.54f, 0.9f);
    private static readonly Color SliderHandle = new Color(1f, 0.78f, 0.22f, 0.36f);
    private static readonly Color SliderHandleOutline = new Color(1f, 0.88f, 0.44f, 1f);
    private static readonly Vector3 PlazaCameraPosition = new Vector3(0.1f, 1.9f, 1.2f);
    private static readonly Vector3 SunLightPosition = new Vector3(0f, 9.5f, 0f);
    private static readonly Vector3 SunLightRayDirection = new Vector3(0.2f, -1f, -0.98f).normalized;
    private const float PlazaCameraYaw = 182.601f;
    private const float PlazaCameraPitch = 6.654f;
    private const float PlazaCameraRoll = 0.362f;
    private const float PlazaCameraFov = 80f;
    private const float PlazaCameraMaxPitchOffset = 45f;
    private const float PlazaCameraDegreesPerSecond = 10f;
    private const float TownCatScale = 0.0275f;

    [MenuItem("CatLife/Build Preview Home Scene")]
    public static void BuildMenu()
    {
        Build();
    }

    [MenuItem("CatLife/Capture Preview Home Screenshot")]
    public static void CaptureMenu()
    {
        Capture();
    }

    public static void Build()
    {
        EnsureFolders();

        SpriteSet sprites = BuildSprites();
        Material skyMaterial = BuildSkyMaterial();
        Material catMaterial = BuildCatMaterial();

        ConfigureCameraAndCat(catMaterial);
        ConfigureSceneObjects(skyMaterial);
        ConfigureLighting();
        ConfigureVolume();
        BuildCanvas(sprites);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static string Capture()
    {
        EnsureFolders();
        SetGameViewFixedResolution(ReferenceWidth, ReferenceHeight);
        EditorApplication.QueuePlayerLoopUpdate();
        ScreenCapture.CaptureScreenshot(ScreenshotPath, 1);
        return ScreenshotPath;
    }

    private static void ConfigureSceneObjects(Material skyMaterial)
    {
        DestroyNamed("CatLifePreviewSkyBackdrop");

        GameObject sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sky.name = "CatLifePreviewSkyBackdrop";
        Camera camera = Camera.main;
        if (camera != null)
        {
            float distance = Mathf.Min(camera.farClipPlane - 5f, 126f);
            float height = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float width = height * 2.25f;
            sky.transform.SetParent(camera.transform, false);
            sky.transform.localPosition = new Vector3(0f, 0f, distance);
            sky.transform.localRotation = Quaternion.identity;
            sky.transform.localScale = new Vector3(width * 1.18f, height * 1.18f, 1f);
        }
        else
        {
            sky.transform.position = new Vector3(0f, 11.5f, 24f);
            sky.transform.rotation = Quaternion.identity;
            sky.transform.localScale = new Vector3(82f, 52f, 1f);
        }

        Collider collider = sky.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        MeshRenderer renderer = sky.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = skyMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        GameObject town = GameObject.Find("CatLifeTownRoot");
        if (town != null)
        {
            foreach (Renderer r in town.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }
    }

    private static void ConfigureLighting()
    {
        Light sun = null;
        GameObject lightGo = GameObject.Find("Main Directional Light");
        if (lightGo == null)
        {
            GameObject lightingRoot = GameObject.Find("CatLifeMainRoot/Lighting");
            lightGo = new GameObject("Main Directional Light");
            if (lightingRoot != null)
            {
                lightGo.transform.SetParent(lightingRoot.transform, false);
            }
        }

        if (lightGo != null)
        {
            sun = lightGo.GetComponent<Light>();
            if (sun == null)
            {
                sun = lightGo.AddComponent<Light>();
            }

            lightGo.name = "Main Directional Light";
            lightGo.transform.position = SunLightPosition;
            lightGo.transform.rotation = Quaternion.LookRotation(SunLightRayDirection, Vector3.up);
        }

        if (sun != null)
        {
            sun.enabled = true;
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.91f, 0.74f, 1f);
            sun.intensity = 1.42f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.62f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.28f;
            RenderSettings.sun = sun;
        }

        RemoveExtraSceneLights(sun);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.78f, 1f, 1f);
        RenderSettings.ambientEquatorColor = new Color(1f, 0.78f, 0.49f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.45f, 0.31f, 0.19f, 1f);
        RenderSettings.reflectionIntensity = 0.22f;
        RenderSettings.fog = false;
    }

    private static void RemoveExtraSceneLights(Light sun)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null || light == sun || light.gameObject.scene != activeScene)
            {
                continue;
            }

            GameObject lightObject = light.gameObject;
            Component[] components = lightObject.GetComponents<Component>();
            if (lightObject.transform.childCount == 0 && components.Length <= 3)
            {
                Object.DestroyImmediate(lightObject);
            }
            else
            {
                Object.DestroyImmediate(light);
            }
        }
    }

    private static void ConfigureVolume()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }

        for (int i = profile.components.Count - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(profile.components[i], true);
        }
        profile.components.Clear();

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.22f);
        bloom.threshold.Override(1.18f);
        bloom.scatter.Override(0.48f);

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(-0.22f);
        color.contrast.Override(4f);
        color.saturation.Override(-12f);
        color.colorFilter.Override(new Color(1f, 0.995f, 0.98f, 1f));

        DepthOfField depth = profile.Add<DepthOfField>(true);
        depth.mode.Override(DepthOfFieldMode.Gaussian);
        depth.gaussianStart.Override(29f);
        depth.gaussianEnd.Override(55f);
        depth.gaussianMaxRadius.Override(0.58f);
        depth.highQualitySampling.Override(true);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.color.Override(new Color(0.34f, 0.16f, 0.02f, 1f));
        vignette.intensity.Override(0.08f);
        vignette.smoothness.Override(0.36f);

        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

        GameObject volumeGo = GameObject.Find("Global Volume");
        if (volumeGo != null)
        {
            Volume volume = volumeGo.GetComponent<Volume>();
            if (volume == null)
            {
                volume = volumeGo.AddComponent<Volume>();
            }

            volume.isGlobal = true;
            volume.priority = 2f;
            volume.sharedProfile = profile;
            volume.profile = profile;
        }

        EditorUtility.SetDirty(profile);
    }

    private static void ConfigureCameraAndCat(Material catMaterial)
    {
        GameObject cat = EnsureCatCompanionModel();
        if (cat != null)
        {
            cat.transform.position = new Vector3(-0.3f, 0.03f, -6.78f);
            cat.transform.rotation = Quaternion.Euler(0f, -0.017f, 0f);
            cat.transform.localScale = Vector3.one * TownCatScale;
            ApplyCatMaterial(cat, catMaterial);
            CatLifeCatTownWalkerSetup.ConfigureSceneCat(cat);
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        camera.transform.position = PlazaCameraPosition;
        camera.transform.rotation = Quaternion.Euler(PlazaCameraPitch, PlazaCameraYaw, PlazaCameraRoll);
        camera.fieldOfView = PlazaCameraFov;
        camera.nearClipPlane = 0.04f;
        camera.farClipPlane = 130f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Hex("76D7F3");
        camera.allowHDR = true;
        camera.allowMSAA = true;

        CatLifePlazaCameraRotator rotator = camera.GetComponent<CatLifePlazaCameraRotator>();
        if (rotator == null)
        {
            rotator = camera.gameObject.AddComponent<CatLifePlazaCameraRotator>();
        }

        rotator.FixedPosition = PlazaCameraPosition;
        rotator.BasePitchDegrees = PlazaCameraPitch;
        rotator.MaxPitchOffsetDegrees = PlazaCameraMaxPitchOffset;
        rotator.YawDegrees = PlazaCameraYaw;
        rotator.PitchDegrees = PlazaCameraPitch;
        rotator.RollDegrees = PlazaCameraRoll;
        rotator.DegreesPerSecond = PlazaCameraDegreesPerSecond;
        rotator.ApplyPose();
        EditorUtility.SetDirty(rotator);

        UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
        if (data != null)
        {
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.requiresDepthTexture = true;
            data.requiresColorTexture = true;
        }
    }

    private static void BuildCanvas(SpriteSet sprites)
    {
        DestroyNamed("CatLifeHomeCanvas");

        Camera camera = Camera.main;

        GameObject canvasGo = new GameObject("CatLifeHomeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CatLifeHomeFontBinder));
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
        {
            canvasGo.layer = uiLayer;
        }

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        EnsureEventSystem();

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);

        Font font = ResolveFont();

        Image topDot = AddImage("TopAccentDot", canvasRect, sprites.dot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -70f), new Vector2(20f, 20f), AccentOrange);
        topDot.raycastTarget = false;

        Text title = AddText("Title", canvasRect, "CatLife", font, 52, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, -61f), new Vector2(290f, 58f));
        AddTextShadow(title, 0.28f, new Vector2(0f, -2f));

        Text subtitle = AddText("FocusMinutes", canvasRect, "\u4eca\u5929\u5df2\u4e13\u6ce8 <color=#FFD14A>48</color> \u5206\u949f", font, 27, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(74f, -118f), new Vector2(320f, 44f));
        subtitle.supportRichText = true;
        AddTextShadow(subtitle, 0.25f, new Vector2(0f, -2f));

        GameObject pill = AddPanel("FocusPill", canvasRect, sprites.roundedSolid, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-38f, -66f), new Vector2(264f, 70f), new Color(1f, 0.78f, 0.22f, 0.01f));
        AddImage("FocusPillOutline", pill.transform, sprites.roundedOutline, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(264f, 70f), new Color(1f, 0.93f, 0.5f, 0.92f));
        AddImage("ClockDisc", pill.transform, sprites.dot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(42f, 42f), AccentOrange);
        AddImage("ClockIcon", pill.transform, sprites.clock, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(25f, 25f), White);
        Text pillText = AddText("FocusPillText", pill.transform, "\u4e13\u6ce8\u4e2d <color=#FFD14A>25:09</color>", font, 25, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(190f, 50f));
        pillText.supportRichText = true;
        AddTextShadow(pillText, 0.23f, new Vector2(0f, -1.5f));

        const float rightMenuX = -116f;
        GameObject catMenu = AddCircleMenu(canvasRect, sprites.cat, font, "\u732b\u54aa", rightMenuX, 834f, sprites);
        GameObject recordMenu = AddCircleMenu(canvasRect, sprites.record, font, "\u8bb0\u5f55", rightMenuX, 661f, sprites);
        GameObject settingsMenu = AddCircleMenu(canvasRect, sprites.settings, font, "\u8bbe\u7f6e", rightMenuX, 315f, sprites);
        AddRotationSliderMenu(canvasRect, sprites.rotate, font, "\u65cb\u8f6c", rightMenuX, 488f, sprites, camera != null ? camera.GetComponent<CatLifePlazaCameraRotator>() : null, new[] { catMenu, recordMenu, settingsMenu });

        AddImage("PageDotActive", canvasRect, sprites.dot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-28f, 194f), new Vector2(18f, 18f), AccentOrange);
        AddImage("PageDotA", canvasRect, sprites.dot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(2f, 194f), new Vector2(14f, 14f), new Color(1f, 1f, 1f, 0.92f));
        AddImage("PageDotB", canvasRect, sprites.dot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(30f, 194f), new Vector2(14f, 14f), new Color(1f, 1f, 1f, 0.92f));

        GameObject button = AddPanel("StartFocusButton", canvasRect, sprites.button, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(690f, 116f), White);
        button.GetComponent<Image>().type = Image.Type.Simple;
        button.AddComponent<Button>();
        AddImage("ButtonSpark", button.transform, sprites.spark, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-158f, 1f), new Vector2(40f, 40f), UiTextCream);
        Text buttonText = AddText("StartFocusText", button.transform, "\u5f00\u59cb\u4e13\u6ce8", font, 47, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(24f, 1f), new Vector2(330f, 72f));
        buttonText.color = UiTextCream;
        AddTextOutline(buttonText, StartTextOutline, new Vector2(1.2f, -1.2f));
        AddTextShadow(buttonText, 0.24f, new Vector2(0f, -2.2f));

        PlaceholderUi placeholder = AddPlaceholderOverlay(canvasRect, sprites, font);
        ConfigureHomeUiController(canvasGo, subtitle, pillText, button.GetComponent<Button>(), catMenu, recordMenu, settingsMenu, placeholder, sprites);

        canvasGo.GetComponent<CatLifeHomeFontBinder>().Apply();
        if (uiLayer >= 0)
        {
            SetLayerRecursively(canvasGo, uiLayer);
        }
    }

    private static GameObject AddCircleMenu(Transform parent, Sprite icon, Font font, string label, float x, float y, SpriteSet sprites)
    {
        GameObject root = new GameObject("MenuGroup_" + label, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(x, y);
        rootRect.sizeDelta = new Vector2(158f, 142f);

        GameObject button = AddPanel("Menu_" + label, root.transform, sprites.circleSolid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(94f, 94f), MenuButtonFill);
        button.AddComponent<Button>();
        AddImage("Outline", button.transform, sprites.circleOutline, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(94f, 94f), MenuButtonOutline);
        AddImage("Icon", button.transform, icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f), UiTextCream);

        Text text = AddText("Label", root.transform, label, font, 25, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(158f, 36f));
        text.color = UiTextCream;
        AddTextOutline(text, MenuTextShadow, new Vector2(0.8f, -0.8f));
        AddTextShadow(text, 0.22f, new Vector2(0f, -1.6f));
        return root;
    }

    private static void AddRotationSliderMenu(Transform parent, Sprite icon, Font font, string label, float x, float y, SpriteSet sprites, CatLifePlazaCameraRotator rotator, GameObject[] hideWhileHeld)
    {
        GameObject root = new GameObject("MenuGroup_" + label, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(x, y);
        rootRect.sizeDelta = new Vector2(260f, 260f);

        GameObject controls = new GameObject("RotationSliderControls", typeof(RectTransform));
        controls.transform.SetParent(root.transform, false);
        RectTransform controlsRect = controls.GetComponent<RectTransform>();
        controlsRect.anchorMin = new Vector2(0.5f, 0.5f);
        controlsRect.anchorMax = new Vector2(0.5f, 0.5f);
        controlsRect.pivot = new Vector2(0.5f, 0.5f);
        controlsRect.anchoredPosition = Vector2.zero;
        controlsRect.sizeDelta = new Vector2(260f, 260f);

        GameObject horizontalTrack = AddPanel("CameraYawSlider", controls.transform, sprites.roundedSolid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(232f, 78f), SliderGlass);
        horizontalTrack.GetComponent<Image>().raycastTarget = false;
        AddImage("GlassHighlight", horizontalTrack.transform, sprites.roundedSolid, new Vector2(0.5f, 0.74f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(196f, 20f), SliderHighlight);
        AddImage("SlotOutline", horizontalTrack.transform, sprites.roundedOutline, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(232f, 78f), SliderOutline);
        AddImage("LeftTick", horizontalTrack.transform, sprites.dot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-78f, 0f), new Vector2(8f, 8f), new Color(1f, 1f, 1f, 0.52f));
        AddImage("CenterTick", horizontalTrack.transform, sprites.dot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f), new Color(1f, 1f, 1f, 0.68f));
        AddImage("RightTick", horizontalTrack.transform, sprites.dot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(78f, 0f), new Vector2(8f, 8f), new Color(1f, 1f, 1f, 0.52f));

        GameObject horizontalHandle = AddPanel("YawSliderHandle", horizontalTrack.transform, sprites.circleSolid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78f, 78f), SliderHandle);
        horizontalHandle.GetComponent<Image>().raycastTarget = false;
        AddImage("HandleOutline", horizontalHandle.transform, sprites.circleOutline, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78f, 78f), SliderHandleOutline);

        GameObject verticalTrack = AddPanel("CameraPitchSlider", controls.transform, sprites.roundedSolid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78f, 232f), new Color(1f, 1f, 1f, 0f));
        verticalTrack.GetComponent<Image>().raycastTarget = false;
        GameObject verticalGlass = AddPanel("VerticalGlass", verticalTrack.transform, sprites.roundedSolid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(232f, 78f), SliderGlass);
        verticalGlass.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        verticalGlass.GetComponent<Image>().raycastTarget = false;

        Image verticalHighlight = AddImage("GlassHighlight", verticalTrack.transform, sprites.roundedSolid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(196f, 20f), SliderHighlight);
        verticalHighlight.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        Image verticalOutline = AddImage("SlotOutline", verticalTrack.transform, sprites.roundedOutline, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(232f, 78f), SliderOutline);
        verticalOutline.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        AddImage("UpTick", verticalTrack.transform, sprites.dot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 78f), new Vector2(8f, 8f), new Color(1f, 1f, 1f, 0.52f));
        AddImage("CenterTick", verticalTrack.transform, sprites.dot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f), new Color(1f, 1f, 1f, 0.68f));
        AddImage("DownTick", verticalTrack.transform, sprites.dot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(8f, 8f), new Color(1f, 1f, 1f, 0.52f));

        GameObject verticalHandle = AddPanel("PitchSliderHandle", verticalTrack.transform, sprites.circleSolid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f), SliderHandle);
        verticalHandle.GetComponent<Image>().raycastTarget = false;
        AddImage("HandleOutline", verticalHandle.transform, sprites.circleOutline, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f), SliderHandleOutline);

        GameObject button = AddPanel("Menu_" + label, root.transform, sprites.circleSolid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(94f, 94f), MenuButtonFill);
        button.AddComponent<Button>();
        AddImage("Outline", button.transform, sprites.circleOutline, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(94f, 94f), MenuButtonOutline);
        AddImage("Icon", button.transform, icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(47f, 47f), UiTextCream);

        CatLifeCameraRotationSlider slider = button.AddComponent<CatLifeCameraRotationSlider>();
        SerializedObject serialized = new SerializedObject(slider);
        serialized.FindProperty("cameraRotator").objectReferenceValue = rotator;
        serialized.FindProperty("horizontalTrack").objectReferenceValue = horizontalTrack.GetComponent<RectTransform>();
        serialized.FindProperty("horizontalHandle").objectReferenceValue = horizontalHandle.GetComponent<RectTransform>();
        serialized.FindProperty("verticalTrack").objectReferenceValue = verticalTrack.GetComponent<RectTransform>();
        serialized.FindProperty("verticalHandle").objectReferenceValue = verticalHandle.GetComponent<RectTransform>();
        serialized.FindProperty("controlsRoot").objectReferenceValue = controls;
        SerializedProperty hideProperty = serialized.FindProperty("hideWhileHeld");
        hideProperty.arraySize = hideWhileHeld.Length;
        for (int i = 0; i < hideWhileHeld.Length; i++)
        {
            hideProperty.GetArrayElementAtIndex(i).objectReferenceValue = hideWhileHeld[i];
        }
        serialized.FindProperty("longPressSeconds").floatValue = 0.22f;
        serialized.FindProperty("deadZonePixels").floatValue = 8f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        controls.SetActive(false);

        Text text = AddText("Label", root.transform, label, font, 25, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(158f, 36f));
        text.color = UiTextCream;
        AddTextOutline(text, MenuTextShadow, new Vector2(0.8f, -0.8f));
        AddTextShadow(text, 0.22f, new Vector2(0f, -1.6f));
    }

    private static PlaceholderUi AddPlaceholderOverlay(Transform parent, SpriteSet sprites, Font font)
    {
        GameObject overlay = AddPanel("PlaceholderOverlay", parent, sprites.roundedSolid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight), new Color(0.97f, 0.91f, 0.73f, 0.98f));
        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.sprite = null;
        overlayImage.type = Image.Type.Simple;
        overlay.transform.SetAsLastSibling();

        GameObject skyBand = AddPanel("PageSkyBand", overlay.transform, sprites.roundedSolid, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(ReferenceWidth, 430f), new Color(0.22f, 0.69f, 0.89f, 0.96f));
        Image skyImage = skyBand.GetComponent<Image>();
        skyImage.sprite = null;
        skyImage.type = Image.Type.Simple;
        AddPanel("PageSunGlow", skyBand.transform, sprites.circleSolid, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-126f, -120f), new Vector2(210f, 210f), new Color(1f, 0.86f, 0.35f, 0.22f));

        Text eyebrow = AddText("PlaceholderEyebrow", overlay.transform, "CatLife", font, 28, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, -62f), new Vector2(260f, 42f));
        eyebrow.color = new Color(1f, 1f, 1f, 0.92f);
        AddTextShadow(eyebrow, 0.18f, new Vector2(0f, -1.2f));

        Text pageStatus = AddText("PlaceholderPageStatus", overlay.transform, "\u4eca\u5929\u5df2\u4e13\u6ce8 <color=#FFD14A>48</color> \u5206\u949f", font, 24, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, -106f), new Vector2(360f, 42f));
        pageStatus.color = White;
        pageStatus.supportRichText = true;
        AddTextShadow(pageStatus, 0.18f, new Vector2(0f, -1.2f));

        GameObject pageBase = AddPanel("PlaceholderPageBase", overlay.transform, sprites.roundedSolid, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -148f), new Vector2(936f, 2130f), new Color(1f, 0.96f, 0.78f, 0.96f));
        Image pageBaseImage = pageBase.GetComponent<Image>();
        pageBaseImage.sprite = null;
        pageBaseImage.type = Image.Type.Simple;

        Text title = AddText("PlaceholderTitle", pageBase.transform, "\u732b\u54aa", font, 58, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -54f), new Vector2(620f, 82f));
        title.color = new Color(0.42f, 0.25f, 0.12f, 1f);

        GameObject headerChip = AddPanel("PlaceholderPageChip", pageBase.transform, sprites.roundedSolid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -152f), new Vector2(410f, 62f), new Color(1f, 0.78f, 0.22f, 0.22f));
        Image chipOutline = AddImage("PlaceholderChipOutline", headerChip.transform, sprites.roundedOutline, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(410f, 62f), new Color(1f, 0.88f, 0.44f, 0.92f));
        chipOutline.type = Image.Type.Sliced;
        Text chipText = AddText("PlaceholderChipText", headerChip.transform, "\u6e38\u620f\u6a21\u5757\u9884\u89c8", font, 25, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 44f));
        chipText.color = White;

        GameObject close = AddPanel("PlaceholderCloseButton", overlay.transform, sprites.circleSolid, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-78f, -94f), new Vector2(72f, 72f), new Color(1f, 0.77f, 0.22f, 0.18f));
        Button closeButton = close.AddComponent<Button>();
        AddImage("CloseOutline", close.transform, sprites.circleOutline, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f), new Color(1f, 0.9f, 0.48f, 1f));
        Text closeText = AddText("CloseText", close.transform, "\u00d7", font, 38, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(48f, 48f));
        closeText.color = White;

        GameObject heroPanel = AddPanel("PlaceholderHeroPanel", pageBase.transform, sprites.roundedSolid, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(824f, 210f), new Color(1f, 0.78f, 0.27f, 0.30f));
        Image heroOutline = AddImage("PlaceholderHeroOutline", heroPanel.transform, sprites.roundedOutline, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(824f, 210f), new Color(1f, 0.76f, 0.28f, 0.76f));
        heroOutline.type = Image.Type.Sliced;
        Image heroIcon = AddImage("PlaceholderHeroIcon", heroPanel.transform, sprites.cat, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(108f, 108f), White);
        Text heroText = AddText("PlaceholderHeroText", heroPanel.transform, "\u67e5\u770b\u5f53\u524d\u966a\u4f34\u72b6\u6001\u3001\u6210\u957f\u503c\u548c\u5df2\u89e3\u9501\u52a8\u4f5c\u3002", font, 28, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(182f, 0f), new Vector2(590f, 124f));
        heroText.color = new Color(0.43f, 0.25f, 0.1f, 1f);
        heroText.horizontalOverflow = HorizontalWrapMode.Wrap;

        Text body = AddText("PlaceholderBody", pageBase.transform, "\u5f53\u524d\u72b6\u6001", font, 31, FontStyle.Bold, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -540f), new Vector2(816f, 1320f));
        body.color = new Color(0.35f, 0.22f, 0.12f, 1f);
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Overflow;
        body.lineSpacing = 1.22f;
        body.supportRichText = true;

        overlay.SetActive(false);
        return new PlaceholderUi
        {
            overlay = overlay,
            pageStatusText = pageStatus,
            titleText = title,
            chipText = chipText,
            heroText = heroText,
            heroIcon = heroIcon,
            bodyText = body,
            closeButton = closeButton
        };
    }

    private static void ConfigureHomeUiController(GameObject canvasGo, Text todayFocusText, Text focusPillText, Button startFocusButton, GameObject catMenu, GameObject recordMenu, GameObject settingsMenu, PlaceholderUi placeholder, SpriteSet sprites)
    {
        CatLifeHomeUiController controller = canvasGo.GetComponent<CatLifeHomeUiController>();
        if (controller == null)
        {
            controller = canvasGo.AddComponent<CatLifeHomeUiController>();
        }

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("todayFocusText").objectReferenceValue = todayFocusText;
        serialized.FindProperty("focusPillText").objectReferenceValue = focusPillText;
        serialized.FindProperty("startFocusButton").objectReferenceValue = startFocusButton;
        serialized.FindProperty("catButton").objectReferenceValue = FindMenuButton(catMenu);
        serialized.FindProperty("recordButton").objectReferenceValue = FindMenuButton(recordMenu);
        serialized.FindProperty("settingsButton").objectReferenceValue = FindMenuButton(settingsMenu);
        serialized.FindProperty("closePlaceholderButton").objectReferenceValue = placeholder.closeButton;
        serialized.FindProperty("placeholderOverlay").objectReferenceValue = placeholder.overlay;
        serialized.FindProperty("placeholderPageStatusText").objectReferenceValue = placeholder.pageStatusText;
        serialized.FindProperty("placeholderTitleText").objectReferenceValue = placeholder.titleText;
        serialized.FindProperty("placeholderChipText").objectReferenceValue = placeholder.chipText;
        serialized.FindProperty("placeholderHeroText").objectReferenceValue = placeholder.heroText;
        serialized.FindProperty("placeholderBodyText").objectReferenceValue = placeholder.bodyText;
        serialized.FindProperty("placeholderHeroIcon").objectReferenceValue = placeholder.heroIcon;
        serialized.FindProperty("catPageIcon").objectReferenceValue = sprites.cat;
        serialized.FindProperty("recordPageIcon").objectReferenceValue = sprites.record;
        serialized.FindProperty("settingsPageIcon").objectReferenceValue = sprites.settings;
        serialized.FindProperty("initialTodayFocusMinutes").intValue = 48;
        serialized.FindProperty("focusSessionSeconds").intValue = 1509;
        serialized.FindProperty("startTimerOnEnable").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static Button FindMenuButton(GameObject menuRoot)
    {
        return menuRoot != null ? menuRoot.GetComponentInChildren<Button>(true) : null;
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
        GameObject eventSystemObject;
        if (eventSystem == null)
        {
            eventSystemObject = new GameObject("CatLifeEventSystem", typeof(EventSystem));
        }
        else
        {
            eventSystemObject = eventSystem.gameObject;
            eventSystemObject.name = "CatLifeEventSystem";
        }

        foreach (StandaloneInputModule legacyModule in eventSystemObject.GetComponents<StandaloneInputModule>())
        {
            Object.DestroyImmediate(legacyModule);
        }

        if (eventSystemObject.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        EditorUtility.SetDirty(eventSystemObject);
    }

    private static GameObject AddPanel(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        return go;
    }

    private static Image AddImage(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text AddText(string name, Transform parent, string text, Font font, int size, FontStyle style, TextAnchor alignment, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 dimensions)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;

        Text label = go.GetComponent<Text>();
        label.text = text;
        label.font = font;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = White;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private static void AddTextShadow(Text text, float alpha, Vector2 distance)
    {
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.27f, 0.1f, 0f, alpha);
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    private static void AddTextOutline(Text text, Color color, Vector2 distance)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static Material BuildSkyMaterial()
    {
        Texture2D sky = MakeSkyTexture(1024, 1536);
        SavePng(sky, SkyTexturePath, false, Vector4.zero);

        Texture2D skyAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexturePath);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Texture");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, SkyMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetTexture("_BaseMap", skyAsset);
        material.SetTexture("_MainTex", skyAsset);
        material.SetColor("_BaseColor", White);
        material.SetInt("_Cull", 0);
        material.renderQueue = 2000;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material BuildCatMaterial()
    {
        ConfigureCatTextureImporter(CatBaseMapPath, TextureImporterType.Default, true);
        ConfigureCatTextureImporter(CatNormalMapPath, TextureImporterType.NormalMap, false);
        ConfigureCatTextureImporter(CatMetallicMapPath, TextureImporterType.Default, false);
        ConfigureCatTextureImporter(CatRoughnessMapPath, TextureImporterType.Default, false);

        string metallicSmoothnessPath = BuildCatMetallicSmoothnessMap();
        ConfigureCatTextureImporter(metallicSmoothnessPath, TextureImporterType.Default, false);

        Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(CatBaseMapPath);
        Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(CatNormalMapPath);
        Texture2D metallicSmoothnessMap = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicSmoothnessPath);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(CatMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, CatMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        SetTextureIfPresent(material, "_BaseMap", baseMap);
        SetTextureIfPresent(material, "_MainTex", baseMap);
        SetTextureIfPresent(material, "_BumpMap", normalMap);
        SetTextureIfPresent(material, "_MetallicGlossMap", metallicSmoothnessMap);
        SetColorIfPresent(material, "_BaseColor", White);
        SetColorIfPresent(material, "_Color", White);
        SetFloatIfPresent(material, "_WorkflowMode", 1f);
        SetFloatIfPresent(material, "_Metallic", 0f);
        SetFloatIfPresent(material, "_Smoothness", 0.52f);
        SetFloatIfPresent(material, "_GlossMapScale", 1f);
        SetFloatIfPresent(material, "_BumpScale", 0.75f);
        SetFloatIfPresent(material, "_SpecularHighlights", 1f);
        SetFloatIfPresent(material, "_EnvironmentReflections", 1f);

        if (normalMap != null)
        {
            material.EnableKeyword("_NORMALMAP");
        }
        if (metallicSmoothnessMap != null)
        {
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        material.renderQueue = -1;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject EnsureCatCompanionModel()
    {
        GameObject cat = GameObject.Find("CatCompanionModel");
        if (cat != null)
        {
            return cat;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(CatModelPath);
        if (model == null)
        {
            Debug.LogWarning("CatLife cat model is missing at " + CatModelPath);
            return null;
        }

        GameObject root = GameObject.Find("CatLifeMainRoot");
        if (root == null)
        {
            root = new GameObject("CatLifeMainRoot");
        }

        Transform characters = root.transform.Find("Characters");
        if (characters == null)
        {
            GameObject charactersGo = new GameObject("Characters");
            charactersGo.transform.SetParent(root.transform, false);
            characters = charactersGo.transform;
        }

        Object instance = PrefabUtility.InstantiatePrefab(model);
        cat = instance as GameObject;
        if (cat == null)
        {
            cat = Object.Instantiate(model);
        }

        cat.name = "CatCompanionModel";
        cat.transform.SetParent(characters, false);
        return cat;
    }

    private static void ApplyCatMaterial(GameObject cat, Material catMaterial)
    {
        if (catMaterial == null)
        {
            return;
        }

        Renderer[] renderers = cat.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = catMaterial;
            }
            else
            {
                for (int j = 0; j < materials.Length; j++)
                {
                    materials[j] = catMaterial;
                }
                renderer.sharedMaterials = materials;
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static void ConfigureCatTextureImporter(string assetPath, TextureImporterType textureType, bool srgb)
    {
        if (!File.Exists(ProjectFilePath(assetPath)))
        {
            return;
        }

        AssetDatabase.ImportAsset(assetPath);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;
        if (importer.textureType != textureType)
        {
            importer.textureType = textureType;
            changed = true;
        }
        if (importer.sRGBTexture != srgb)
        {
            importer.sRGBTexture = srgb;
            changed = true;
        }
        if (!importer.mipmapEnabled)
        {
            importer.mipmapEnabled = true;
            changed = true;
        }
        if (importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = false;
            changed = true;
        }
        if (importer.wrapMode != TextureWrapMode.Repeat)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static string BuildCatMetallicSmoothnessMap()
    {
        string metallicFile = ProjectFilePath(CatMetallicMapPath);
        string roughnessFile = ProjectFilePath(CatRoughnessMapPath);
        if (!File.Exists(metallicFile) || !File.Exists(roughnessFile))
        {
            return CatMetallicMapPath;
        }

        string outputFile = ProjectFilePath(CatMetallicSmoothnessPath);
        if (File.Exists(outputFile)
            && File.GetLastWriteTimeUtc(outputFile) >= File.GetLastWriteTimeUtc(metallicFile)
            && File.GetLastWriteTimeUtc(outputFile) >= File.GetLastWriteTimeUtc(roughnessFile))
        {
            return CatMetallicSmoothnessPath;
        }

        Texture2D metallic = LoadDiskPng(metallicFile, true);
        Texture2D roughness = LoadDiskPng(roughnessFile, true);
        if (metallic == null || roughness == null)
        {
            if (metallic != null)
            {
                Object.DestroyImmediate(metallic);
            }
            if (roughness != null)
            {
                Object.DestroyImmediate(roughness);
            }
            return CatMetallicMapPath;
        }

        int width = Mathf.Min(2048, metallic.width);
        int height = Mathf.Min(2048, metallic.height);
        Texture2D packed = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                Color metal = metallic.GetPixelBilinear(u, v);
                Color rough = roughness.GetPixelBilinear(u, v);
                float metalValue = metal.grayscale;
                float smoothness = 1f - rough.grayscale;
                packed.SetPixel(x, y, new Color(metalValue, metalValue, metalValue, smoothness));
            }
        }
        packed.Apply(false, false);

        Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
        File.WriteAllBytes(outputFile, packed.EncodeToPNG());
        Object.DestroyImmediate(metallic);
        Object.DestroyImmediate(roughness);
        Object.DestroyImmediate(packed);

        AssetDatabase.ImportAsset(CatMetallicSmoothnessPath);
        return CatMetallicSmoothnessPath;
    }

    private static Texture2D LoadDiskPng(string filePath, bool linear)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
        if (!texture.LoadImage(File.ReadAllBytes(filePath)))
        {
            Object.DestroyImmediate(texture);
            return null;
        }
        return texture;
    }

    private static string ProjectFilePath(string assetPath)
    {
        string relative = assetPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
    }

    private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
    {
        if (texture != null && material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
        }
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static SpriteSet BuildSprites()
    {
        SpriteSet set = new SpriteSet();

        set.dot = CreateSprite("dot", MakeCircleTexture(128, false), Vector4.zero);
        set.circleSolid = CreateSprite("circle_solid", MakeCircleTexture(160, false), Vector4.zero);
        set.circleOutline = CreateSprite("circle_outline", MakeCircleTexture(160, true), Vector4.zero);
        set.roundedSolid = CreateSprite("rounded_solid", MakeRoundedRectTexture(320, 96, 42, false), new Vector4(42, 42, 42, 42));
        set.roundedOutline = CreateSprite("rounded_outline", MakeRoundedRectTexture(320, 96, 42, true), new Vector4(42, 42, 42, 42));
        set.button = CreateSprite("start_button", MakeButtonTexture(820, 124, 58), new Vector4(58, 58, 58, 58));

        set.cat = CreateSprite("icon_cat", MakeCatIcon(), Vector4.zero);
        set.record = CreateSprite("icon_record", MakeRecordIcon(), Vector4.zero);
        set.forest = CreateSprite("icon_forest", MakeForestIcon(), Vector4.zero);
        set.rotate = CreateSprite("icon_rotate", MakeRotateIcon(), Vector4.zero);
        set.settings = CreateSprite("icon_settings", MakeSettingsIcon(), Vector4.zero);
        set.spark = CreateSprite("icon_spark", MakeSparkIcon(), Vector4.zero);
        set.clock = CreateSprite("icon_clock", MakeClockIcon(), Vector4.zero);

        return set;
    }

    private static Sprite CreateSprite(string name, Texture2D texture, Vector4 border)
    {
        string path = UiFolder + "/" + name + ".png";
        SavePng(texture, path, true, border);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
        {
            return sprite;
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            sprite = assets[i] as Sprite;
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    private static void SavePng(Texture2D texture, string assetPath, bool sprite, Vector4 border)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        File.WriteAllBytes(assetPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.sRGBTexture = true;

        if (sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
        }
        else
        {
            importer.textureType = TextureImporterType.Default;
        }

        importer.SaveAndReimport();
    }

    private static Texture2D MakeSkyTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color top = Hex("229CE8");
        Color mid = Hex("49BFEF");
        Color bottom = Hex("94DCEB");

        for (int y = 0; y < height; y++)
        {
            float t = y / (float)(height - 1);
            Color c = t < 0.58f ? Color.Lerp(bottom, mid, t / 0.58f) : Color.Lerp(mid, top, (t - 0.58f) / 0.42f);
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, c);
            }
        }

        AddCloud(texture, width * 0.10f, height * 0.92f, 0.64f, 0.72f);
        AddCloud(texture, width * 0.88f, height * 0.90f, 0.58f, 0.66f);
        AddCloud(texture, width * 0.70f, height * 0.74f, 0.72f, 0.56f);
        AddCloud(texture, width * 0.48f, height * 0.80f, 0.38f, 0.42f);
        AddCloud(texture, width * 0.07f, height * 0.66f, 0.48f, 0.42f);

        texture.Apply();
        return texture;
    }

    private static void AddCloud(Texture2D texture, float cx, float cy, float scale, float alpha)
    {
        AddEllipse(texture, cx - 100f * scale, cy - 2f * scale, 170f * scale, 74f * scale, alpha * 0.86f);
        AddEllipse(texture, cx + 10f * scale, cy + 30f * scale, 210f * scale, 112f * scale, alpha);
        AddEllipse(texture, cx + 148f * scale, cy + 6f * scale, 160f * scale, 78f * scale, alpha * 0.86f);
        AddEllipse(texture, cx + 42f * scale, cy - 28f * scale, 260f * scale, 70f * scale, alpha * 0.75f);
    }

    private static void AddEllipse(Texture2D texture, float cx, float cy, float rx, float ry, float alpha)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - rx * 1.25f));
        int maxX = Mathf.Min(texture.width - 1, Mathf.CeilToInt(cx + rx * 1.25f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - ry * 1.25f));
        int maxY = Mathf.Min(texture.height - 1, Mathf.CeilToInt(cy + ry * 1.25f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                float d = dx * dx + dy * dy;
                float a = Mathf.Clamp01(1f - Smooth01(0.52f, 1.18f, d)) * alpha;
                if (a <= 0f)
                {
                    continue;
                }

                Color old = texture.GetPixel(x, y);
                Color cloud = new Color(1f, 1f, 1f, a);
                texture.SetPixel(x, y, Color.Lerp(old, cloud, a));
            }
        }
    }

    private static Texture2D MakeCircleTexture(int size, bool outline)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        float radius = size * 0.43f;
        float stroke = size * 0.095f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha;
                if (outline)
                {
                    float ringDistance = Mathf.Abs(distance - radius);
                    alpha = ringDistance <= stroke * 0.5f ? 1f : 0f;
                }
                else
                {
                    alpha = distance <= radius ? 1f : 0f;
                }
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Texture2D MakeRoundedRectTexture(int width, int height, int radius, bool outline)
    {
        Texture2D texture = ClearTexture(width, height);
        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        Vector2 half = new Vector2(width * 0.5f - radius - 1f, height * 0.5f - radius - 1f);
        float innerInset = 6f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float d = RoundedDistance(new Vector2(x, y) - center, half, radius);
                float outerA = 1f - Smooth01(0f, 2f, d);
                float alpha = outerA;
                if (outline)
                {
                    float innerD = RoundedDistance(new Vector2(x, y) - center, new Vector2(half.x - innerInset, half.y - innerInset), radius - innerInset);
                    alpha = outerA * Smooth01(0f, 2f, innerD);
                }
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D MakeButtonTexture(int width, int height, int radius)
    {
        Texture2D texture = ClearTexture(width, height);
        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        Vector2 half = new Vector2(width * 0.5f - radius - 1f, height * 0.5f - radius - 1f);
        Color left = Hex("FF9734");
        Color mid = Hex("FFBA3E");
        Color right = Hex("FFD86A");
        Color lowerWarmth = Hex("E87422");
        Color rim = Hex("FFF0A8");

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 point = new Vector2(x, y) - center;
                float d = RoundedDistance(point, half, radius);
                float fillAlpha = 1f - Smooth01(0f, 2f, d);
                float glowAlpha = Mathf.Clamp01(1f - Smooth01(0f, 19f, d)) * 0.18f;

                if (fillAlpha <= 0f && glowAlpha <= 0f)
                {
                    continue;
                }

                float t = x / (float)(width - 1);
                float vertical = y / (float)(height - 1);
                Color gradient = t < 0.52f ? Color.Lerp(left, mid, t / 0.52f) : Color.Lerp(mid, right, (t - 0.52f) / 0.48f);
                float topGlow = Mathf.Clamp01((vertical - 0.52f) / 0.42f);
                float bottomShade = Mathf.Clamp01((0.34f - vertical) / 0.34f);
                gradient = Color.Lerp(gradient, White, topGlow * 0.10f);
                gradient = Color.Lerp(gradient, lowerWarmth, bottomShade * 0.08f);
                Color result = new Color(gradient.r, gradient.g, gradient.b, fillAlpha);
                if (d > -7f && fillAlpha > 0.1f)
                {
                    result = Color.Lerp(result, rim, 0.42f);
                    result.a = fillAlpha;
                }
                if (vertical > 0.66f && fillAlpha > 0.1f && d < -12f)
                {
                    result = Color.Lerp(result, White, 0.10f * Mathf.Clamp01((vertical - 0.66f) / 0.18f));
                    result.a = fillAlpha;
                }
                if (fillAlpha <= 0f)
                {
                    result = new Color(1f, 0.78f, 0.22f, glowAlpha * 0.72f);
                }

                texture.SetPixel(x, y, result);
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D MakeCatIcon()
    {
        Texture2D texture = ClearTexture(128, 128);
        FillTriangle(texture, new Vector2(31, 68), new Vector2(46, 103), new Vector2(60, 73), White);
        FillTriangle(texture, new Vector2(68, 73), new Vector2(82, 103), new Vector2(98, 68), White);
        FillCircle(texture, 64, 60, 34, White);
        FillCircle(texture, 52, 61, 4, new Color(0f, 0f, 0f, 0f));
        FillCircle(texture, 76, 61, 4, new Color(0f, 0f, 0f, 0f));
        texture.Apply();
        return texture;
    }

    private static Texture2D MakeRecordIcon()
    {
        Texture2D texture = ClearTexture(128, 128);
        FillRect(texture, 34, 63, 18, 38, White);
        FillRect(texture, 57, 45, 18, 56, White);
        FillRect(texture, 80, 28, 18, 73, White);
        texture.Apply();
        return texture;
    }

    private static Texture2D MakeForestIcon()
    {
        Texture2D texture = ClearTexture(128, 128);
        StrokeLine(texture, new Vector2(64, 100), new Vector2(64, 35), 7f, White);
        FillEllipse(texture, 49, 56, 24, 36, White);
        FillEllipse(texture, 80, 50, 25, 38, White);
        texture.Apply();
        return texture;
    }

    private static Texture2D MakeSettingsIcon()
    {
        Texture2D texture = ClearTexture(128, 128);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            FillCircle(texture, 64 + Mathf.Cos(a) * 31f, 64 + Mathf.Sin(a) * 31f, 9f, White);
        }
        StrokeCircle(texture, 64, 64, 27, 11, White);
        FillCircle(texture, 64, 64, 10, White);
        texture.Apply();
        return texture;
    }

    private static Texture2D MakeRotateIcon()
    {
        Texture2D texture = ClearTexture(128, 128);
        StrokeCircle(texture, 64, 64, 35, 8, White);
        FillTriangle(texture, new Vector2(99, 58), new Vector2(82, 48), new Vector2(86, 72), White);
        FillTriangle(texture, new Vector2(29, 70), new Vector2(46, 80), new Vector2(42, 56), White);
        texture.Apply();
        return texture;
    }

    private static Texture2D MakeSparkIcon()
    {
        Texture2D texture = ClearTexture(128, 128);
        FillTriangle(texture, new Vector2(64, 12), new Vector2(78, 64), new Vector2(64, 116), White);
        FillTriangle(texture, new Vector2(64, 12), new Vector2(50, 64), new Vector2(64, 116), White);
        FillTriangle(texture, new Vector2(20, 64), new Vector2(64, 52), new Vector2(108, 64), White);
        FillTriangle(texture, new Vector2(20, 64), new Vector2(64, 76), new Vector2(108, 64), White);
        texture.Apply();
        return texture;
    }

    private static Texture2D MakeClockIcon()
    {
        Texture2D texture = ClearTexture(128, 128);
        StrokeCircle(texture, 64, 64, 45, 8, White);
        StrokeLine(texture, new Vector2(64, 64), new Vector2(64, 35), 7f, White);
        StrokeLine(texture, new Vector2(64, 64), new Vector2(88, 70), 7f, White);
        texture.Apply();
        return texture;
    }

    private static void FillCircle(Texture2D texture, float cx, float cy, float radius, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 2f));
        int maxX = Mathf.Min(texture.width - 1, Mathf.CeilToInt(cx + radius + 2f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 2f));
        int maxY = Mathf.Min(texture.height - 1, Mathf.CeilToInt(cy + radius + 2f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float a = 1f - Smooth01(radius - 1f, radius + 1f, d);
                Blend(texture, x, y, color, a);
            }
        }
    }

    private static void FillEllipse(Texture2D texture, float cx, float cy, float rx, float ry, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - rx - 2f));
        int maxX = Mathf.Min(texture.width - 1, Mathf.CeilToInt(cx + rx + 2f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - ry - 2f));
        int maxY = Mathf.Min(texture.height - 1, Mathf.CeilToInt(cy + ry + 2f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                float d = dx * dx + dy * dy;
                float a = 1f - Smooth01(0.82f, 1.04f, d);
                Blend(texture, x, y, color, a);
            }
        }
    }

    private static void StrokeCircle(Texture2D texture, float cx, float cy, float radius, float width, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius - width - 2f));
        int maxX = Mathf.Min(texture.width - 1, Mathf.CeilToInt(cx + radius + width + 2f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius - width - 2f));
        int maxY = Mathf.Min(texture.height - 1, Mathf.CeilToInt(cy + radius + width + 2f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float d = Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - radius);
                float a = 1f - Smooth01(width * 0.5f - 1f, width * 0.5f + 1f, d);
                Blend(texture, x, y, color, a);
            }
        }
    }

    private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        for (int yy = y; yy < y + height; yy++)
        {
            for (int xx = x; xx < x + width; xx++)
            {
                if (xx >= 0 && xx < texture.width && yy >= 0 && yy < texture.height)
                {
                    texture.SetPixel(xx, yy, color);
                }
            }
        }
    }

    private static void StrokeLine(Texture2D texture, Vector2 a, Vector2 b, float width, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - width - 2f));
        int maxX = Mathf.Min(texture.width - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + width + 2f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - width - 2f));
        int maxY = Mathf.Min(texture.height - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + width + 2f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float d = DistanceToSegment(new Vector2(x, y), a, b);
                float alpha = 1f - Smooth01(width * 0.5f - 1f, width * 0.5f + 1f, d);
                Blend(texture, x, y, color, alpha);
            }
        }
    }

    private static void FillTriangle(Texture2D texture, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - 1f));
        int maxX = Mathf.Min(texture.width - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - 1f));
        int maxY = Mathf.Min(texture.height - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 1f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (PointInTriangle(new Vector2(x, y), a, b, c))
                {
                    Blend(texture, x, y, color, 1f);
                }
            }
        }
    }

    private static void Blend(Texture2D texture, int x, int y, Color color, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        if (alpha <= 0f)
        {
            return;
        }

        Color current = texture.GetPixel(x, y);
        Color source = color;
        source.a *= alpha;
        texture.SetPixel(x, y, Color.Lerp(current, source, source.a));
    }

    private static float RoundedDistance(Vector2 point, Vector2 half, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) - half;
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    private static float Smooth01(float edge0, float edge1, float value)
    {
        if (Mathf.Approximately(edge0, edge1))
        {
            return value < edge0 ? 0f : 1f;
        }

        float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(point - a, ab) / Vector2.Dot(ab, ab);
        t = Mathf.Clamp01(t);
        return Vector2.Distance(point, a + ab * t);
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s = a.y * c.x - a.x * c.y + (c.y - a.y) * p.x + (a.x - c.x) * p.y;
        float t = a.x * b.y - a.y * b.x + (a.y - b.y) * p.x + (b.x - a.x) * p.y;
        if ((s < 0) != (t < 0))
        {
            return false;
        }

        float area = -b.y * c.x + a.y * (c.x - b.x) + a.x * (b.y - c.y) + b.x * c.y;
        return area < 0 ? (s <= 0 && s + t >= area) : (s >= 0 && s + t <= area);
    }

    private static Texture2D ClearTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }
        return texture;
    }

    private static Font ResolveFont()
    {
        string[] names =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "SimHei",
            "Arial"
        };

        Font font = Font.CreateDynamicFontFromOSFont(names, 64);
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        return font;
    }

    private static void LookAt(Transform transform, Vector3 target)
    {
        Vector3 direction = target - transform.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    private static void SetGameViewFixedResolution(int width, int height)
    {
        System.Reflection.Assembly editorAssembly = typeof(Editor).Assembly;
        System.Type gameViewSizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
        System.Type gameViewSizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
        System.Type gameViewSizeGroupType = editorAssembly.GetType("UnityEditor.GameViewSizeGroupType");
        System.Type gameViewSizeTypeEnum = editorAssembly.GetType("UnityEditor.GameViewSizeType");
        System.Type gameViewType = editorAssembly.GetType("UnityEditor.GameView");
        if (gameViewSizesType == null || gameViewSizeType == null || gameViewSizeGroupType == null || gameViewSizeTypeEnum == null || gameViewType == null)
        {
            return;
        }

        System.Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(gameViewSizesType);
        System.Reflection.PropertyInfo instanceProperty = singletonType.GetProperty("instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        object sizesInstance = instanceProperty != null ? instanceProperty.GetValue(null, null) : null;
        if (sizesInstance == null)
        {
            return;
        }

        object standaloneGroup = System.Enum.Parse(gameViewSizeGroupType, "Standalone");
        System.Reflection.MethodInfo getGroupMethod = gameViewSizesType.GetMethod("GetGroup", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        object group = getGroupMethod != null ? getGroupMethod.Invoke(sizesInstance, new[] { standaloneGroup }) : null;
        if (group == null)
        {
            return;
        }

        System.Type groupType = group.GetType();
        System.Reflection.MethodInfo getTotalCountMethod = groupType.GetMethod("GetTotalCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        System.Reflection.MethodInfo getGameViewSizeMethod = groupType.GetMethod("GetGameViewSize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        System.Reflection.MethodInfo addCustomSizeMethod = groupType.GetMethod("AddCustomSize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        System.Reflection.PropertyInfo widthProperty = gameViewSizeType.GetProperty("width", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        System.Reflection.PropertyInfo heightProperty = gameViewSizeType.GetProperty("height", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (getTotalCountMethod == null || getGameViewSizeMethod == null || addCustomSizeMethod == null || widthProperty == null || heightProperty == null)
        {
            return;
        }

        int selectedIndex = -1;
        int total = (int)getTotalCountMethod.Invoke(group, null);
        for (int i = 0; i < total; i++)
        {
            object size = getGameViewSizeMethod.Invoke(group, new object[] { i });
            if (size != null && (int)widthProperty.GetValue(size, null) == width && (int)heightProperty.GetValue(size, null) == height)
            {
                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex < 0)
        {
            object fixedResolution = System.Enum.Parse(gameViewSizeTypeEnum, "FixedResolution");
            System.Reflection.ConstructorInfo constructor = gameViewSizeType.GetConstructor(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] { gameViewSizeTypeEnum, typeof(int), typeof(int), typeof(string) },
                null);
            if (constructor == null)
            {
                return;
            }

            object newSize = constructor.Invoke(new object[] { fixedResolution, width, height, "CatLife Preview 9:20" });
            addCustomSizeMethod.Invoke(group, new[] { newSize });
            selectedIndex = (int)getTotalCountMethod.Invoke(group, null) - 1;
        }

        EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
        System.Reflection.PropertyInfo selectedSizeProperty = gameViewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (selectedSizeProperty != null)
        {
            selectedSizeProperty.SetValue(gameView, selectedIndex, null);
        }

        System.Reflection.PropertyInfo drawGizmosProperty = gameViewType.GetProperty("drawGizmos", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (drawGizmosProperty != null)
        {
            drawGizmosProperty.SetValue(gameView, false, null);
        }

        gameView.Repaint();
    }

    private static void DestroyNamed(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Art");
        EnsureFolder("Assets/Art", "Cat");
        EnsureFolder("Assets/Art/Cat", "Materials");
        EnsureFolder("Assets/Art/Cat", "Textures");
        EnsureFolder("Assets", "UI");
        EnsureFolder("Assets/UI", "CatLifeHome");
        EnsureFolder("Assets/Art", "Environment");
        EnsureFolder("Assets", "Screenshots");
        EnsureFolder("Assets", "Settings");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static Color Hex(string hex)
    {
        Color color;
        ColorUtility.TryParseHtmlString("#" + hex, out color);
        return color;
    }

    private sealed class PlaceholderUi
    {
        public GameObject overlay;
        public Text pageStatusText;
        public Text titleText;
        public Text chipText;
        public Text heroText;
        public Text bodyText;
        public Image heroIcon;
        public Button closeButton;
    }

    private sealed class SpriteSet
    {
        public Sprite dot;
        public Sprite circleSolid;
        public Sprite circleOutline;
        public Sprite roundedSolid;
        public Sprite roundedOutline;
        public Sprite button;
        public Sprite cat;
        public Sprite record;
        public Sprite forest;
        public Sprite rotate;
        public Sprite settings;
        public Sprite spark;
        public Sprite clock;
    }
}

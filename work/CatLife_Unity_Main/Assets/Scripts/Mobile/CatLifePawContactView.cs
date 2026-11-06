using UnityEngine;
using UnityEngine.UI;

// Enabled only on the isolated UI candidate until that layout is adopted.
[DefaultExecutionOrder(9000)]
public sealed class CatLifePawContactView : MonoBehaviour
{
    [SerializeField] private Vector3 contactInCatRoot;
    [SerializeField] private Vector3[] nearPawPoints;
    [SerializeField] private Vector3[] farPawPoints;
    [SerializeField] private string nearPawPath;
    [SerializeField] private string farPawPath;
    [SerializeField] private Material materialTemplate;
    [SerializeField] private RectTransform interventionPrompt;
    [SerializeField] private Text sliderHint;
    [SerializeField] private float trackWidthFraction = .76f;
    [SerializeField] private float trackHeight = 144;
    private Transform head;
    private float promptUntil;
    private Camera worldCamera;
    private Transform catRoot, nearPaw, farPaw;
    private RectTransform track;
    private Canvas canvas;
    private CatLifeSwipeToEnd swipe;
    private Material material;
    private static readonly int NearProperty = Shader.PropertyToID("_Paw0");
    private static readonly int FarProperty = Shader.PropertyToID("_Paw1");

    public void Configure(Vector3 contact, Vector3[] nearPoints, Vector3[] farPoints, string nearPath, string farPath, Material template)
    {
        contactInCatRoot = contact; nearPawPoints = nearPoints; farPawPoints = farPoints;
        nearPawPath = nearPath; farPawPath = farPath; materialTemplate = template;
    }
    public void Bind(Camera camera, Transform root)
    {
        worldCamera = camera; catRoot = root;
        nearPaw = root.Find(nearPawPath); farPaw = root.Find(farPawPath);
        head = System.Array.Find(root.GetComponentInChildren<SkinnedMeshRenderer>().bones, bone => bone.name == "head");
        track = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        swipe = GetComponent<CatLifeSwipeToEnd>();
        material = new Material(materialTemplate);
        foreach (Image image in GetComponentsInChildren<Image>(true)) image.material = material;
        swipe.InterventionStarted += ShowPrompt;
        swipe.GestureCancelled += HidePrompt;
    }
    public void ConfigurePrompt(RectTransform prompt, Text hint) { interventionPrompt = prompt; sliderHint = hint; }
    public void SetTrackSize(float widthFraction, float height) { trackWidthFraction = widthFraction; trackHeight = height; }
    private void ShowPrompt() { interventionPrompt.gameObject.SetActive(true); promptUntil = Time.unscaledTime + 2; }
    private void HidePrompt() { if (interventionPrompt != null) interventionPrompt.gameObject.SetActive(false); }
    private void OnDisable() { HidePrompt(); }
    private void LateUpdate()
    {
        if (worldCamera == null || catRoot == null) return;
        float width = worldCamera.targetTexture != null ? worldCamera.targetTexture.width : Screen.width;
        float height = worldCamera.targetTexture != null ? worldCamera.targetTexture.height : Screen.height;
        ProjectPaw(nearPaw, nearPawPoints, NearProperty, width, height);
        ProjectPaw(farPaw, farPawPoints, FarProperty, width, height);
        sliderHint.text = swipe.Progress >= .65f ? "松开确认结束" : "继续右滑可结束";
        var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (interventionPrompt.gameObject.activeSelf)
        {
            if (Time.unscaledTime >= promptUntil) HidePrompt();
            else
            {
                Vector2 headPoint = worldCamera.WorldToScreenPoint(head.position + Vector3.up * .85f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)interventionPrompt.parent, headPoint, uiCamera, out var promptPoint);
                interventionPrompt.anchoredPosition = promptPoint;
            }
        }
        if (swipe.GesturePhase != CatLife.Mobile.CatLifeExitGesturePhase.Idle || swipe.IsSettling) return;
        var parent = (RectTransform)track.parent;
        Vector2 contact = worldCamera.WorldToScreenPoint(catRoot.TransformPoint(contactInCatRoot));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, contact, uiCamera, out var local);
        float trackWidth = parent.rect.width * trackWidthFraction;
        track.anchorMin = track.anchorMax = new Vector2(.5f, .5f);
        track.sizeDelta = new Vector2(trackWidth, trackHeight);
        track.anchoredPosition = local - new Vector2((.08f + .84f * .35f - .5f) * trackWidth, 0);
    }
    private void ProjectPaw(Transform paw, Vector3[] points, int property, float width, float height)
    {
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        foreach (var point in points)
        {
            Vector3 screen = worldCamera.WorldToScreenPoint(paw.TransformPoint(point));
            minX = Mathf.Min(minX, screen.x); maxX = Mathf.Max(maxX, screen.x);
            minY = Mathf.Min(minY, screen.y); maxY = Mathf.Max(maxY, screen.y);
        }
        material.SetVector(property, new Vector4((minX + maxX) / (2 * width), (minY + maxY) / (2 * height),
            ((maxX - minX) * .55f + 2) / width, ((maxY - minY) * .55f + 2) / height));
    }
    private void OnDestroy()
    {
        if (swipe != null) { swipe.InterventionStarted -= ShowPrompt; swipe.GestureCancelled -= HidePrompt; }
        if (material != null) Destroy(material);
    }
}

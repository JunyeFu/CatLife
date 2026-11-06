using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// Full text is laid out once. Only the glyph draw changes; no session or request state lives here.
[RequireComponent(typeof(Text))]
public sealed class CatLifeInsightReveal : BaseMeshEffect
{
    [SerializeField] private Shader revealShader;
    [SerializeField] private float characterSeconds, staggerSeconds, blurDesignPx;
    [SerializeField] private Vector4 bezier;
    private Text label;
    private Material ownedMaterial, originalMaterial;
    private object record, suppressedRecord;
    private string content;
    private int[] quadOrders = Array.Empty<int>();
    private int elementCount;
    private bool queued;
    private double startedAt;
    public bool IsRevealing { get; private set; }
    public bool AwaitingVisibility => queued;
    public int AnimatedElementCount => elementCount;
#if UNITY_EDITOR
    private float? reviewElapsed;
    public void SetReviewElapsed(float elapsed) { reviewElapsed = elapsed; WriteElapsed(elapsed); }
#endif

    public void Configure(Shader shader, float duration, float stagger, float blur, Vector4 curve)
    { revealShader = shader; characterSeconds = duration; staggerSeconds = stagger; blurDesignPx = blur; bezier = curve; }

    public void Present(object owner, string fullText, bool ready)
    {
        if (ReferenceEquals(record, owner) && content == fullText) return;
        Finish();
        record = owner; content = fullText;
        label = GetComponent<Text>(); label.text = fullText;
        quadOrders = BuildQuadOrders(fullText, out elementCount);
        queued = ready && elementCount > 0 && !ReferenceEquals(suppressedRecord, owner);
        if (queued && isActiveAndEnabled) StartReveal();
    }

    // uGUI emits one quad per non-whitespace UTF-16 unit, including fallback surrogate glyphs.
    // All units in a text element share an order, so combining marks / surrogate pairs reveal together.
    public static int[] BuildQuadOrders(string value, out int count)
    {
        int[] starts = StringInfo.ParseCombiningCharacters(value);
        var orders = new List<int>(); count = 0;
        int lineStart = 0;
        for (int e = 0; e < starts.Length; e++)
        {
            int from = starts[e], to = e + 1 < starts.Length ? starts[e + 1] : value.Length;
            if (from > 0 && value[from - 1] == '\n') lineStart = from;
            int lineEnd = value.IndexOf('\n', lineStart); if (lineEnd < 0) lineEnd = value.Length;
            string line = value.Substring(lineStart, lineEnd - lineStart);
            bool source = line.StartsWith("来源：", StringComparison.Ordinal) || line.StartsWith("MiMo 生成", StringComparison.Ordinal)
                || line.StartsWith("本地洞察 ·", StringComparison.Ordinal);
            bool visible = false;
            for (int i = from; i < to; i++) if (!char.IsWhiteSpace(value[i])) visible = true;
            int order = source || !visible ? -1 : count++;
            for (int i = from; i < to; i++) if (!char.IsWhiteSpace(value[i])) orders.Add(order);
        }
        return orders.ToArray();
    }

    protected override void OnEnable() { base.OnEnable(); if (queued) StartReveal(); }
    protected override void OnDisable()
    {
        if (record != null) suppressedRecord = record;
        Finish(); base.OnDisable();
    }
    private void StartReveal()
    {
        queued = false; label = GetComponent<Text>();
        originalMaterial = label.material;
        ownedMaterial = new Material(revealShader) { name = "CatLife insight reveal (local)" };
        ownedMaterial.SetFloat("_CharacterSeconds", characterSeconds);
        ownedMaterial.SetFloat("_StaggerSeconds", staggerSeconds);
        ownedMaterial.SetFloat("_BlurPx", blurDesignPx);
        ownedMaterial.SetVector("_Bezier", bezier);
        label.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
            | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;
        startedAt = Time.unscaledTimeAsDouble;
#if UNITY_EDITOR
        reviewElapsed = null;
#endif
        IsRevealing = true; label.material = ownedMaterial; WriteElapsed(0); label.SetVerticesDirty();
    }
    private void Update()
    {
        if (!IsRevealing) return;
        float elapsed = (float)(Time.unscaledTimeAsDouble - startedAt);
#if UNITY_EDITOR
        if (reviewElapsed.HasValue) elapsed = reviewElapsed.Value;
#endif
        if (elapsed >= (elementCount - 1) * staggerSeconds + characterSeconds) Finish();
        else WriteElapsed(elapsed);
    }
    private void WriteElapsed(float elapsed)
    {
        if (ownedMaterial == null) return;
        ownedMaterial.SetFloat("_Elapsed", elapsed);
        // MaskableGraphic may own a stencil clone; update the actual rendered material as well.
        label.materialForRendering.SetFloat("_Elapsed", elapsed);
    }
    public void Finish()
    {
        queued = false;
        if (!IsRevealing) return;
        IsRevealing = false;
        label.material = originalMaterial; label.SetVerticesDirty();
        Destroy(ownedMaterial); ownedMaterial = null;
    }
    public void StopForBackground()
    { suppressedRecord = record; Finish(); }
    protected override void OnDestroy() { Finish(); base.OnDestroy(); }

    public override void ModifyMesh(VertexHelper helper)
    {
        if (!IsRevealing) return;
        for (int q = 0; q < helper.currentVertCount / 4; q++)
        {
            var v = new UIVertex[4];
            for (int i = 0; i < 4; i++) helper.PopulateUIVertex(ref v[i], q * 4 + i);
            float width = v[1].position.x - v[0].position.x, height = v[0].position.y - v[3].position.y;
            int order = quadOrders[q];
            float pad = order < 0 ? 0 : blurDesignPx * 3;
            Vector2 origin = v[0].uv0, dx = (Vector2)(v[1].uv0 - v[0].uv0), dy = (Vector2)(v[3].uv0 - v[0].uv0);
            for (int i = 0; i < 4; i++)
            {
                bool right = i == 1 || i == 2, bottom = i >= 2;
                v[i].position += new Vector3(right ? pad : -pad, bottom ? -pad : pad, 0);
                v[i].uv0 = new Vector4(right ? 1 + pad / width : -pad / width, bottom ? 1 + pad / height : -pad / height, 0, 0);
                v[i].uv1 = new Vector4(origin.x, origin.y, dx.x, dx.y);
                v[i].uv2 = new Vector4(dy.x, dy.y, 1 / width, 1 / height);
                v[i].uv3 = new Vector4(order, 0, 0, 0);
                helper.SetUIVertex(v[i], q * 4 + i);
            }
        }
    }
}

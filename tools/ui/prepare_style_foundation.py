"""Prepare the approved UI tokens/font specimen; never edit a scene or Prefab.

Run with the existing Python environment containing fontTools. The original
variable font, static Medium font and device captures remain read-only inputs.
"""

import argparse
import html
import json
import uuid
from pathlib import Path

from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools import subset


ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "work/CatLife_Unity_Main/Assets"


def write_meta(path, importer):
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        meta.write_text(
            f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n{importer}\n",
            encoding="utf-8",
        )


def prepare(args):
    token_path = ASSETS / "MobileRuntime/UI/Style/CL_UI_StyleTokens.json"
    tokens = json.loads(token_path.read_text(encoding="utf-8"))
    report_dir = args.report_dir.resolve()
    report_dir.mkdir(parents=True, exist_ok=True)
    weight = tokens["typography"]["softwareDarkWeight"]
    font_path = ROOT / "work/CatLife_Unity_Main" / tokens["typography"]["softwareDarkFont"]
    font_path.parent.mkdir(parents=True, exist_ok=True)
    source = TTFont(args.font_source)
    coverage_files = [ASSETS / "Scripts/Mobile/CatLifeMobileApp.cs", ASSETS / "Editor/CatLifeMobileUiPrefabBuilder.cs"]
    coverage_files += list((ASSETS / "MobileRuntime/Core").glob("*.cs"))
    coverage_files += list((ASSETS / "Scripts/Recognition").glob("*.cs"))
    coverage = set(range(32, 127))
    for code in coverage_files:
        coverage.update(ord(c) for c in code.read_text(encoding="utf-8") if ord(c) > 127 and not c.isspace())
    unavailable = sorted(coverage - set(source.getBestCmap()))
    options = subset.Options()
    options.layout_features = ["*"]
    options.ignore_missing_unicodes = False
    cutter = subset.Subsetter(options=options)
    cutter.populate(unicodes=coverage.intersection(source.getBestCmap()))
    cutter.subset(source)
    font = instantiateVariableFont(source, {"wght": weight}, inplace=False)
    names = {
        1: "CatLife UI", 2: f"Dark{weight}",
        3: f"CatLife UI Dark{weight} v1", 4: f"CatLife UI Dark{weight}",
        6: f"CatLifeUI-Dark{weight}", 16: "CatLife UI", 17: f"Dark{weight}",
    }
    for name_id, text in names.items():
        font["name"].removeNames(nameID=name_id)
        font["name"].setName(text, name_id, 3, 1, 0x409)
    font["OS/2"].fsSelection = (font["OS/2"].fsSelection & ~0x21) | 0x40
    font["head"].macStyle &= ~3
    font.save(font_path)
    exported = TTFont(font_path)
    cmap = exported.getBestCmap()
    required = "专注中0123456789:"
    missing = [c for c in required if ord(c) not in cmap]
    widths = {str(i): exported["hmtx"][cmap[48 + i]][0] for i in range(10)}
    assert not missing, missing
    assert "fvar" not in exported
    assert exported["OS/2"].usWeightClass == weight
    assert len(set(widths.values())) == 1
    baseline = TTFont(args.baseline_font)
    summary = {
        "source_file": str(args.font_source.resolve()),
        "output_file": str(font_path),
        "baseline_weight": baseline["OS/2"].usWeightClass,
        "output_weight": exported["OS/2"].usWeightClass,
        "variable_axes_remaining": "fvar" in exported,
        "required_text": required,
        "missing_required_characters": missing,
        "digit_advance": widths,
        "units_per_em": exported["head"].unitsPerEm,
        "output_bytes": font_path.stat().st_size,
        "glyph_coverage_files": [str(p.relative_to(ROOT)) for p in coverage_files],
        "included_codepoints": len(cmap),
        "unavailable_source_codepoints": [f"U+{cp:04X}" for cp in unavailable],
        "unity_import_verified": False,
        "prefab_binding_changed": False,
    }
    (report_dir / "font-preparation.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8",
    )
    license_path = font_path.parent / "OFL-CatLifeUI-Dark460.txt"
    license_text = (args.font_source.parent / "OFL-NotoSansSC.txt").read_text(encoding="utf-8")
    license_path.write_text("\n".join(line.rstrip() for line in license_text.splitlines()) + "\n", encoding="utf-8")
    write_meta(font_path, """TrueTypeFontImporter:
  externalObjects: {}
  serializedVersion: 4
  fontSize: 16
  forceTextureCase: -2
  characterSpacing: 0
  characterPadding: 1
  includeFontData: 1
  fontNames:
  - CatLife UI
  fallbackFontReferences: []
  customCharacters:
  fontRenderingMode: 0
  ascentCalculationMode: 1
  useLegacyBoundsCalculation: 0
  shouldRoundAdvanceValue: 1
  userData:
  assetBundleName:
  assetBundleVariant:""")
    importer = "TextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:"
    write_meta(license_path, importer)
    write_meta(token_path, importer)
    style_dir = token_path.parent
    write_meta(style_dir, "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:")
    if args.font_only:
        print(json.dumps(summary, ensure_ascii=False))
        return

    # Real captures remain unchanged. The right column is a component specimen,
    # never an altered device capture or a claim that the new UI is installed.
    colors = tokens["colors"]
    variables = ";".join(f"--{k}:{v}" for k, v in colors.items())
    focus = tokens["inputFocus"]
    rgba = lambda a: f"rgb(32 32 30 / {a})"
    rings = f"inset 0 0 0 {focus['innerRingPx']}px {rgba(focus['innerRingAlpha'])},inset 0 0 0 {focus['outerRingPx']}px {rgba(focus['outerRingAlpha'])},inset 0 2px {focus['insetBlurPx']}px {rgba(focus['insetShadowAlpha'])}"
    swatches = "".join(f'<div class="swatch" style="background:{colors[k]}"><span>{colors[k]}</span></div>' for k in ["darkBase", "darkSurface", "darkCard", "darkRaised"])
    home = (args.baseline_dir / "home-ready-r41.png").resolve().as_uri()
    settings = (args.baseline_dir / "settings-baseline-r41.png").resolve().as_uri()
    document = f"""<!doctype html><html lang="zh-CN"><meta charset="utf-8">
<title>CatLife / WP06.5 基础参考</title><style>
@font-face{{font-family:Baseline;src:url('{args.baseline_font.resolve().as_uri()}');font-weight:500}}
@font-face{{font-family:Dark460;src:url('{font_path.as_uri()}');font-weight:460}}
:root{{{variables}}}*{{box-sizing:border-box}}body{{margin:0;background:var(--lightBase);color:var(--ink);font-family:Baseline,'Microsoft YaHei',sans-serif;font-weight:500}}
.board{{width:1500px;padding:48px 54px}}.eyebrow{{color:var(--mutedInk);font-size:15px;letter-spacing:2px}}h1{{font-size:34px;margin:10px 0 6px;font-weight:500}}p{{margin:0;color:var(--mutedInk);font-size:17px;line-height:1.65;text-wrap:pretty}}
.columns{{display:grid;grid-template-columns:290px 290px 1fr;gap:30px;margin-top:36px}}h2{{font-weight:500;font-size:18px;margin:0 0 15px}}.phone{{width:262px;height:588px;display:block;border-radius:22px;box-shadow:0 18px 36px #20201e12}}
.caption{{font-size:14px;line-height:1.6;max-width:265px;margin-top:14px}}.specimen{{background:var(--lightCard);border-radius:24px;padding:28px}}.row{{display:flex;gap:12px;align-items:center;margin-top:22px}}.dot{{background:var(--brandOrange);width:10px;height:10px;border-radius:50%}}.card-title{{font-size:23px}}
.input{{margin-top:20px;background:var(--lightBase);padding:17px 20px;border-radius:16px;font-size:19px}}.focused{{box-shadow:{rings}}}.label{{font-size:13px;color:var(--mutedInk);margin-top:15px}}.dark{{background:var(--darkCard);color:var(--onDark);padding:24px;border-radius:20px;margin-top:24px}}.dark p{{color:var(--onDark);opacity:.65;font-size:13px}}.sample{{font-size:38px;line-height:1.6;white-space:nowrap}}.target{{font-family:Dark460;font-weight:460}}.swatches{{display:flex;height:56px;overflow:hidden;border-radius:12px;margin-top:20px}}.swatch{{flex:1;display:flex;align-items:center;justify-content:center;color:var(--onDark);font:12px sans-serif}}
.foot{{margin-top:32px;padding-top:18px;color:var(--mutedInk);font-size:14px;line-height:1.8}}.accent{{color:var(--brandGreen)}}
</style><main class="board"><div class="eyebrow">CATLIFE / WP06.5 / 2026.11.06</div>
<h1>构图保留，界面表面回归安静。</h1><p>左侧两张为本轮读取的 r41 模拟器原图；右侧仅为待接入的样式参考。</p>
<div class="columns"><section><h2>01 / 当前主页 · 真实截图</h2><img class="phone" src="{html.escape(home)}"><p class="caption">猫、小镇、品牌橙绿及按钮位置保留。<br>这张图尚未应用新皮肤。</p></section>
<section><h2>02 / 当前设置 · 真实截图</h2><img class="phone" src="{html.escape(settings)}"><p class="caption">14 条记录已读回；AI 与自动专注关闭，本地统计开启。</p></section>
<section><h2>03 / 拟用组件 · 尚未集成</h2><div class="specimen"><div class="row" style="margin-top:0"><span class="dot"></span><span class="card-title">中性底板 / 品牌强调保留</span></div><p style="font-size:15px;margin-top:10px">#f5f5f3 底板与 #fcfcfb 卡片，通过明度分层。</p>
<div class="label">普通输入表面</div><div class="input">猫咪名字</div><div class="label">聚焦输入 / 内部双环与内阴影，无外描边</div><div class="input focused">25 分钟</div>
<div class="dark"><p>实际字形对比 / 当前 500 → 派生 460</p><div class="sample">专注中 00:58</div><div class="sample target">专注中 00:58</div><p>仅验证字体实物；Unity 导入、实际 HUD 绑定仍待验。</p><div class="swatches">{swatches}</div></div>
<p style="margin-top:18px;font-size:14px" class="accent">本页不增加主题开关，不改变业务按钮几何。</p></div></section></div>
<div class="foot">G0 基础参考 · 非 r42 运行画面 · 没有构建 APK / 发送模型请求<br>逐字 blur、结束条弹簧及七页新皮肤，分别在 G1–G3 用真实运行画面验收。</div></main></html>"""
    (report_dir / "foundation-reference.html").write_text(document, encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--font-source", type=Path, required=True)
    parser.add_argument("--baseline-font", type=Path, required=True)
    parser.add_argument("--baseline-dir", type=Path, required=True)
    parser.add_argument("--report-dir", type=Path, required=True)
    parser.add_argument("--font-only", action="store_true")
    prepare(parser.parse_args())

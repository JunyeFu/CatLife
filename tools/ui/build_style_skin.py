"""Build new borderless, sliced UI skins with the existing Inkscape tool."""
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TOKENS = ROOT / "work/CatLife_Unity_Main/Assets/MobileRuntime/UI/Style/CL_UI_StyleTokens.json"
SOURCE = ROOT / "06-deliverables/catlife-ui-style-20261106/assets"
OUTPUT = ROOT / "work/CatLife_Unity_Main/Assets/MobileRuntime/UI/Art/Style"
INKSCAPE = "C:/Program Files/Inkscape/bin/inkscape.com"


def build():
    t = json.loads(TOKENS.read_text(encoding="utf-8"))
    c, f = t["colors"], t["inputFocus"]
    SOURCE.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    skins = {
        "SkinCard": (256, 128, 32, c["lightCard"]),
        "SkinChip": (218, 128, 32, c["lightInset"]),
        "SkinChipSelected": (218, 128, 32, c["brandOrange"]),
        "SkinIconCircle": (144, 144, 72, c["lightCard"]),
        "SkinPrimary": (256, 180, 90, c["brandOrange"]),
        "SkinPrimaryPressed": (256, 180, 90, c["brandOrangePressed"]),
        "SkinPrimaryDisabled": (256, 180, 90, c["mutedInk"]),
        "SkinProgress": (256, 24, 12, c["brandOrange"]),
        "SkinProgressTrack": (256, 24, 12, c["lightInset"]),
        "SkinSecondary": (256, 140, 32, c["lightInset"]),
        "SkinTrack": (256, 180, 90, c["lightBase"]),
        "SkinSwipeFill": (256, 52, 26, c["brandOrange"]),
    }
    documents = {}
    for name, (w, h, r, fill) in skins.items():
        rect = f'<rect width="{w}" height="{h}" rx="{r}" fill="{fill}"/>'
        if name == "SkinPrimary":
            rect = f'<defs><linearGradient id="color" x2="0" y2="1"><stop stop-color="{c["brandOrangeTop"]}"/><stop offset="1" stop-color="{fill}"/></linearGradient></defs><rect width="{w}" height="{h}" rx="{r}" fill="url(#color)"/>'
        documents[name] = (w, h, rect)
    documents["SkinSheet"] = (256, 256, f'<path d="M56 0H200Q256 0 256 56V256H0V56Q0 0 56 0Z" fill="{c["lightBase"]}"/>')
    for name, w, h, r in [("SkinCardShadow", 256, 192, 32), ("SkinPrimaryShadow", 256, 244, 90)]:
        body = f'<defs><filter id="soft" x="-20%" y="-60%" width="140%" height="220%"><feGaussianBlur stdDeviation="8"/></filter></defs><rect x="32" y="36" width="{w-64}" height="{h-64}" rx="{r}" fill="{c["ink"]}" fill-opacity=".06" filter="url(#soft)"/>'
        documents[name] = (w, h, body)
    for on in [False, True]:
        body = f'<rect width="132" height="84" rx="42" fill="{c["brandOrange"] if on else c["lightInset"]}"/><circle cx="{92 if on else 40}" cy="42" r="34" fill="{c["lightCard"]}"/>'
        documents["ToggleOn" if on else "ToggleOff"] = (132, 84, body)
    # All strokes are clipped inside the input. The central area stays transparent.
    body = f'''<defs><clipPath id="inside"><rect width="256" height="128" rx="32"/></clipPath><filter id="soft"><feGaussianBlur stdDeviation="{f['insetBlurPx']/3}"/></filter></defs><g clip-path="url(#inside)" fill="none" stroke="{c['ink']}"><rect x=".5" y=".5" width="255" height="127" rx="31.5" stroke-width="{f['innerRingPx']}" stroke-opacity="{f['innerRingAlpha']}"/><rect x="1.5" y="1.5" width="253" height="125" rx="30.5" stroke-width="{f['outerRingPx']}" stroke-opacity="{f['outerRingAlpha']}"/><rect x="1" y="-2" width="254" height="128" rx="31" stroke-width="6" stroke-opacity="{f['insetShadowAlpha']}" filter="url(#soft)"/></g>'''
    documents["InputFocus"] = (256, 128, body)
    for name, (w, h, body) in documents.items():
        svg = SOURCE / (name + ".svg")
        svg.write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">{body}</svg>\n', encoding="utf-8")
        png = OUTPUT / ("S_CL_UI_Style_" + name + ".png")
        subprocess.run([INKSCAPE, str(svg), "--export-type=png", "--export-area-page", "--export-dpi=96", f"--export-filename={png}"], check=True, capture_output=True)
    print(f"Prepared {len(documents)} new UI skins; original Figma sources and images untouched.")


if __name__ == "__main__":
    build()

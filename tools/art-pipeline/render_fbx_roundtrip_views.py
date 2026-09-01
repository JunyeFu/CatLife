import runpy
import sys
from pathlib import Path

import bpy


argv = sys.argv[sys.argv.index("--") + 1 :]
fbx_path = Path(argv[0]).resolve()
output_path = Path(argv[1]).resolve()
bpy.ops.wm.fbx_import(filepath=str(fbx_path))
sys.argv = [sys.argv[0], "--", str(output_path)]
runpy.run_path(str(Path(__file__).with_name("render_standardized_views.py")), run_name="__main__")

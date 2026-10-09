"""Restore the observed slot-clear loss from the pre-conversion render master.

Run in Blender background mode. Writes a separate candidate, never the sources.
"""
import argparse
import sys
from collections import Counter
from pathlib import Path

import bpy


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--pipeline', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--track', choices=('runtime', 'master'), default='runtime')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    pipeline, output = Path(args.pipeline), Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(pipeline / args.track / ('CatLife_' + args.track + '.blend')))
    runtime = bpy.data.objects['CL_ENV_IslandBase_01']
    with bpy.data.libraries.load(str(pipeline / 'render/CatLife_render.blend'), link=False) as (_, data):
        data.objects = ['CL_ENV_IslandBase_01']
    source = data.objects[0]
    # The transfer relies on exact face correspondence, not nearest-surface guessing.
    assert len(runtime.data.vertices) == len(source.data.vertices)
    assert [tuple(p.vertices) for p in runtime.data.polygons] == [tuple(p.vertices) for p in source.data.polygons]
    mapping = {
        'M_Island_GrassTop': 'MAT_CL_GrassSoftGreen',
        'M_Island_SoilSide': 'MAT_CL_SoilWarm',
        'M_Island_DarkBottom': 'MAT_CL_IslandDarkBottom',
        'M_Island_GrassTop_LightPatch': 'MAT_CL_GrassLight',
        'M_Island_SoilSide_WarmEdge': 'MAT_CL_SoilEdge',
        'M_Island_GrassTop_DeepPatch': 'MAT_CL_GrassDeep',
    }
    slots = {material.name: i for i, material in enumerate(runtime.data.materials)}
    for target_face, source_face in zip(runtime.data.polygons, source.data.polygons):
        source_material = source.data.materials[source_face.material_index]
        target_face.material_index = slots[mapping[source_material['source_name']]]
    print('RESTORED_ISLAND_FACES', dict(Counter(runtime.data.materials[p.material_index].name for p in runtime.data.polygons)))
    bpy.data.objects.remove(source, do_unlink=True)
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output / ('CatLife_' + args.track + '_island_restored.blend')), compress=True)
    if args.track == 'master':
        return
    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH':
            obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=str(output / 'CL_TWN_Runtime.fbx'), use_selection=True,
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=False,
        object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
        use_triangles=True, add_leaf_bones=False, bake_anim=False,
        path_mode='RELATIVE', embed_textures=False)
    triangles = 0
    for obj in bpy.context.selected_objects:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    print('RESTORED_TOWN_TRIANGLES', triangles)


if __name__ == '__main__':
    main()

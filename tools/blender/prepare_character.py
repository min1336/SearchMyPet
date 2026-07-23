"""Convert a Meshy GLB character to a Unity-native FBX and emit an audit report.

Run with Blender in background mode:

    blender -b --python tools/blender/prepare_character.py -- \
      --source SourceAssets/Meshy/MeshyCharacter.glb \
      --output CamoHuntAR/Assets/CamoHuntAR/Art/MeshyCharacter.fbx \
      --report artifacts/meshy-character-report.json
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = []

    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--report", required=True)
    return parser.parse_args(argv)


def world_bounds(mesh_objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    depsgraph = bpy.context.evaluated_depsgraph_get()

    for obj in mesh_objects:
        evaluated = obj.evaluated_get(depsgraph)
        for corner in evaluated.bound_box:
            point = evaluated.matrix_world @ Vector(corner)
            minimum.x = min(minimum.x, point.x)
            minimum.y = min(minimum.y, point.y)
            minimum.z = min(minimum.z, point.z)
            maximum.x = max(maximum.x, point.x)
            maximum.y = max(maximum.y, point.y)
            maximum.z = max(maximum.z, point.z)

    return minimum, maximum


def main() -> None:
    args = parse_args()
    source = Path(args.source).resolve()
    output = Path(args.output).resolve()
    report = Path(args.report).resolve()

    if not source.is_file():
        raise FileNotFoundError(source)

    output.parent.mkdir(parents=True, exist_ok=True)
    report.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(source))
    if "FINISHED" not in result:
        raise RuntimeError(f"GLB import failed: {result}")

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    for armature in armatures:
        armature.data.pose_position = "REST"
        armature.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    bpy.context.view_layer.update()
    custom_shape_objects = {
        pose_bone.custom_shape
        for armature in armatures
        for pose_bone in armature.pose.bones
        if pose_bone.custom_shape is not None
    }
    excluded_custom_shapes = sorted(obj.name for obj in custom_shape_objects)
    for obj in custom_shape_objects:
        bpy.data.objects.remove(obj, do_unlink=True)

    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not mesh_objects:
        raise RuntimeError("The GLB contains no mesh objects.")

    minimum, maximum = world_bounds(mesh_objects)
    dimensions = maximum - minimum
    material_names = sorted(
        {
            slot.material.name
            for obj in mesh_objects
            for slot in obj.material_slots
            if slot.material is not None
        }
    )
    image_names = sorted(image.name for image in bpy.data.images if image.name != "Render Result")
    actions = sorted(action.name for action in bpy.data.actions)

    export_result = bpy.ops.export_scene.fbx(
        filepath=str(output),
        check_existing=False,
        use_selection=False,
        object_types={"ARMATURE", "EMPTY", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=False,
        path_mode="COPY",
        embed_textures=True,
    )
    if "FINISHED" not in export_result:
        raise RuntimeError(f"FBX export failed: {export_result}")

    payload = {
        "source": str(source),
        "output": str(output),
        "mesh_objects": [obj.name for obj in mesh_objects],
        "armatures": [obj.name for obj in armatures],
        "excluded_custom_shapes": excluded_custom_shapes,
        "actions": actions,
        "materials": material_names,
        "images": image_names,
        "bounds": {
            "min": list(minimum),
            "max": list(maximum),
            "dimensions": list(dimensions),
        },
        "counts": {
            "meshes": len(mesh_objects),
            "armatures": len(armatures),
            "materials": len(material_names),
            "images": len(image_names),
            "actions": len(actions),
        },
    }
    report.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )

    print("CAMO_HUNT_CHARACTER_REPORT=" + json.dumps(payload, ensure_ascii=False))


if __name__ == "__main__":
    main()

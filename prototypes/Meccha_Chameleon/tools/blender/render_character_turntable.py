"""Render four audit views of a GLB character without modifying the source."""

from __future__ import annotations

import argparse
import math
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
    parser.add_argument("--output-dir", required=True)
    return parser.parse_args(argv)


def bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in objects:
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


def point_camera(camera: bpy.types.Object, target: Vector) -> None:
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def material(name: str, color: tuple[float, float, float, float]) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.diffuse_color = color
    result.use_nodes = True
    principled = result.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Roughness"].default_value = 0.72
    return result


def main() -> None:
    args = parse_args()
    source = Path(args.source).resolve()
    output_dir = Path(args.output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source))

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
    for obj in custom_shape_objects:
        bpy.data.objects.remove(obj, do_unlink=True)

    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    minimum, maximum = bounds(meshes)
    center = (minimum + maximum) * 0.5
    largest = max(maximum - minimum)

    body_material = material("AuditBody", (0.58, 0.86, 0.22, 1.0))
    accent_material = material("AuditAccent", (0.035, 0.045, 0.055, 1.0))
    for obj in meshes:
        obj.data.materials.clear()
        obj.data.materials.append(
            accent_material if "ico" in obj.name.lower() else body_material
        )

    bpy.ops.object.light_add(type="AREA", location=(3.0, -4.0, 5.0))
    key = bpy.context.object
    key.data.energy = 900
    key.data.shape = "DISK"
    key.data.size = 4.0

    bpy.ops.object.light_add(type="AREA", location=(-4.0, 2.0, 2.0))
    fill = bpy.context.object
    fill.data.energy = 500
    fill.data.size = 3.0

    bpy.ops.object.camera_add()
    camera = bpy.context.object
    bpy.context.scene.camera = camera
    camera.data.lens = 58

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("AuditWorld")
    scene.world.color = (0.018, 0.022, 0.028)

    distance = largest * 2.2
    elevation = center.z + largest * 0.12
    for label, angle in (("front", -90), ("right", 0), ("back", 90), ("left", 180)):
        radians = math.radians(angle)
        camera.location = (
            center.x + math.cos(radians) * distance,
            center.y + math.sin(radians) * distance,
            elevation,
        )
        point_camera(camera, center)
        scene.render.filepath = str(output_dir / f"{label}.png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()

using UnityEngine;

namespace CamoHuntAR
{
    public static class MeshUvUtility
    {
        public static bool TryGetTextureCoordinate(
            Mesh mesh,
            int triangleIndex,
            Vector3 barycentricCoordinate,
            out Vector2 uv)
        {
            uv = default;
            if (mesh == null || triangleIndex < 0)
                return false;

            var triangleStart = triangleIndex * 3;
            var triangles = mesh.triangles;
            var coordinates = mesh.uv;
            if (triangleStart + 2 >= triangles.Length || coordinates.Length == 0)
                return false;

            var first = triangles[triangleStart];
            var second = triangles[triangleStart + 1];
            var third = triangles[triangleStart + 2];
            if (first < 0 || second < 0 || third < 0 ||
                first >= coordinates.Length || second >= coordinates.Length || third >= coordinates.Length)
            {
                return false;
            }

            uv = coordinates[first] * barycentricCoordinate.x +
                 coordinates[second] * barycentricCoordinate.y +
                 coordinates[third] * barycentricCoordinate.z;
            return true;
        }
    }
}

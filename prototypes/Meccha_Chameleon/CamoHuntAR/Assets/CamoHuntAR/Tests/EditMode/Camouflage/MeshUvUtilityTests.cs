using NUnit.Framework;
using UnityEngine;

namespace CamoHuntAR.Tests.EditMode.Camouflage
{
    public sealed class MeshUvUtilityTests
    {
        private Mesh mesh;

        [SetUp]
        public void SetUp()
        {
            mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up },
            };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(mesh);

        [Test]
        public void TextureCoordinateUsesTriangleBarycentricCoordinates()
        {
            var found = MeshUvUtility.TryGetTextureCoordinate(
                mesh,
                0,
                new Vector3(0.2f, 0.3f, 0.5f),
                out var uv);

            Assert.That(found, Is.True);
            Assert.That(uv, Is.EqualTo(new Vector2(0.3f, 0.5f)));
        }

        [Test]
        public void InvalidTriangleIsRejected()
        {
            Assert.That(
                MeshUvUtility.TryGetTextureCoordinate(mesh, 1, Vector3.one, out _),
                Is.False);
        }
    }
}

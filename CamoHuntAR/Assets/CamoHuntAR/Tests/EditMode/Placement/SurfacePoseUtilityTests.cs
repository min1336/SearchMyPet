using NUnit.Framework;
using UnityEngine;

namespace CamoHuntAR.Tests
{
    public sealed class SurfacePoseUtilityTests
    {
        [Test]
        public void HorizontalSurfaceKeepsCharacterUpOnTheSurfaceAndFacingCamera()
        {
            var hit = new Pose(new Vector3(1f, 0.5f, 2f), Quaternion.identity);

            var result = SurfacePoseUtility.CreatePlacementPose(hit, Vector3.forward);

            Assert.That(result.position, Is.EqualTo(hit.position));
            Assert.That(Vector3.Dot(result.rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(result.rotation * Vector3.forward, Vector3.back), Is.GreaterThan(0.999f));
        }

        [Test]
        public void VerticalSurfacePointsCharacterForwardAlongSurfaceNormal()
        {
            var wallNormal = Vector3.back;
            var hit = new Pose(Vector3.zero, Quaternion.FromToRotation(Vector3.up, wallNormal));

            var result = SurfacePoseUtility.CreatePlacementPose(hit, Vector3.forward);

            Assert.That(Vector3.Dot(result.rotation * Vector3.forward, wallNormal), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(result.rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.999f));
        }

        [Test]
        public void VerticalSurfaceFlipsAnInwardNormalTowardTheCamera()
        {
            var inwardNormal = Vector3.forward;
            var hit = new Pose(Vector3.zero, Quaternion.FromToRotation(Vector3.up, inwardNormal));

            var result = SurfacePoseUtility.CreatePlacementPose(hit, Vector3.forward);

            Assert.That(Vector3.Dot(result.rotation * Vector3.forward, Vector3.back), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(result.rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.999f));
        }

        [Test]
        public void SlopedHorizontalSurfaceUsesItsNormalAsCharacterUp()
        {
            var surfaceNormal = new Vector3(0f, 0.8f, 0.6f).normalized;
            var hit = new Pose(Vector3.zero, Quaternion.FromToRotation(Vector3.up, surfaceNormal));

            var result = SurfacePoseUtility.CreatePlacementPose(hit, Vector3.right);

            Assert.That(Vector3.Dot(result.rotation * Vector3.up, surfaceNormal), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(result.rotation * Vector3.forward, Vector3.left), Is.GreaterThan(0.999f));
        }

        [Test]
        public void HorizontalSurfaceFallsBackToAStableDirectionWhenCameraLooksAlongNormal()
        {
            var hit = new Pose(Vector3.zero, Quaternion.identity);

            var result = SurfacePoseUtility.CreatePlacementPose(hit, Vector3.down);

            Assert.That(result.rotation.x, Is.Not.NaN);
            Assert.That(result.rotation.y, Is.Not.NaN);
            Assert.That(result.rotation.z, Is.Not.NaN);
            Assert.That(result.rotation.w, Is.Not.NaN);
            Assert.That(Vector3.Dot(result.rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.999f));
        }
    }
}

using UnityEngine;

namespace CamoHuntAR
{
    public static class SurfacePoseUtility
    {
        private const float HorizontalThreshold = 0.75f;
        private const float DirectionEpsilon = 0.0001f;

        public static Pose CreatePlacementPose(Pose raycastPose, Vector3 cameraForward)
        {
            var normal = (raycastPose.rotation * Vector3.up).normalized;
            Quaternion rotation;

            if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) >= HorizontalThreshold)
            {
                var towardCamera = Vector3.ProjectOnPlane(-cameraForward, normal);
                if (towardCamera.sqrMagnitude < DirectionEpsilon)
                    towardCamera = Vector3.ProjectOnPlane(Vector3.forward, normal);

                rotation = Quaternion.LookRotation(towardCamera.normalized, normal);
            }
            else
            {
                if (Vector3.Dot(normal, cameraForward) > 0f)
                    normal = -normal;

                var upright = Vector3.ProjectOnPlane(Vector3.up, normal);
                if (upright.sqrMagnitude < DirectionEpsilon)
                    upright = Vector3.forward;

                rotation = Quaternion.LookRotation(normal, upright.normalized);
            }

            return new Pose(raycastPose.position, rotation);
        }
    }
}

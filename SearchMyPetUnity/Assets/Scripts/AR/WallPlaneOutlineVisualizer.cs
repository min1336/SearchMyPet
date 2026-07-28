using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace SearchMyPet.AR
{
    [RequireComponent(typeof(ARPlane))]
    public sealed class WallPlaneOutlineVisualizer : MonoBehaviour
    {
        private ARPlane plane;
        private LineRenderer outline;

        private void Awake()
        {
            plane = GetComponent<ARPlane>();

            var outlineObject = new GameObject("Vertical Wall Outline");
            outlineObject.transform.SetParent(transform, false);
            outline = outlineObject.AddComponent<LineRenderer>();
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.widthMultiplier = 0.012f;
            outline.startColor = new Color(0.18f, 0.85f, 1f, 0.9f);
            outline.endColor = outline.startColor;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[WallPlaneDetection] Sprites/Default shader was unavailable; vertical outline is disabled.");
                outline.enabled = false;
                return;
            }

            outline.material = new Material(shader);
            outline.positionCount = 0;
        }

        private void Update()
        {
            var isTrackedVerticalPlane = plane.alignment == PlaneAlignment.Vertical && plane.trackingState == TrackingState.Tracking;
            outline.enabled = isTrackedVerticalPlane;
            if (!isTrackedVerticalPlane)
            {
                return;
            }

            var boundary = plane.boundary;
            outline.positionCount = boundary.Length;
            for (var index = 0; index < boundary.Length; index++)
            {
                var point = boundary[index];
                outline.SetPosition(index, new Vector3(point.x, 0f, point.y));
            }
        }

        private void OnDestroy()
        {
            if (outline != null && outline.material != null)
            {
                Destroy(outline.material);
            }
        }
    }
}

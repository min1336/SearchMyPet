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
        private bool isHighlighted;

        private void Awake()
        {
            plane = GetComponent<ARPlane>();

            var outlineObject = new GameObject("Vertical Wall Outline");
            outlineObject.transform.SetParent(transform, false);
            outline = outlineObject.AddComponent<LineRenderer>();
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.widthMultiplier = 0.0045f;
            outline.numCornerVertices = 4;
            outline.numCapVertices = 4;
            outline.startColor = new Color(0.18f, 0.85f, 1f, 0.62f);
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

        public void SetHighlighted(bool highlighted)
        {
            isHighlighted = highlighted;
        }

        private void Update()
        {
            var isTrackedVerticalPlane = isHighlighted &&
                plane.alignment == PlaneAlignment.Vertical &&
                plane.trackingState == TrackingState.Tracking;
            outline.enabled = isTrackedVerticalPlane;
            if (!isTrackedVerticalPlane)
            {
                return;
            }

            var boundary = plane.boundary;
            if (boundary.Length < 3)
            {
                outline.enabled = false;
                return;
            }

            var min = boundary[0];
            var max = boundary[0];
            for (var index = 1; index < boundary.Length; index++)
            {
                min = Vector2.Min(min, boundary[index]);
                max = Vector2.Max(max, boundary[index]);
            }

            // ARKit plane boundaries change frame-to-frame and look jagged when
            // drawn verbatim. A thin bounding rectangle communicates the selected
            // wall without covering the camera view with debug geometry.
            outline.positionCount = 4;
            outline.SetPosition(0, new Vector3(min.x, 0f, min.y));
            outline.SetPosition(1, new Vector3(max.x, 0f, min.y));
            outline.SetPosition(2, new Vector3(max.x, 0f, max.y));
            outline.SetPosition(3, new Vector3(min.x, 0f, max.y));
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

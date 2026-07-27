using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CamoHuntAR
{
    /// <summary>Temporarily presents a placed character in an isolated camera view for surface painting.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterPaintEditorController : MonoBehaviour
    {
        private const int EditingLayer = 30;

        [SerializeField] private ARPlacementController placementController;
        [SerializeField] private Camera arCamera;
        [SerializeField] private CameraFrameColorSampler cameraSampler;
        [SerializeField, Min(0.1f)] private float rotationDegreesPerScreenWidth = 220f;

        private readonly List<LayerState> originalLayers = new List<LayerState>();
        private CamouflageSurfacePaintController target;
        private Camera editorCamera;
        private Transform editorPivot;
        private GameObject backgroundQuad;
        private Material backgroundMaterial;
        private Texture2D backgroundTexture;
        private Transform originalParent;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;
        private bool rotating;
        private int rotationPointerId = -1;
        private Vector2 previousPointerPosition;

        public bool IsEditing => target != null;
        public bool IsRotating => rotating;

        private void Awake()
        {
            if (placementController != null)
            {
                placementController.CharacterPlaced += BeginEditing;
                placementController.CharacterReset += CancelEditing;
            }
        }

        private void OnDestroy()
        {
            if (placementController != null)
            {
                placementController.CharacterPlaced -= BeginEditing;
                placementController.CharacterReset -= CancelEditing;
            }

            RestoreArPresentation();
        }

        private void Update()
        {
            if (!IsEditing || !rotating || !PointerContactReader.TryRead(out var contact))
                return;

            if (contact.Phase == PointerContactPhase.Began)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(contact.PointerId))
                    return;

                rotationPointerId = contact.PointerId;
                previousPointerPosition = contact.Position;
                return;
            }

            if (contact.PointerId != rotationPointerId)
                return;

            if (contact.Phase == PointerContactPhase.Moved)
            {
                var delta = contact.Position - previousPointerPosition;
                previousPointerPosition = contact.Position;
                var degrees = rotationDegreesPerScreenWidth / Mathf.Max(1f, Screen.width);
                editorPivot.Rotate(Vector3.up, -delta.x * degrees, Space.World);
                editorPivot.Rotate(editorCamera.transform.right, delta.y * degrees, Space.World);
                return;
            }

            if (contact.Phase == PointerContactPhase.Ended)
                rotationPointerId = -1;
        }

        public void SetRotationEnabled(bool enabled)
        {
            if (!IsEditing)
                return;

            rotating = enabled;
            rotationPointerId = -1;
            target.SetInputEnabled(!enabled);
        }

        public void FinishEditing()
        {
            if (IsEditing)
                RestoreArPresentation();
        }

        private void BeginEditing(CamouflageSurfacePaintController controller)
        {
            if (controller == null)
                return;

            RestoreArPresentation();
            target = controller;
            originalParent = target.transform.parent;
            originalLocalPosition = target.transform.localPosition;
            originalLocalRotation = target.transform.localRotation;
            originalLocalScale = target.transform.localScale;
            CaptureAndSetLayer(target.transform, EditingLayer);

            editorPivot = new GameObject("CharacterPaintEditorPivot").transform;
            target.transform.SetParent(editorPivot, false);
            target.transform.localPosition = Vector3.zero;
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = Vector3.one;

            editorCamera = CreateEditorCamera(target.transform);
            CreateFrozenBackground();
            if (arCamera != null)
                arCamera.enabled = false;
            target.SetPaintCamera(editorCamera);
            target.SetInputEnabled(true);
        }

        private Camera CreateEditorCamera(Transform character)
        {
            var cameraObject = new GameObject("CharacterPaintEditorCamera", typeof(Camera));
            var cameraComponent = cameraObject.GetComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = new Color(0.018f, 0.055f, 0.071f, 1f);
            cameraComponent.cullingMask = 1 << EditingLayer;
            cameraComponent.fieldOfView = 34f;
            cameraComponent.nearClipPlane = 0.01f;
            cameraComponent.farClipPlane = 10f;

            var bounds = CalculateBounds(character);
            var radius = Mathf.Max(bounds.extents.magnitude, 0.08f);
            var distance = radius / Mathf.Tan(cameraComponent.fieldOfView * Mathf.Deg2Rad * 0.5f) * 1.22f;
            cameraObject.transform.position = bounds.center + Vector3.back * distance;
            cameraObject.transform.LookAt(bounds.center);
            return cameraComponent;
        }

        private void CreateFrozenBackground()
        {
            if (cameraSampler == null ||
                !cameraSampler.TryCreateSnapshot(out backgroundTexture, out var textureTransform))
            {
                return;
            }

            backgroundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var collider = backgroundQuad.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            backgroundQuad.name = "FrozenPaintEditorBackground";
            backgroundQuad.layer = EditingLayer;
            backgroundQuad.transform.SetParent(editorCamera.transform, false);
            const float distance = 2f;
            backgroundQuad.transform.localPosition = Vector3.forward * distance;
            backgroundQuad.transform.localRotation = Quaternion.identity;
            var height = 2f * distance * Mathf.Tan(editorCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            backgroundQuad.transform.localScale = new Vector3(height * editorCamera.aspect, height, 1f);

            backgroundMaterial = new Material(Shader.Find("Unlit/Texture"));
            backgroundMaterial.mainTexture = backgroundTexture;
            backgroundQuad.GetComponent<MeshRenderer>().material = backgroundMaterial;

            var mesh = backgroundQuad.GetComponent<MeshFilter>().mesh;
            mesh.uv = new[]
            {
                TransformViewportPoint(textureTransform, new Vector2(0f, 0f)),
                TransformViewportPoint(textureTransform, new Vector2(1f, 0f)),
                TransformViewportPoint(textureTransform, new Vector2(0f, 1f)),
                TransformViewportPoint(textureTransform, new Vector2(1f, 1f)),
            };
        }

        private static Vector2 TransformViewportPoint(Matrix4x4 transform, Vector2 point)
        {
            var transformed = transform.MultiplyPoint3x4(new Vector3(point.x, point.y, 0f));
            return new Vector2(transformed.x, transformed.y);
        }

        private void CancelEditing() => RestoreArPresentation();

        private void RestoreArPresentation()
        {
            if (target != null)
            {
                target.SetInputEnabled(false);
                target.transform.SetParent(originalParent, false);
                target.transform.localPosition = originalLocalPosition;
                target.transform.localRotation = originalLocalRotation;
                target.transform.localScale = originalLocalScale;
                RestoreLayers();
            }

            if (editorCamera != null)
                Destroy(editorCamera.gameObject);
            if (editorPivot != null)
                Destroy(editorPivot.gameObject);
            if (backgroundQuad != null)
                Destroy(backgroundQuad);
            if (backgroundMaterial != null)
                Destroy(backgroundMaterial);
            if (backgroundTexture != null)
                Destroy(backgroundTexture);
            if (arCamera != null)
                arCamera.enabled = true;

            target = null;
            editorCamera = null;
            editorPivot = null;
            backgroundQuad = null;
            backgroundMaterial = null;
            backgroundTexture = null;
            originalParent = null;
            rotating = false;
            rotationPointerId = -1;
            originalLayers.Clear();
        }

        private void CaptureAndSetLayer(Transform current, int layer)
        {
            originalLayers.Add(new LayerState(current.gameObject, current.gameObject.layer));
            current.gameObject.layer = layer;
            foreach (Transform child in current)
                CaptureAndSetLayer(child, layer);
        }

        private void RestoreLayers()
        {
            foreach (var state in originalLayers)
            {
                if (state.Object != null)
                    state.Object.layer = state.Layer;
            }
        }

        private static Bounds CalculateBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private readonly struct LayerState
        {
            public LayerState(GameObject gameObject, int layer)
            {
                Object = gameObject;
                Layer = layer;
            }

            public GameObject Object { get; }
            public int Layer { get; }
        }
    }
}

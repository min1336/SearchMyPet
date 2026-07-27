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
        [SerializeField, Min(0.1f)] private float rotationDegreesPerScreenWidth = 220f;

        private readonly List<LayerState> originalLayers = new List<LayerState>();
        private CamouflageSurfacePaintController target;
        private Camera editorCamera;
        private Transform editorPivot;
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
            if (arCamera != null)
                arCamera.enabled = true;

            target = null;
            editorCamera = null;
            editorPivot = null;
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

using UnityEngine;
using UnityEngine.EventSystems;

namespace CamoHuntAR
{
    /// <summary>Translates a screen point into a character UV brush stroke or eyedropper sample.</summary>
    [DisallowMultipleComponent]
    public sealed class CamouflageSurfacePaintController : MonoBehaviour
    {
        [SerializeField] private Camera paintCamera;
        [SerializeField] private CamouflageTexturePainter texturePainter;
        [SerializeField] private CameraFrameColorSampler cameraSampler;
        [SerializeField, Min(0.001f)] private float brushRadius = 0.018f;

        private readonly CamouflagePaintSession session = new CamouflagePaintSession();
        private int activePointerId = -1;
        private bool inputEnabled = true;
        private bool paintCanvasPrepared;
        private Vector2 lastPaintUv;

        public Color32 SelectedColor => session.Picker.SelectedColor;
        public CamouflagePaintMode Mode => session.Mode;
        public bool CanUndo => session.CanUndo;

        private void Awake()
        {
            if (texturePainter == null)
                texturePainter = GetComponent<CamouflageTexturePainter>();
        }

        private void Update()
        {
            if (GetComponent<PlacementVisual>()?.IsPreview == true)
            {
                if (session.IsPainting)
                    session.CancelStroke();
                return;
            }

            if (!inputEnabled)
                return;

            if (!PointerContactReader.TryRead(out var contact))
                return;

            if (contact.Phase == PointerContactPhase.Began &&
                EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(contact.PointerId))
                return;
            if (activePointerId == contact.PointerId &&
                EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject(contact.PointerId))
            {
                session.CompleteStroke();
                activePointerId = -1;
                return;
            }

            if (paintCamera == null)
                paintCamera = Camera.main;
            if (cameraSampler == null)
                cameraSampler = FindFirstObjectByType<CameraFrameColorSampler>();

            HandleContact(contact);
        }

        public void Configure(
            Camera sourceCamera,
            CamouflageTexturePainter painter,
            CameraFrameColorSampler sampler)
        {
            paintCamera = sourceCamera;
            texturePainter = painter;
            cameraSampler = sampler;
        }

        public void SetHsv(float hue, float saturation, float value)
        {
            session.SetHsv(hue, saturation, value);
        }

        public void SetMode(CamouflagePaintMode mode) => session.SetMode(mode);

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled && session.IsPainting)
            {
                session.CancelStroke();
                activePointerId = -1;
            }
        }

        public void SetPaintCamera(Camera sourceCamera) => paintCamera = sourceCamera;

        public void PrepareForPainting()
        {
            if (paintCanvasPrepared || texturePainter == null)
                return;

            paintCanvasPrepared = texturePainter.InitializeCanvas(new Color32(255, 255, 255, 255));
        }

        public bool TryUndo()
        {
            if (!session.TryUndo(out var restored))
                return false;
            return texturePainter.Repaint(restored, new Color32(255, 255, 255, 255));
        }

        public bool TryPaintScreenPoint(Vector2 screenPoint)
        {
            if (!TryGetCharacterUv(screenPoint, out var uv))
                return false;
            if (!session.BeginStroke(uv, brushRadius))
                return false;
            var painted = texturePainter.PaintAtUv(uv, session.Picker.SelectedColor, brushRadius);
            return painted && session.CompleteStroke();
        }

        public bool TrySampleCharacterScreenPoint(Vector2 screenPoint)
        {
            if (!TryGetCharacterUv(screenPoint, out var uv) ||
                !texturePainter.TrySampleUv(uv, out var sampled))
            {
                return false;
            }

            session.SampleCharacter(sampled);
            return true;
        }

        public bool TrySampleRealityViewport(Vector2 viewportPoint)
        {
            if (cameraSampler == null || !cameraSampler.TrySampleViewport(viewportPoint, out var sampled))
                return false;
            session.SampleReality(sampled);
            return true;
        }

        private void HandleContact(PointerContact contact)
        {
            if (contact.Phase == PointerContactPhase.Began)
            {
                if (session.Mode == CamouflagePaintMode.SampleReality)
                {
                    TrySampleRealityViewport(new Vector2(
                        contact.Position.x / Screen.width,
                        contact.Position.y / Screen.height));
                    return;
                }
                if (session.Mode == CamouflagePaintMode.SampleCharacter)
                {
                    TrySampleCharacterScreenPoint(contact.Position);
                    return;
                }
                if (TryGetCharacterUv(contact.Position, out var uv) && session.BeginStroke(uv, brushRadius))
                {
                    activePointerId = contact.PointerId;
                    lastPaintUv = uv;
                    texturePainter.PaintAtUv(uv, session.Picker.SelectedColor, brushRadius);
                }
                return;
            }

            if (contact.PointerId != activePointerId)
                return;
            if (contact.Phase == PointerContactPhase.Moved && TryGetCharacterUv(contact.Position, out var movedUv))
            {
                if (session.AppendStroke(movedUv))
                {
                    texturePainter.PaintLine(lastPaintUv, movedUv, session.Picker.SelectedColor, brushRadius);
                    lastPaintUv = movedUv;
                }
                return;
            }

            if (contact.Phase == PointerContactPhase.Ended)
            {
                session.CompleteStroke();
                activePointerId = -1;
            }
        }

        private bool TryGetCharacterUv(Vector2 screenPoint, out Vector2 uv)
        {
            uv = default;
            if (paintCamera == null || texturePainter == null)
                return false;

            var ray = paintCamera.ScreenPointToRay(screenPoint);
            foreach (var hit in Physics.RaycastAll(ray))
            {
                // The root CapsuleCollider is for placement/finding. Only the
                // skinned mesh collider supplies the character's UV coordinate.
                if (hit.collider is not MeshCollider ||
                    !hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                return MeshUvUtility.TryGetTextureCoordinate(
                    ((MeshCollider)hit.collider).sharedMesh,
                    hit.triangleIndex,
                    hit.barycentricCoordinate,
                    out uv);
            }

            return false;
        }
    }
}

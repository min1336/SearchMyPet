using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace SearchMyPet.AR
{
    /// <summary>
    /// Displays only the zoom stops that the active ARKit capture device can use.
    /// AR Foundation does not expose this capture-device API, so iOS calls a
    /// deliberately small native bridge instead of faking zoom by scaling Unity UI.
    /// </summary>
    public sealed class ARCameraLensController : MonoBehaviour
    {
        [SerializeField] private GameObject selectorRoot;
        [SerializeField] private Button ultraWideButton;
        [SerializeField] private Button normalButton;
        [SerializeField] private Button doubleButton;
        [SerializeField] private Text selectedLensText;

        private Button[] lensButtons;
        private float minimumZoom;
        private float maximumZoom;
        private bool captureDeviceAvailable;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool SearchMyPetARCameraLensGetRange(
            out float minimum,
            out float maximum,
            out float current);

        [DllImport("__Internal")]
        private static extern bool SearchMyPetARCameraLensRampTo(float requestedZoom, float rate, out float appliedZoom);
#endif

        private void Awake()
        {
            if (lensButtons == null)
            {
                lensButtons = new[] { ultraWideButton, normalButton, doubleButton };
            }

            SetSelectorVisible(false);
        }

        private IEnumerator Start()
        {
            // ARKit makes its configurable capture device available only after
            // the AR session has begun initializing.
            yield return new WaitForSeconds(0.75f);

            const int maximumAttempts = 8;
            for (var attempt = 0; attempt < maximumAttempts; attempt++)
            {
                if (RefreshAvailability())
                {
                    yield break;
                }

                if (attempt < maximumAttempts - 1)
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }

            Debug.LogWarning("[ARCameraLens] This AR session does not expose a configurable capture device after retrying.");
        }

        public void SelectUltraWide() => SelectPreset(0.5f);

        public void SelectNormal() => SelectPreset(1f);

        public void SelectDouble() => SelectPreset(2f);

        private bool RefreshAvailability()
        {
            if (!TryGetNativeRange(out minimumZoom, out maximumZoom, out var currentZoom))
            {
                SetSelectorVisible(false);
                return false;
            }

            captureDeviceAvailable = true;
            SetButtonAvailable(ultraWideButton, 0.5f);
            SetButtonAvailable(normalButton, 1f);
            SetButtonAvailable(doubleButton, 2f);
            SetSelectorVisible(IsPresetAvailable(0.5f) || IsPresetAvailable(2f));
            UpdateSelection(currentZoom);
            Debug.Log($"[ARCameraLens] available zoom range={minimumZoom:F2}x..{maximumZoom:F2}x current={currentZoom:F2}x");
            return true;
        }

        private void SelectPreset(float preset)
        {
            if (!captureDeviceAvailable || !IsPresetAvailable(preset))
            {
                return;
            }

            if (TryRampNativeZoom(preset, out var appliedZoom))
            {
                UpdateSelection(appliedZoom);
                Debug.Log($"[ARCameraLens] selected {appliedZoom:F2}x");
            }
            else
            {
                // The AR session can restart after interruption; re-query the
                // hardware before exposing a stale selector again.
                if (!RefreshAvailability())
                {
                    Debug.LogWarning("[ARCameraLens] The configurable capture device is temporarily unavailable.");
                }
            }
        }

        private bool IsPresetAvailable(float preset)
        {
            const float tolerance = 0.02f;
            return preset >= minimumZoom - tolerance && preset <= maximumZoom + tolerance;
        }

        private void SetButtonAvailable(Button button, float preset)
        {
            if (button != null)
            {
                button.gameObject.SetActive(IsPresetAvailable(preset));
            }
        }

        private void UpdateSelection(float currentZoom)
        {
            var nearestPreset = 1f;
            var nearestDistance = float.MaxValue;
            foreach (var preset in new[] { 0.5f, 1f, 2f })
            {
                if (!IsPresetAvailable(preset))
                {
                    continue;
                }

                var distance = Mathf.Abs(currentZoom - preset);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPreset = preset;
                }
            }

            if (selectedLensText != null)
            {
                selectedLensText.text = $"{nearestPreset:0.0#}×";
            }

            foreach (var button in lensButtons)
            {
                if (button == null || !button.gameObject.activeSelf)
                {
                    continue;
                }

                var selected = Mathf.Abs(ParsePreset(button) - nearestPreset) < 0.01f;
                button.image.color = selected
                    ? new Color(0.16f, 0.18f, 0.15f, 0.96f)
                    : new Color(0f, 0f, 0f, 0.3f);
                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.color = selected ? new Color(0.70f, 1f, 0.05f) : new Color(0.9f, 0.92f, 0.92f);
                }
            }
        }

        private static float ParsePreset(Button button)
        {
            return button.name.Contains("0.5") ? 0.5f : button.name.Contains("2") ? 2f : 1f;
        }

        private void SetSelectorVisible(bool visible)
        {
            if (selectorRoot != null)
            {
                selectorRoot.SetActive(visible);
            }
        }

        private static bool TryGetNativeRange(out float minimum, out float maximum, out float current)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SearchMyPetARCameraLensGetRange(out minimum, out maximum, out current);
#else
            minimum = maximum = current = 1f;
            return false;
#endif
        }

        private static bool TryRampNativeZoom(float requestedZoom, out float appliedZoom)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SearchMyPetARCameraLensRampTo(requestedZoom, 5f, out appliedZoom);
#else
            appliedZoom = 1f;
            return false;
#endif
        }
    }
}

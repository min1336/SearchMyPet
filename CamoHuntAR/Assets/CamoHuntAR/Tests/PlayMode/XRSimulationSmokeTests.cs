using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Simulation;

namespace CamoHuntAR.Tests
{
    public sealed class XRSimulationSmokeTests
    {
        private const float SessionTimeoutSeconds = 15f;
        private const float PlaneTimeoutSeconds = 15f;
        private const float InteractiveObservationSeconds = 30f;

        [UnityTest]
        public IEnumerator SimulationStartsAndDiscoversAPlacementPlane()
        {
            SceneManager.LoadScene("ARPlacementScene", LoadSceneMode.Single);
            yield return null;

            var sessionDeadline = Time.realtimeSinceStartup + SessionTimeoutSeconds;
            while (ARSession.state != ARSessionState.SessionTracking &&
                   Time.realtimeSinceStartup < sessionDeadline)
            {
                yield return null;
            }

            Assert.That(
                ARSession.state,
                Is.EqualTo(ARSessionState.SessionTracking),
                "XR Simulation did not reach SessionTracking.");

            var planeManager = Object.FindAnyObjectByType<ARPlaneManager>();
            var controller = Object.FindAnyObjectByType<ARPlacementController>();
            var simulationCamera = Object.FindAnyObjectByType<SimulationCameraPoseProvider>();
            Assert.That(planeManager, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(simulationCamera, Is.Not.Null);

            var planeDeadline = Time.realtimeSinceStartup + PlaneTimeoutSeconds;
            var initialPosition = simulationCamera.transform.position;
            var initialRotation = simulationCamera.transform.rotation;
            var frame = 0;
            while (planeManager.trackables.count == 0 && Time.realtimeSinceStartup < planeDeadline)
            {
                var phase = frame++ * 0.025f;
                SetSimulationCameraPose(
                    simulationCamera,
                    new Pose(
                        initialPosition + new Vector3(Mathf.Sin(phase) * 0.35f, 0f, Mathf.Cos(phase) * 0.35f),
                        initialRotation * Quaternion.Euler(0f, Mathf.Sin(phase) * 30f, 0f)));
                yield return null;
            }

            Assert.That(
                planeManager.trackables.count,
                Is.GreaterThan(0),
                "XR Simulation did not discover a plane in the default environment.");
            Assert.That(controller.State, Is.EqualTo(PlacementState.Detecting));
            Assert.That(controller.LastError, Is.Empty);

            ARPlane placementPlane = null;
            foreach (var plane in planeManager.trackables)
            {
                if (plane.alignment == PlaneAlignment.HorizontalUp ||
                    plane.alignment == PlaneAlignment.Vertical)
                {
                    placementPlane = plane;
                    break;
                }
            }

            Assert.That(placementPlane, Is.Not.Null, "No supported placement plane was discovered.");
            var surfaceNormal = placementPlane.transform.up;
            var placementRay = new Ray(
                placementPlane.transform.position + surfaceNormal,
                -surfaceNormal);
            Assert.That(
                controller.TryMovePreview(placementRay),
                Is.True,
                "World-space raycast did not hit the discovered simulation plane.");
            Assert.That(controller.State, Is.EqualTo(PlacementState.Previewing));
            Assert.That(Object.FindObjectsByType<PlacementVisual>(), Has.Length.EqualTo(1));

            var confirmButton = GameObject.Find("ConfirmButton")?.GetComponent<Button>();
            Assert.That(confirmButton, Is.Not.Null);
            Assert.That(confirmButton.interactable, Is.True);
            confirmButton.onClick.Invoke();
            var anchorDeadline = Time.realtimeSinceStartup + 5f;
            while (controller.State != PlacementState.Placed &&
                   string.IsNullOrEmpty(controller.LastError) &&
                   Time.realtimeSinceStartup < anchorDeadline)
            {
                yield return null;
            }

            Assert.That(controller.LastError, Is.Empty);
            Assert.That(controller.State, Is.EqualTo(PlacementState.Placed));
            Assert.That(planeManager.enabled, Is.False);
            var anchorManager = Object.FindAnyObjectByType<ARAnchorManager>();
            Assert.That(anchorManager.trackables.count, Is.EqualTo(1));

            yield return WaitForInteractiveObservation();

            // Let ARTrackableManager consume the provider's add event before removing the anchor.
            yield return null;

            var resetButton = GameObject.Find("ResetButton")?.GetComponent<Button>();
            Assert.That(resetButton, Is.Not.Null);
            resetButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.State, Is.EqualTo(PlacementState.Detecting));
            Assert.That(planeManager.enabled, Is.True);
            Assert.That(anchorManager.trackables.count, Is.EqualTo(0));
            Assert.That(Object.FindObjectsByType<PlacementVisual>(), Is.Empty);
        }

        private static IEnumerator WaitForInteractiveObservation()
        {
            if (Application.isBatchMode)
                yield break;

            Debug.Log(
                $"XR Simulation placement succeeded. Keeping Play Mode active for " +
                $"{InteractiveObservationSeconds:0} seconds for visual inspection.");

            var observationDeadline =
                Time.realtimeSinceStartup + InteractiveObservationSeconds;
            while (Time.realtimeSinceStartup < observationDeadline)
                yield return null;
        }

        private static void SetSimulationCameraPose(
            SimulationCameraPoseProvider simulationCamera,
            Pose pose)
        {
            var updatePose = typeof(SimulationCameraPoseProvider).GetMethod(
                "UpdatePose",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(updatePose, Is.Not.Null, "XR Simulation camera pose API changed.");
            updatePose.Invoke(simulationCamera, new object[] { pose });
        }
    }
}

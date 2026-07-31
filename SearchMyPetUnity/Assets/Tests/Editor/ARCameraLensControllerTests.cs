using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SearchMyPet.AR;
using UnityEngine;

namespace SearchMyPet.Tests.Editor
{
    public sealed class ARCameraLensControllerTests
    {
        [Test]
        public void Start_WhenNativeRangeIsTemporarilyUnavailable_SchedulesAnotherAttempt()
        {
            var gameObject = new GameObject("AR Camera Lens Controller Test");
            try
            {
                var controller = gameObject.AddComponent<ARCameraLensController>();
                var startMethod = typeof(ARCameraLensController).GetMethod(
                    "Start",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(startMethod, Is.Not.Null);

                var routine = (IEnumerator)startMethod.Invoke(controller, null);
                Assert.That(routine.MoveNext(), Is.True, "Start should wait before the first native query.");
                Assert.That(
                    routine.MoveNext(),
                    Is.True,
                    "A temporarily unavailable ARKit capture device should schedule another query.");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}

using NUnit.Framework;
using UnityEngine.XR.ARSubsystems;

namespace CamoHuntAR.Tests
{
    public sealed class PlacementPlaneUtilityTests
    {
        [TestCase(PlaneAlignment.HorizontalUp, TrackingState.Tracking, true)]
        [TestCase(PlaneAlignment.Vertical, TrackingState.Tracking, true)]
        [TestCase(PlaneAlignment.HorizontalDown, TrackingState.Tracking, false)]
        [TestCase(PlaneAlignment.NotAxisAligned, TrackingState.Tracking, false)]
        [TestCase(PlaneAlignment.HorizontalUp, TrackingState.Limited, false)]
        [TestCase(PlaneAlignment.Vertical, TrackingState.None, false)]
        public void IsPlaceable_MatchesPlacementContract(
            PlaneAlignment alignment,
            TrackingState trackingState,
            bool expected)
        {
            Assert.That(
                PlacementPlaneUtility.IsPlaceable(alignment, trackingState),
                Is.EqualTo(expected));
        }
    }
}

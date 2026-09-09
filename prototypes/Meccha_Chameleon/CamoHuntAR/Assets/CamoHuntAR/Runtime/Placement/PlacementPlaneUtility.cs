using UnityEngine.XR.ARSubsystems;

namespace CamoHuntAR
{
    public static class PlacementPlaneUtility
    {
        public static bool IsPlaceable(
            PlaneAlignment alignment,
            TrackingState trackingState)
        {
            return trackingState == TrackingState.Tracking &&
                   (alignment == PlaneAlignment.HorizontalUp ||
                    alignment == PlaneAlignment.Vertical);
        }
    }
}

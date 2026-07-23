using System;

namespace CamoHuntAR
{
    public sealed class PlacementStateMachine
    {
        public PlacementState Current { get; private set; } = PlacementState.Initializing;

        public event Action<PlacementState> Changed;

        public bool MarkSessionReady()
        {
            return Current == PlacementState.Initializing && Set(PlacementState.Detecting);
        }

        public bool MarkPreviewAvailable()
        {
            if (Current != PlacementState.Detecting && Current != PlacementState.Previewing)
                return false;

            return Set(PlacementState.Previewing);
        }

        public bool MarkPlaced()
        {
            return Current == PlacementState.Previewing && Set(PlacementState.Placed);
        }

        public void Reset()
        {
            Set(PlacementState.Detecting);
        }

        private bool Set(PlacementState next)
        {
            if (Current == next)
                return true;

            Current = next;
            Changed?.Invoke(Current);
            return true;
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace CamoHuntAR
{
    public enum PointerContactPhase { Began, Moved, Ended }

    public readonly struct PointerContact
    {
        public PointerContact(Vector2 position, int pointerId, PointerContactPhase phase)
        {
            Position = position;
            PointerId = pointerId;
            Phase = phase;
        }

        public Vector2 Position { get; }
        public int PointerId { get; }
        public PointerContactPhase Phase { get; }
    }

    /// <summary>Reads one primary pointer lifecycle without changing placement's tap reader.</summary>
    public static class PointerContactReader
    {
        public static bool TryRead(out PointerContact contact)
        {
            var touch = Touchscreen.current;
            if (touch != null)
            {
                var primary = touch.primaryTouch;
                if (primary.press.wasPressedThisFrame)
                    return Set(primary.position.ReadValue(), primary.touchId.ReadValue(), PointerContactPhase.Began, out contact);
                if (primary.press.wasReleasedThisFrame)
                    return Set(primary.position.ReadValue(), primary.touchId.ReadValue(), PointerContactPhase.Ended, out contact);
                if (primary.press.isPressed)
                    return Set(primary.position.ReadValue(), primary.touchId.ReadValue(), PointerContactPhase.Moved, out contact);
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                    return Set(mouse.position.ReadValue(), mouse.deviceId, PointerContactPhase.Began, out contact);
                if (mouse.leftButton.wasReleasedThisFrame)
                    return Set(mouse.position.ReadValue(), mouse.deviceId, PointerContactPhase.Ended, out contact);
                if (mouse.leftButton.isPressed)
                    return Set(mouse.position.ReadValue(), mouse.deviceId, PointerContactPhase.Moved, out contact);
            }

            contact = default;
            return false;
        }

        private static bool Set(Vector2 position, int id, PointerContactPhase phase, out PointerContact contact)
        {
            contact = new PointerContact(position, id, phase);
            return true;
        }
    }
}

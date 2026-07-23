using UnityEngine;
using UnityEngine.InputSystem;

namespace CamoHuntAR
{
    public readonly struct PointerPress
    {
        public PointerPress(Vector2 position, int pointerId)
        {
            Position = position;
            PointerId = pointerId;
        }

        public Vector2 Position { get; }
        public int PointerId { get; }
    }

    public static class PointerPressReader
    {
        public static bool TryRead(out PointerPress press)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                press = new PointerPress(
                    touchscreen.primaryTouch.position.ReadValue(),
                    touchscreen.primaryTouch.touchId.ReadValue());
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                press = new PointerPress(mouse.position.ReadValue(), mouse.deviceId);
                return true;
            }

            press = default;
            return false;
        }
    }
}

using System;
using UnityEngine;

namespace CamoHuntAR
{
    [CreateAssetMenu(
        fileName = "PaletteDefinition",
        menuName = "CAMO HUNT/Camouflage Palette")]
    public sealed class PaletteDefinition : ScriptableObject
    {
        public const int RequiredColorCount = 8;

        [SerializeField] private Color32[] colors = Array.Empty<Color32>();

        public int Count => colors?.Length ?? 0;

        public bool TryGetColor(int index, out Color32 color)
        {
            if (Count != RequiredColorCount || index < 0 || index >= Count)
            {
                color = default;
                return false;
            }

            color = colors[index];
            return true;
        }
    }
}

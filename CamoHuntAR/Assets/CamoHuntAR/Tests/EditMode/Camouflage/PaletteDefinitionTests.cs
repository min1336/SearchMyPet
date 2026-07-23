using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CamoHuntAR.Tests
{
    public sealed class PaletteDefinitionTests
    {
        private static readonly Color32[] ExpectedColors =
        {
            new Color32(46, 84, 52, 255),
            new Color32(87, 116, 67, 255),
            new Color32(91, 65, 47, 255),
            new Color32(137, 101, 73, 255),
            new Color32(79, 84, 81, 255),
            new Color32(137, 142, 136, 255),
            new Color32(190, 172, 133, 255),
            new Color32(42, 53, 59, 255),
        };

        private PaletteDefinition _palette;

        [TearDown]
        public void TearDown()
        {
            if (_palette != null)
                Object.DestroyImmediate(_palette);
        }

        [Test]
        public void EightColorsAreReturnedInSerializedOrder()
        {
            _palette = CreatePalette(ExpectedColors);

            Assert.That(_palette.Count, Is.EqualTo(PaletteDefinition.RequiredColorCount));
            for (var index = 0; index < ExpectedColors.Length; index++)
            {
                Assert.That(_palette.TryGetColor(index, out var color), Is.True);
                Assert.That(color, Is.EqualTo(ExpectedColors[index]));
            }
        }

        [TestCase(-1)]
        [TestCase(PaletteDefinition.RequiredColorCount)]
        public void OutOfRangeIndexIsRejected(int index)
        {
            _palette = CreatePalette(ExpectedColors);

            Assert.That(_palette.TryGetColor(index, out var color), Is.False);
            Assert.That(color, Is.EqualTo(default(Color32)));
        }

        [TestCase(0)]
        [TestCase(7)]
        [TestCase(9)]
        public void PaletteWithAnyCountOtherThanEightIsRejected(int colorCount)
        {
            var colors = new Color32[colorCount];
            _palette = CreatePalette(colors);

            Assert.That(_palette.Count, Is.EqualTo(colorCount));
            Assert.That(_palette.TryGetColor(0, out _), Is.False);
        }

        private static PaletteDefinition CreatePalette(Color32[] colors)
        {
            var palette = ScriptableObject.CreateInstance<PaletteDefinition>();
            var serializedPalette = new SerializedObject(palette);
            var serializedColors = serializedPalette.FindProperty("colors");
            serializedColors.arraySize = colors.Length;

            for (var index = 0; index < colors.Length; index++)
            {
                serializedColors.GetArrayElementAtIndex(index).colorValue = colors[index];
            }

            serializedPalette.ApplyModifiedPropertiesWithoutUndo();
            return palette;
        }
    }
}

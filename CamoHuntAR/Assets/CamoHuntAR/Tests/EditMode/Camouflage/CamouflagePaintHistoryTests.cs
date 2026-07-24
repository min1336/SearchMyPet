using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CamoHuntAR.Tests
{
    public sealed class CamouflagePaintHistoryTests
    {
        private static readonly string[] ExpectedRegions = { "body", "accent", "eyes" };
        private PaletteDefinition _palette;

        [SetUp]
        public void SetUp()
        {
            _palette = CreateEightColorPalette();
        }

        [TearDown]
        public void TearDown()
        {
            if (_palette != null)
                Object.DestroyImmediate(_palette);
        }

        [Test]
        public void TryUndoWithoutSnapshotIsRejected()
        {
            var history = new CamouflagePaintHistory();

            Assert.That(history.CanUndo, Is.False);
            Assert.That(history.TryUndo(out var restored), Is.False);
            Assert.That(restored, Is.Null);
        }

        [Test]
        public void CaptureRestoresOneIndependentSnapshotThenClearsIt()
        {
            var original = CamouflageData.CreateDefault(ExpectedRegions);
            var history = new CamouflagePaintHistory();

            history.Capture(original);
            Assert.That(original.TrySetColor("body", 3, _palette), Is.True);

            Assert.That(history.CanUndo, Is.True);
            Assert.That(history.TryUndo(out var restored), Is.True);
            Assert.That(GetPaletteIndex(restored, "body"), Is.EqualTo(0));
            Assert.That(history.CanUndo, Is.False);
            Assert.That(history.TryUndo(out _), Is.False);

            Assert.That(restored.TrySetColor("accent", 4, _palette), Is.True);
            Assert.That(GetPaletteIndex(original, "accent"), Is.EqualTo(0));
        }

        [Test]
        public void LaterCaptureReplacesEarlierSnapshot()
        {
            var data = CamouflageData.CreateDefault(ExpectedRegions);
            var history = new CamouflagePaintHistory();

            history.Capture(data);
            Assert.That(data.TrySetColor("body", 2, _palette), Is.True);
            history.Capture(data);
            Assert.That(data.TrySetColor("body", 5, _palette), Is.True);

            Assert.That(history.TryUndo(out var restored), Is.True);
            Assert.That(GetPaletteIndex(restored, "body"), Is.EqualTo(2));
        }

        [Test]
        public void ClearDiscardsSnapshot()
        {
            var history = new CamouflagePaintHistory();

            history.Capture(CamouflageData.CreateDefault(ExpectedRegions));
            history.Clear();

            Assert.That(history.CanUndo, Is.False);
            Assert.That(history.TryUndo(out var restored), Is.False);
            Assert.That(restored, Is.Null);
        }

        private static int GetPaletteIndex(CamouflageData data, string regionId)
        {
            return data.Regions.Single(region => region.RegionId == regionId).PaletteIndex;
        }

        private static PaletteDefinition CreateEightColorPalette()
        {
            var palette = ScriptableObject.CreateInstance<PaletteDefinition>();
            var serializedPalette = new SerializedObject(palette);
            var serializedColors = serializedPalette.FindProperty("colors");
            serializedColors.arraySize = PaletteDefinition.RequiredColorCount;

            for (var index = 0; index < PaletteDefinition.RequiredColorCount; index++)
            {
                serializedColors.GetArrayElementAtIndex(index).colorValue =
                    new Color32((byte)(20 + index), (byte)(40 + index), (byte)(60 + index), 255);
            }

            serializedPalette.ApplyModifiedPropertiesWithoutUndo();
            return palette;
        }
    }
}

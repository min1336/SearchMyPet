using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CamoHuntAR.Tests
{
    public sealed class CamouflageDataTests
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
                UnityEngine.Object.DestroyImmediate(_palette);
        }

        [Test]
        public void CreateDefaultCreatesEachExpectedRegionExactlyOnce()
        {
            var data = CamouflageData.CreateDefault(ExpectedRegions);

            Assert.That(data.PaletteVersion, Is.EqualTo(CamouflageData.CurrentPaletteVersion));
            Assert.That(data.Regions.Select(region => region.RegionId), Is.EqualTo(ExpectedRegions));
            Assert.That(data.Regions.All(region => region.PaletteIndex == 0), Is.True);
            Assert.That(data.TryValidate(_palette, ExpectedRegions, out var error), Is.True, error);
        }

        [Test]
        public void RegionsCannotBeModifiedThroughThePublicView()
        {
            var data = CamouflageData.CreateDefault(ExpectedRegions);

            Assert.That(data.Regions, Is.InstanceOf<System.Collections.Generic.IReadOnlyList<CamouflageRegionColor>>());
            Assert.That(data.Regions, Is.Not.InstanceOf<System.Collections.Generic.List<CamouflageRegionColor>>());
        }

        [TestCase("{\"paletteVersion\":1,\"regions\":[{\"regionId\":\"body\",\"paletteIndex\":0},{\"regionId\":\"accent\",\"paletteIndex\":1},{\"regionId\":\"unknown\",\"paletteIndex\":2}]}")]
        [TestCase("{\"paletteVersion\":1,\"regions\":[{\"regionId\":\"body\",\"paletteIndex\":0},{\"regionId\":\"body\",\"paletteIndex\":1},{\"regionId\":\"eyes\",\"paletteIndex\":2}]}")]
        [TestCase("{\"paletteVersion\":1,\"regions\":[{\"regionId\":\"body\",\"paletteIndex\":0},{\"regionId\":\"accent\",\"paletteIndex\":1}]}")]
        [TestCase("{\"paletteVersion\":1,\"regions\":[{\"regionId\":\"body\",\"paletteIndex\":-1},{\"regionId\":\"accent\",\"paletteIndex\":1},{\"regionId\":\"eyes\",\"paletteIndex\":2}]}")]
        [TestCase("{\"paletteVersion\":1,\"regions\":[{\"regionId\":\"body\",\"paletteIndex\":8},{\"regionId\":\"accent\",\"paletteIndex\":1},{\"regionId\":\"eyes\",\"paletteIndex\":2}]}")]
        [TestCase("{\"paletteVersion\":2,\"regions\":[{\"regionId\":\"body\",\"paletteIndex\":0},{\"regionId\":\"accent\",\"paletteIndex\":1},{\"regionId\":\"eyes\",\"paletteIndex\":2}]}")]
        public void InvalidSerializedDataIsRejected(string json)
        {
            var data = JsonUtility.FromJson<CamouflageData>(json);

            Assert.That(data.TryValidate(_palette, ExpectedRegions, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void CloneDoesNotShareRegionStorageWithTheOriginal()
        {
            var original = CamouflageData.CreateDefault(ExpectedRegions);
            var clone = original.Clone();

            Assert.That(clone.TrySetColor("body", 3, _palette), Is.True);

            Assert.That(GetPaletteIndex(original, "body"), Is.EqualTo(0));
            Assert.That(GetPaletteIndex(clone, "body"), Is.EqualTo(3));
        }

        [Test]
        public void OnlyKnownRegionWithValidPaletteIndexCanChange()
        {
            var data = CamouflageData.CreateDefault(ExpectedRegions);

            Assert.That(data.TrySetColor("body", 7, _palette), Is.True);
            Assert.That(GetPaletteIndex(data, "body"), Is.EqualTo(7));
            Assert.That(data.TrySetColor("unknown", 1, _palette), Is.False);
            Assert.That(data.TrySetColor("body", -1, _palette), Is.False);
            Assert.That(data.TrySetColor("body", 8, _palette), Is.False);
            Assert.That(data.TryValidate(_palette, ExpectedRegions, out var error), Is.True, error);
        }

        [Test]
        public void DuplicateOrBlankExpectedRegionsAreRejectedAtCreation()
        {
            Assert.Throws<ArgumentException>(
                () => CamouflageData.CreateDefault(new[] { "body", "body" }));
            Assert.Throws<ArgumentException>(
                () => CamouflageData.CreateDefault(new[] { "body", string.Empty }));
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

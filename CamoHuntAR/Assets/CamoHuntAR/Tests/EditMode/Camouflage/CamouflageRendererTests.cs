using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CamoHuntAR.Tests
{
    public sealed class CamouflageRendererTests
    {
        private PaletteDefinition _palette;
        private Material _bodyMaterial;
        private Material _accentMaterial;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _palette = CreateEightColorPalette();
            var shader = Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
            _bodyMaterial = new Material(shader) { color = Color.white };
            _accentMaterial = new Material(shader) { color = Color.white };
            _root = new GameObject("CamoCritter");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_palette);
            Object.DestroyImmediate(_bodyMaterial);
            Object.DestroyImmediate(_accentMaterial);
        }

        [Test]
        public void ApplyAssignsDifferentPaletteColorsWithoutMutatingSharedMaterials()
        {
            var bodyRenderer = CreateRenderer("Body", _bodyMaterial);
            var accentRenderer = CreateRenderer("Accent", _accentMaterial);
            var camouflageRenderer = _root.AddComponent<CamouflageRenderer>();
            camouflageRenderer.Configure(
                _palette,
                new[]
                {
                    new CamouflageRegionBinding("body", bodyRenderer, 0),
                    new CamouflageRegionBinding("accent", accentRenderer, 0),
                });
            var data = CamouflageData.CreateDefault(new[] { "body", "accent" });
            Assert.That(data.TrySetColor("body", 1, _palette), Is.True);
            Assert.That(data.TrySetColor("accent", 6, _palette), Is.True);

            camouflageRenderer.Apply(data);

            Assert.That(bodyRenderer.sharedMaterial.color, Is.EqualTo(GetPaletteColor(1)));
            Assert.That(accentRenderer.sharedMaterial.color, Is.EqualTo(GetPaletteColor(6)));
            Assert.That(_bodyMaterial.color, Is.EqualTo(Color.white));
            Assert.That(_accentMaterial.color, Is.EqualTo(Color.white));
        }

        [Test]
        public void ConfigureRejectsInvalidMaterialIndexAndDuplicateRegionId()
        {
            var renderer = CreateRenderer("Body", _bodyMaterial);
            var camouflageRenderer = _root.AddComponent<CamouflageRenderer>();

            Assert.Throws<System.ArgumentException>(() => camouflageRenderer.Configure(
                _palette,
                new[] { new CamouflageRegionBinding("body", renderer, 1) }));
            Assert.Throws<System.ArgumentException>(() => camouflageRenderer.Configure(
                _palette,
                new[]
                {
                    new CamouflageRegionBinding("body", renderer, 0),
                    new CamouflageRegionBinding("body", renderer, 0),
                }));
        }

        [Test]
        public void DestroyReleasesRuntimeMaterialOwnership()
        {
            var renderer = CreateRenderer("Body", _bodyMaterial);
            var camouflageRenderer = _root.AddComponent<CamouflageRenderer>();
            camouflageRenderer.Configure(
                _palette,
                new[] { new CamouflageRegionBinding("body", renderer, 0) });
            camouflageRenderer.Apply(CamouflageData.CreateDefault(new[] { "body" }));
            var runtimeMaterials = (System.Collections.IDictionary)typeof(CamouflageRenderer)
                .GetField("_runtimeMaterials", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(camouflageRenderer);
            Assert.That(runtimeMaterials.Count, Is.EqualTo(1));

            typeof(CamouflageRenderer)
                .GetMethod("OnDestroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(camouflageRenderer, null);

            Assert.That(runtimeMaterials.Count, Is.EqualTo(0));
        }

        private Renderer CreateRenderer(string name, Material material)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.name = name;
            child.transform.SetParent(_root.transform);
            var renderer = child.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private Color GetPaletteColor(int index)
        {
            Assert.That(_palette.TryGetColor(index, out var color), Is.True);
            return color;
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

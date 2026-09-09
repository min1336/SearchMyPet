using NUnit.Framework;
using UnityEngine;

namespace CamoHuntAR.Tests
{
    public sealed class PlacementVisualTests
    {
        private GameObject _root;
        private GameObject _child;
        private Material _rootMaterial;
        private Material _childMaterial;

        [SetUp]
        public void SetUp()
        {
            _root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _child.transform.SetParent(_root.transform);

            var shader = Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
            _rootMaterial = new Material(shader) { color = new Color(0.2f, 0.4f, 0.6f, 1f) };
            _childMaterial = new Material(shader) { color = new Color(0.7f, 0.3f, 0.1f, 1f) };
            _root.GetComponent<Renderer>().sharedMaterial = _rootMaterial;
            _child.GetComponent<Renderer>().sharedMaterial = _childMaterial;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_rootMaterial);
            Object.DestroyImmediate(_childMaterial);
        }

        [Test]
        public void SetPreviewPreservesSharedMaterialsAndOnlyChangesAlpha()
        {
            var renderers = new[]
            {
                _root.GetComponent<Renderer>(),
                _child.GetComponent<Renderer>(),
            };
            var visual = _root.AddComponent<PlacementVisual>();
            visual.Configure(renderers);

            visual.SetPreview(true);

            Assert.That(renderers[0].sharedMaterial, Is.SameAs(_rootMaterial));
            Assert.That(renderers[1].sharedMaterial, Is.SameAs(_childMaterial));
            Assert.That(_rootMaterial.color, Is.EqualTo(new Color(0.2f, 0.4f, 0.6f, 1f)));
            Assert.That(_childMaterial.color, Is.EqualTo(new Color(0.7f, 0.3f, 0.1f, 1f)));
            Assert.That(GetPreviewColor(renderers[0]).a, Is.LessThan(1f));
            Assert.That(GetPreviewColor(renderers[1]).a, Is.LessThan(1f));

            visual.SetPreview(false);

            Assert.That(GetPreviewColor(renderers[0]).a, Is.EqualTo(1f));
            Assert.That(GetPreviewColor(renderers[1]).a, Is.EqualTo(1f));
        }

        private static Color GetPreviewColor(Renderer renderer)
        {
            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            return propertyBlock.GetColor("_Color");
        }
    }
}

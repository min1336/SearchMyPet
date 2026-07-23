using NUnit.Framework;
using UnityEngine;

namespace CamoHuntAR.Tests
{
    public sealed class PlacementVisualTests
    {
        private GameObject _root;
        private GameObject _child;
        private Material _preview;
        private Material _placed;

        [SetUp]
        public void SetUp()
        {
            _root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _child.transform.SetParent(_root.transform);

            var shader = Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
            _preview = new Material(shader);
            _placed = new Material(shader);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_preview);
            Object.DestroyImmediate(_placed);
        }

        [Test]
        public void SetPreviewChangesEveryConfiguredRendererWithoutInstantiatingMaterials()
        {
            var renderers = new[]
            {
                _root.GetComponent<Renderer>(),
                _child.GetComponent<Renderer>()
            };
            var visual = _root.AddComponent<PlacementVisual>();
            visual.Configure(renderers, _preview, _placed);

            visual.SetPreview(true);

            Assert.That(renderers[0].sharedMaterial, Is.SameAs(_preview));
            Assert.That(renderers[1].sharedMaterial, Is.SameAs(_preview));

            visual.SetPreview(false);

            Assert.That(renderers[0].sharedMaterial, Is.SameAs(_placed));
            Assert.That(renderers[1].sharedMaterial, Is.SameAs(_placed));
        }
    }
}

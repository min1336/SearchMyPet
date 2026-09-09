using NUnit.Framework;
using UnityEngine;

namespace CamoHuntAR.Tests
{
    public sealed class CamouflageTexturePaintingTests
    {
        private GameObject _root;
        private Material _sourceMaterial;

        [SetUp]
        public void SetUp()
        {
            _root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _sourceMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader"));
            _root.GetComponent<Renderer>().sharedMaterial = _sourceMaterial;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_sourceMaterial);
        }

        [Test]
        public void StrokeDataStoresFreeUvColorAndRadiusIndependently()
        {
            var data = CamouflageData.CreateEmpty();

            Assert.That(data.TryAddStroke(new Vector2(0.25f, 0.75f), new Color32(14, 92, 203, 255), 0.08f), Is.True);
            var clone = data.Clone();
            Assert.That(clone.Strokes, Has.Count.EqualTo(1));
            Assert.That(clone.Strokes[0].Uv, Is.EqualTo(new Vector2(0.25f, 0.75f)));
            Assert.That(clone.Strokes[0].Color, Is.EqualTo(new Color32(14, 92, 203, 255)));
            Assert.That(clone.Strokes[0].Radius, Is.EqualTo(0.08f));

            Assert.That(clone.TryAddStroke(new Vector2(-0.1f, 0.5f), Color.white, 0.1f), Is.False);
            Assert.That(clone.TryAddStroke(new Vector2(0.5f, 0.5f), Color.white, 0f), Is.False);
            Assert.That(data.Strokes, Has.Count.EqualTo(1));
        }

        [Test]
        public void TexturePainterPaintsAndSamplesAtUvWithoutMutatingSourceMaterial()
        {
            var renderer = _root.GetComponent<Renderer>();
            var painter = _root.AddComponent<CamouflageTexturePainter>();
            painter.ConfigureForPrefab(renderer, 0, 32);
            painter.InitializeCanvas(Color.white);
            _root.AddComponent<CapsuleCollider>();

            var painted = new Color32(30, 160, 80, 255);
            painter.PaintAtUv(new Vector2(0.5f, 0.5f), painted, 0.12f);

            Assert.That(painter.TrySampleUv(new Vector2(0.5f, 0.5f), out var sampled), Is.True);
            Assert.That(sampled, Is.EqualTo(painted));
            Assert.That(_sourceMaterial.mainTexture, Is.Null);
        }

        [Test]
        public void TexturePainterUsesCanvasColorWithoutPrototypeMaterialTint()
        {
            var renderer = _root.GetComponent<Renderer>();
            _sourceMaterial.color = new Color(0.12f, 0.68f, 0.52f, 1f);
            var painter = _root.AddComponent<CamouflageTexturePainter>();
            painter.ConfigureForPrefab(renderer, 0, 32);

            Assert.That(painter.InitializeCanvas(Color.white), Is.True);
            Assert.That(renderer.sharedMaterial.color, Is.EqualTo(Color.white));
        }

        [Test]
        public void TexturePainterInterpolatesBetweenUvPoints()
        {
            var painter = _root.AddComponent<CamouflageTexturePainter>();
            painter.ConfigureForPrefab(_root.GetComponent<Renderer>(), 0, 64);
            painter.InitializeCanvas(Color.white);
            var color = new Color32(30, 160, 80, 255);

            Assert.That(painter.PaintLine(new Vector2(0.2f, 0.5f), new Vector2(0.8f, 0.5f), color, 0.03f), Is.True);
            Assert.That(painter.TrySampleUv(new Vector2(0.5f, 0.5f), out var middle), Is.True);
            Assert.That(middle.g, Is.GreaterThan(150));
            Assert.That(middle.r, Is.LessThan(80));
        }

        [Test]
        public void HsvPickerAndCanvasEyedropperSetTheSelectedColor()
        {
            var picker = new CamouflageColorPickerState();
            picker.SetHsv(0.5f, 0.75f, 0.8f);
            var hsvColor = picker.SelectedColor;

            picker.SampleCanvas(new Color32(12, 34, 56, 255));

            Assert.That(hsvColor, Is.Not.EqualTo(picker.SelectedColor));
            Assert.That(picker.SelectedColor, Is.EqualTo(new Color32(12, 34, 56, 255)));
        }

        [Test]
        public void SurfaceControllerPaintsAndSamplesTheCharacterAtAScreenPoint()
        {
            _root.transform.position = Vector3.zero;
            var painter = _root.AddComponent<CamouflageTexturePainter>();
            painter.ConfigureForPrefab(_root.GetComponent<Renderer>(), 0, 32);
            painter.InitializeCanvas(Color.white);

            var cameraObject = new GameObject("PaintCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -2f);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 1f;

            var controller = _root.AddComponent<CamouflageSurfacePaintController>();
            controller.Configure(camera, painter, null);
            controller.SetHsv(0f, 1f, 1f);

            try
            {
                var screenCenter = new Vector2(camera.pixelWidth * 0.5f, camera.pixelHeight * 0.5f);
                Assert.That(controller.TryPaintScreenPoint(screenCenter), Is.True);
                Assert.That(controller.TrySampleCharacterScreenPoint(screenCenter), Is.True);
                Assert.That(controller.SelectedColor.r, Is.EqualTo(255));
                Assert.That(controller.SelectedColor.g, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}

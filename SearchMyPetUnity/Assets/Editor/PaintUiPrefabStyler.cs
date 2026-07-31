using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SearchMyPet.Editor
{
    public static class PaintUiPrefabStyler
    {
        private const string PrefabPath = "Assets/Prefabs/SearchMyPetAppUI.prefab";
        private const string PaletteIconPath = "Assets/UI/ColorPalette.png";
        private static readonly Color Neon = new(0.70f, 1f, 0.05f, 1f);
        private static readonly Color Panel = new(0.035f, 0.04f, 0.045f, 0.96f);
        private static readonly Color Card = new(0.10f, 0.11f, 0.12f, 1f);

        [MenuItem("SearchMyPet/Style Paint UI")]
        public static void StylePaintUi()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var appTabBar = root.transform.Find("App Tab Bar");
                if (appTabBar != null)
                {
                    Object.DestroyImmediate(appTabBar.gameObject);
                }

                var safeArea = root.transform.Find("Character Paint UI/Safe Area");
                var topBar = safeArea.Find("Paint Top Bar");
                var toolbar = safeArea.Find("Paint Toolbar");
                var context = safeArea.Find("Paint Tool Options");
                var rounded = toolbar.GetComponent<Image>().sprite;
                var circle = context.Find("Palette Options/Selected Color").GetComponent<Image>().sprite;
                var paletteIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PaletteIconPath);
                var font = root.GetComponentInChildren<Text>(true).font;

                SetRect((RectTransform)toolbar, new Vector2(0f, 104f), new Vector2(358f, 76f));
                toolbar.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.03f, 0.96f);
                StyleToolbar(toolbar, font);
                toolbar.gameObject.SetActive(false);

                StyleBackButton(topBar, font, circle);
                topBar.gameObject.SetActive(true);

                SetRect((RectTransform)context, new Vector2(0f, 186f), new Vector2(358f, 110f));
                context.GetComponent<Image>().color = Panel;
                context.gameObject.SetActive(false);
                foreach (Transform panel in context)
                {
                    panel.gameObject.SetActive(panel.name == "Palette Options");
                }
                StylePalette(context.Find("Palette Options"), font, rounded);

                var oldQuick = safeArea.Find("Paint Quick Controls");
                if (oldQuick != null)
                {
                    Object.DestroyImmediate(oldQuick.gameObject);
                }
                CreateQuickControls(safeArea, font, rounded, circle, paletteIcon);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("SearchMyPet paint UI styled.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("SearchMyPet/Open Paint UI Prefab")]
        public static void OpenPaintUiPrefab()
        {
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        }

        private static void StyleToolbar(Transform toolbar, Font font)
        {
            var names = new[] { "붓", "크기", "질감", "팔레트", "스포이드" };
            var icons = new[] { "╱", "○", "?", "◉", "⌁" };
            var positions = new[] { 7f, 76f, 145f, 214f, 283f };
            for (var index = 0; index < names.Length; index++)
            {
                var button = toolbar.Find(names[index]);
                SetRect((RectTransform)button, new Vector2(positions[index], 10f), new Vector2(65f, 56f), Vector2.zero, Vector2.zero, Vector2.zero);
                var selected = index == 0;
                button.GetComponent<Image>().color = selected ? Card : Color.clear;

                var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
                outline.effectColor = Neon;
                outline.effectDistance = new Vector2(1f, -1f);
                outline.useGraphicAlpha = false;
                outline.enabled = selected;

                var label = button.Find("Label").GetComponent<Text>();
                label.fontSize = 10;
                label.fontStyle = FontStyle.Normal;
                label.color = selected ? Neon : Color.white;
                SetRect(label.rectTransform, new Vector2(0f, 4f), new Vector2(65f, 20f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

                var oldIcon = button.Find("Icon");
                if (oldIcon != null)
                {
                    Object.DestroyImmediate(oldIcon.gameObject);
                }
                var icon = CreateText(button, "Icon", icons[index], font, 18, selected ? Neon : Color.white);
                SetRect(icon.rectTransform, new Vector2(0f, -5f), new Vector2(44f, 28f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            }
        }

        private static void StyleBackButton(Transform topBar, Font font, Sprite circle)
        {
            topBar.GetComponent<Image>().color = Color.clear;
            topBar.GetComponent<Image>().raycastTarget = false;
            SetRect((RectTransform)topBar, new Vector2(0f, -16f), new Vector2(358f, 48f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            var title = topBar.Find("Title");
            if (title != null) title.gameObject.SetActive(false);
            var back = topBar.Find("완료");
            var image = back.GetComponent<Image>();
            image.sprite = circle;
            image.type = Image.Type.Simple;
            image.color = new Color(0.04f, 0.045f, 0.05f, 0.86f);
            SetRect((RectTransform)back, new Vector2(12f, -6f), new Vector2(38f, 38f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            var label = back.Find("Label").GetComponent<Text>();
            label.text = "<";
            label.font = font;
            label.fontSize = 22;
            label.color = Color.white;
        }

        private static void StylePalette(Transform palette, Font font, Sprite rounded)
        {
            var oldTitle = palette.Find("Selected Color Label");
            if (oldTitle != null)
            {
                Object.DestroyImmediate(oldTitle.gameObject);
            }
            var title = CreateText(palette, "Selected Color Label", "선택 색상", font, 11, Color.white);
            title.alignment = TextAnchor.MiddleLeft;
            SetRect(title.rectTransform, new Vector2(12f, -5f), new Vector2(130f, 22f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            var oldShortcut = palette.Find("Eyedropper Shortcut");
            if (oldShortcut != null)
            {
                Object.DestroyImmediate(oldShortcut.gameObject);
            }
            var shortcut = CreateButton(palette, "Eyedropper Shortcut", rounded, Card);
            SetRect((RectTransform)shortcut.transform, new Vector2(304f, 16f), new Vector2(42f, 34f), Vector2.zero, Vector2.zero, Vector2.zero);
            var icon = CreateText(shortcut.transform, "Icon", "⌁", font, 18, Color.white);
            Stretch(icon.rectTransform);

            AddGradientPreview(palette.Find("Hue/Background"), true);
            AddGradientPreview(palette.Find("Brightness/Background"), false);
        }

        private static void CreateQuickControls(Transform parent, Font font, Sprite rounded, Sprite circle, Sprite paletteIcon)
        {
            var root = new GameObject("Paint Quick Controls", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            SetRect((RectTransform)root.transform, new Vector2(0f, 20f), new Vector2(358f, 76f));
            var background = root.GetComponent<Image>();
            background.sprite = rounded;
            background.type = Image.Type.Sliced;
            background.color = new Color(0.02f, 0.025f, 0.03f, 0.96f);
            background.raycastTarget = false;

            var palette = CreateButton(root.transform, "Color Palette Button", rounded, Color.white);
            SetRect((RectTransform)palette.transform, new Vector2(16f, 12f), new Vector2(52f, 52f), Vector2.zero, Vector2.zero, Vector2.zero);
            palette.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);
            palette.GetComponent<CanvasRenderer>().cullTransparentMesh = false;
            var paletteImage = CreateImage(palette.transform, "Palette Icon", paletteIcon, Color.white);
            SetRect(paletteImage.rectTransform, new Vector2(3f, 3f), new Vector2(46f, 46f), Vector2.zero, Vector2.zero, Vector2.zero);
            var pickerDot = CreateImage(palette.transform, "Selected Color", circle, Neon);
            SetRect(pickerDot.rectTransform, new Vector2(38f, 10f), new Vector2(15f, 15f), Vector2.zero, Vector2.zero, Vector2.zero);
            var pickerRing = pickerDot.gameObject.AddComponent<Outline>();
            pickerRing.effectColor = Color.white;
            pickerRing.effectDistance = new Vector2(1f, -1f);

            var capture = CreateButton(root.transform, "Capture Button", circle, Card);
            SetRect((RectTransform)capture.transform, new Vector2(145f, 4f), new Vector2(68f, 68f), Vector2.zero, Vector2.zero, Vector2.zero);
            var shutter = CreateImage(capture.transform, "White Fill", circle, Color.white);
            SetRect(shutter.rectTransform, Vector2.zero, new Vector2(58f, 58f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var pose = CreateButton(root.transform, "Pose Button", rounded, Panel);
            SetRect((RectTransform)pose.transform, new Vector2(290f, 12f), new Vector2(52f, 52f), Vector2.zero, Vector2.zero, Vector2.zero);
            var counter = CreateText(pose.transform, "Label", "♙\n1 / 4", font, 11, Color.white);
            counter.lineSpacing = 0.8f;
            Stretch(counter.rectTransform);
        }

        private static void AddGradientPreview(Transform parent, bool hue)
        {
            var old = parent.Find("Gradient Preview");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }
            var preview = new GameObject("Gradient Preview", typeof(RectTransform));
            preview.transform.SetParent(parent, false);
            Stretch((RectTransform)preview.transform);
            const int steps = 12;
            for (var index = 0; index < steps; index++)
            {
                var color = hue
                    ? Color.HSVToRGB(index / (float)(steps - 1), 0.85f, 1f)
                    : Color.Lerp(Color.black, Color.white, index / (float)(steps - 1));
                var strip = CreateImage(preview.transform, index.ToString(), null, color);
                strip.raycastTarget = false;
                var rect = strip.rectTransform;
                rect.anchorMin = new Vector2(index / (float)steps, 0f);
                rect.anchorMax = new Vector2((index + 1f) / steps, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        private static Button CreateButton(Transform parent, string name, Sprite sprite, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            root.GetComponent<Button>().targetGraphic = image;
            return root.GetComponent<Button>();
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(Transform parent, string name, string value, Font font, int size, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(parent, false);
            var text = root.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2? anchorMin = null, Vector2? anchorMax = null, Vector2? pivot = null)
        {
            rect.anchorMin = anchorMin ?? new Vector2(0.5f, 0f);
            rect.anchorMax = anchorMax ?? new Vector2(0.5f, 0f);
            rect.pivot = pivot ?? new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}

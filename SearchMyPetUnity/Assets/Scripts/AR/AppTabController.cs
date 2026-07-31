using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace SearchMyPet.AR
{
    public enum AppTab
    {
        Map,
        Camera
    }

    public sealed class AppTabController : MonoBehaviour
    {
        private ARSession arSession;
        private GameObject mapFallback;
        private bool initialized;

        public AppTab ActiveTab { get; private set; } = AppTab.Camera;

        public void Initialize(Transform canvas)
        {
            if (initialized || canvas == null)
            {
                return;
            }

            initialized = true;
            arSession = FindAnyObjectByType<ARSession>();
            var editableRoot = canvas.Find("SearchMyPetAppUI");
            if (editableRoot != null)
            {
                mapFallback = editableRoot.Find("Map Fallback")?.gameObject;
            }
            else
            {
                CreateMapFallback(canvas);
            }
            SelectTab(AppTab.Camera);
        }

        public void SelectTab(AppTab tab)
        {
            ActiveTab = tab;
            var showMap = tab == AppTab.Map;
            SetNativeMapVisible(showMap);
            mapFallback?.SetActive(showMap && !NativeMapAvailable);
            if (arSession != null)
            {
                arSession.enabled = !showMap;
            }

        }

        private void CreateMapFallback(Transform canvas)
        {
            mapFallback = new GameObject("Map Fallback", typeof(RectTransform), typeof(Image));
            mapFallback.transform.SetParent(canvas, false);
            var rect = (RectTransform)mapFallback.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            mapFallback.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.06f);

            var label = new GameObject("Map Message", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(mapFallback.transform, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0.1f, 0.35f);
            labelRect.anchorMax = new Vector2(0.9f, 0.65f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>();
            text.text = "내 주변 지도\niPhone에서 현재 위치를 표시합니다";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            mapFallback.SetActive(false);
        }

        private void OnDestroy()
        {
            SetNativeMapVisible(false);
        }

        private static bool NativeMapAvailable => Application.platform == RuntimePlatform.IPhonePlayer;

        private static void SetNativeMapVisible(bool visible)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SMPMapSetVisible(visible ? 1 : 0);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SMPMapSetVisible(int visible);
#endif
    }
}

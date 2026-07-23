using UnityEngine;
using UnityEngine.UI;

namespace CamoHuntAR
{
    public sealed class RuntimeFontBinder : MonoBehaviour
    {
        private static readonly string[] PreferredFonts =
        {
            "Apple SD Gothic Neo",
            "Malgun Gothic",
            "Arial Unicode MS"
        };

        [SerializeField] private Text[] targets;

        private Font _runtimeFont;

        private void Awake()
        {
            _runtimeFont = Font.CreateDynamicFontFromOSFont(PreferredFonts, 32);
            if (_runtimeFont == null || targets == null)
                return;

            foreach (var target in targets)
            {
                if (target != null)
                    target.font = _runtimeFont;
            }
        }

        private void OnDestroy()
        {
            if (_runtimeFont != null)
                Destroy(_runtimeFont);
        }
    }
}

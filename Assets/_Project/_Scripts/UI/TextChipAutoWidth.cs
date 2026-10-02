using TMPro;
using UnityEngine;

namespace Artti.UI
{
    // 글 길이에 맞춰 칩(둥근 막대) 크기를 바꾼다. 칩 왼쪽 위는 그대로 두고 오른쪽/아래로 늘어난다(칩 pivot = 왼쪽 위).
    //   한 줄(글 + 양옆 여백)이 maxWidth보다 길면 띄어쓰기에서 줄을 바꿔 두 줄로 하고(예: "메가커피" / "폴리텍인천캠퍼스점"),
    //   칩은 그만큼 아래로 높아진다. 칩 아래 칸(below, 예: 설명 글)은 위쪽 끝을 같이 내린다(아래쪽 끝은 그대로).
    //   두 줄로도 넘치거나 띄어쓰기가 없으면 글자를 줄이고, fontMin보다 작아지면 ...으로 자른다.
    //   칩 그림은 9-slice라 둥근 모서리는 그대로, 가운데만 늘어난다. 자리/값은 에디터 스크립트(ARFieldResultLayout)가 채운다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class TextChipAutoWidth : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private float padding = 22f;    // 글 양옆 여백
        [SerializeField] private float minWidth = 110f;
        [SerializeField] private float maxWidth = 312f;
        [SerializeField] private float fontMax = 26f;
        [SerializeField] private float fontMin = 16f;
        [Tooltip("두 줄일 때 줄 간격(TMP lineSpacing, 음수면 촘촘하게)")]
        [SerializeField] private float twoLineSpacing = -12f;
        [Tooltip("한 줄일 때 칩 높이")]
        [SerializeField] private float baseHeight = 46f;
        [Tooltip("칩이 두 줄로 높아질 때 위쪽 끝을 같이 내릴 칸(선택). 왼쪽 위 pivot이어야 한다.")]
        [SerializeField] private RectTransform below;
        [SerializeField] private float belowBaseY;
        [SerializeField] private float belowBaseHeight;

        private RectTransform _rt;

        public void SetText(string text)
        {
            if (label == null) return;
            if (_rt == null) _rt = (RectTransform)transform;
            text = text ?? string.Empty;

            float room = maxWidth - 2f * padding;
            label.enableAutoSizing = false;
            label.fontSize = fontMax;
            label.lineSpacing = 0f;
            string shown = text;
            if (Width(text) > room)
            {
                string twoLines = SplitInTwo(text);
                if (twoLines != null)
                {
                    shown = twoLines;
                    label.lineSpacing = twoLineSpacing;
                }
            }

            label.text = shown;
            Vector2 size = label.GetPreferredValues(shown, float.PositiveInfinity, float.PositiveInfinity);
            if (size.x > room && size.x > 0f)
            {
                label.fontSize = Mathf.Max(fontMin, fontMax * room / size.x);
                size = label.GetPreferredValues(shown, float.PositiveInfinity, float.PositiveInfinity);
            }
            label.overflowMode = TextOverflowModes.Ellipsis;

            float oneLine = label.GetPreferredValues("가", float.PositiveInfinity, float.PositiveInfinity).y;
            float extra = shown.IndexOf('\n') >= 0 ? Mathf.Max(0f, size.y - oneLine) : 0f;
            float width = Mathf.Clamp(Mathf.Min(size.x, room) + 2f * padding, minWidth, maxWidth);
            _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, baseHeight + extra);

            if (below != null)
            {
                below.anchoredPosition = new Vector2(below.anchoredPosition.x, belowBaseY - extra);
                below.sizeDelta = new Vector2(below.sizeDelta.x, Mathf.Max(0f, belowBaseHeight - extra));
            }
        }

        private float Width(string s) => label.GetPreferredValues(s, float.PositiveInfinity, float.PositiveInfinity).x;

        // 띄어쓰기 중 두 줄의 긴 쪽이 가장 짧아지는 곳에서 줄을 바꾼다. 띄어쓰기가 없으면 null.
        private string SplitInTwo(string text)
        {
            string best = null;
            float bestWidth = float.MaxValue;
            for (int i = text.IndexOf(' '); i >= 0; i = text.IndexOf(' ', i + 1))
            {
                string first = text.Substring(0, i).TrimEnd();
                string second = text.Substring(i + 1).TrimStart();
                if (first.Length == 0 || second.Length == 0) continue;
                float w = Mathf.Max(Width(first), Width(second));
                if (w < bestWidth)
                {
                    bestWidth = w;
                    best = first + "\n" + second;
                }
            }
            return best;
        }
    }
}

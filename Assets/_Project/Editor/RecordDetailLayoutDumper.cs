using System.Text;
using UnityEditor;
using UnityEngine;

namespace Artti.Editor
{
    // 손배치한 RecordDetailScene의 모든 RectTransform 값을 콘솔에 덤프한다.
    // 용도: 손배치 -> 빌더 코드 이식. 실행 후 콘솔 로그를 복사해 빌더에 반영.
    public static class RecordDetailLayoutDumper
    {
        [MenuItem("Artti/Dump RecordDetailScene Layout")]
        public static void Dump()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { Debug.LogError("[LayoutDumper] Canvas 없음. RecordDetailScene 열고 실행."); return; }

            var sb = new StringBuilder();
            sb.AppendLine("===== RecordDetail Layout Dump =====");
            Walk((RectTransform)canvas.transform, "", sb);
            Debug.Log(sb.ToString());
        }

        static void Walk(RectTransform rt, string path, StringBuilder sb)
        {
            string name = string.IsNullOrEmpty(path) ? rt.name : path + "/" + rt.name;
            var ap = rt.anchoredPosition;
            var sd = rt.sizeDelta;
            var aMin = rt.anchorMin;
            var aMax = rt.anchorMax;
            var pv = rt.pivot;
            // 컴포넌트 종류 표시 (텍스트면 내용 일부)
            var tmp = rt.GetComponent<TMPro.TMP_Text>();
            string extra = tmp != null ? $" text=\"{Trunc(tmp.text)}\"" : "";
            sb.AppendLine(
                $"{name} | pos({ap.x:0.#},{ap.y:0.#}) size({sd.x:0.#},{sd.y:0.#}) " +
                $"anchor({aMin.x:0.##},{aMin.y:0.##})-({aMax.x:0.##},{aMax.y:0.##}) pivot({pv.x:0.##},{pv.y:0.##}){extra}");

            for (int i = 0; i < rt.childCount; i++)
            {
                var c = rt.GetChild(i) as RectTransform;
                if (c != null) Walk(c, name, sb);
            }
        }

        static string Trunc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ");
            return s.Length > 16 ? s.Substring(0, 16) : s;
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Artti.AAC.Logging;
using Artti.Common;

namespace Artti.UI
{
    public class MainSceneView : MonoBehaviour
    {
        [SerializeField] private Button trainingModeBtn;
        [SerializeField] private Button arFieldModeBtn;
        [SerializeField] private Button reportBtn;

        [Header("Report Modal")]
        [SerializeField] private GameObject reportPanel;
        [SerializeField] private TMP_Text reportText;
        [SerializeField] private Button closeReportBtn;

        private void Start()
        {
            trainingModeBtn.onClick.AddListener(() => SceneManager.LoadScene("TrainingHubScene"));
            arFieldModeBtn.onClick.AddListener(() => SceneManager.LoadScene("ARFieldScene"));
            if (reportBtn != null) reportBtn.onClick.AddListener(ShowReport);
            if (closeReportBtn != null) closeReportBtn.onClick.AddListener(HideReport);

            if (reportPanel != null) reportPanel.SetActive(false);
        }

        public void ShowReport()
        {
            if (reportText != null) reportText.text = BuildReportText();
            if (reportPanel != null) reportPanel.SetActive(true);
        }

        public void HideReport()
        {
            if (reportPanel != null) reportPanel.SetActive(false);
        }

        private string BuildReportText()
        {
            var sb = new StringBuilder();
            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null)
            {
                sb.AppendLine("AppBootstrap 미초기화 — 프로필 선택 후 다시 시도해주세요.");
                return sb.ToString();
            }

            var profile = bootstrap.ProfileManager?.ActiveProfile;
            sb.AppendLine($"사용자: {profile?.nickname ?? "(없음)"}");
            sb.AppendLine();

            var store = bootstrap.LogStore as JsonAppendLogStore;
            if (store == null || string.IsNullOrEmpty(store.FilePath) || !File.Exists(store.FilePath))
            {
                sb.AppendLine("아직 활동 기록이 없어요.");
                return sb.ToString();
            }

            var events = ReadEventsSafely(store.FilePath);
            sb.AppendLine($"누적 활동: {events.Count}회");
            sb.AppendLine();
            sb.AppendLine("최근 활동");
            sb.AppendLine("─────────────");

            if (events.Count == 0)
            {
                sb.AppendLine("(없음)");
                return sb.ToString();
            }

            int start = Mathf.Max(0, events.Count - 10);
            for (int i = events.Count - 1; i >= start; i--)
            {
                var ev = events[i];
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(ev.timestampUnixMs).ToLocalTime();
                var scenario = string.IsNullOrEmpty(ev.scenarioId) ? "" : $" · {ev.scenarioId}";
                sb.AppendLine($"{dt:MM-dd HH:mm} {ev.eventType}{scenario}");
            }
            return sb.ToString();
        }

        private static List<AACEvent> ReadEventsSafely(string path)
        {
            var list = new List<AACEvent>();
            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var ev = JsonUtility.FromJson<AACEvent>(line);
                        if (ev != null) list.Add(ev);
                    }
                    catch { /* skip malformed line */ }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MainSceneView] 리포트 로그 읽기 실패: {e.Message}");
            }
            return list;
        }
    }
}

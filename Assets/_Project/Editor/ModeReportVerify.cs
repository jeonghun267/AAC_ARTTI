using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.AAC.Logging;
using Artti.Common;
using Artti.Report;
using Artti.UI;
using Object = UnityEngine.Object;

namespace Artti.Editor
{
    // 모드별 리포트 동작 확인(씬은 저장하지 않는다). 오늘·어제·지난주 예시 기록을 임시 파일로 만들어 리포트 씬에 넣고,
    // 실제 탭 버튼(오늘 / 이번 주 / 전체)과 '전체 보기' 버튼을 눌러(onClick) 화면 글자·칸이 계산 결과와 같은지 본다.
    // 결과는 Console(PASS/FAIL)과 Temp/LayoutCaptures/verify_*.png. 에디터라 등장 연출(트윈)은 기다리지 않는다.
    public static class ModeReportVerify
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly string[] TabNames = { "today", "week", "all" };

        [MenuItem("Artti/Debug/Verify Mode Reports (Tabs + View All)")]
        static void VerifyMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogWarning("[ModeReportVerify] 저장 안 한 씬 변경이 있어 확인하지 않는다.");
                return;
            }
            string log = Path.Combine(Path.GetTempPath(), "artti_verify_" + Guid.NewGuid().ToString("N") + ".jsonl");
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LayoutCaptures"));
            Directory.CreateDirectory(outDir);
            var report = new StringBuilder();
            int pass = 0, fail = 0;
            void Check(string what, bool ok, string detail = "")
            {
                if (ok) pass++; else fail++;
                report.AppendLine($"{(ok ? "PASS" : "FAIL")}  {what}{(string.IsNullOrEmpty(detail) ? "" : "  (" + detail + ")")}");
            }

            try
            {
                WriteSampleLog(log);
                var service = new ReportDataService(log);
                VerifyConversation(service, outDir, Check, report);
                VerifyAccuracy(service, outDir, Check, report);
            }
            catch (Exception e)
            {
                fail++;
                report.AppendLine("FAIL  예외: " + e);
            }
            finally
            {
                if (File.Exists(log)) File.Delete(log);
                EditorSceneManager.OpenScene(ScenePaths.ConversationReport, OpenSceneMode.Single);
            }
            var summary = $"[ModeReportVerify] PASS {pass} / FAIL {fail}\n{report}";
            if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
        }

        // 모범 발음 소리 확인: 리포트와 같은 TTS(구글 클라우드, 정확도 모드 목소리)로 예시 문장 음성을 받아 길이를 본다.
        //   에디터에서는 소리를 내지 않는다(받은 음성의 길이로 확인). 결과는 몇 초 뒤 Console에 나온다.
        [MenuItem("Artti/Debug/Verify Report TTS (Model Pronunciation)")]
        static void VerifyTtsMenu() => VerifyTtsAsync().Forget();

        static async Cysharp.Threading.Tasks.UniTaskVoid VerifyTtsAsync()
        {
            var key = ApiKeyLoader.GetOrFallback(ApiKeyLoader.GoogleTtsApi, ApiKeyLoader.GeminiApi);
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[ModeReportVerify] TTS 키 없음(.env GOOGLE_TTS_API_KEY / GEMINI_API_KEY) — 실행해도 소리가 나지 않는다");
                return;
            }
            var tts = new Artti.Common.Speech.CloudTtsService(key, null);
            tts.SetVoice(TrainingModeContext.VoiceFor(TrainingMode.Accuracy));
            string[] sentences =
            {
                "사과 주세요", "생수 어디에 있나요?", "아니요, 봉투는 필요 없어요. 카드로 계산할게요.", "학교",
                "이렇게 들렸어요. 아과 주세요", "바르게 말하면, 사과 주세요",   // 비교 듣기
            };
            var sb = new StringBuilder();
            int ok = 0;
            foreach (var s in sentences)
            {
                var clip = await tts.SynthesizeAsync(s);
                bool good = clip != null && clip.length > 0.3f;
                if (good) ok++;
                sb.AppendLine($"{(good ? "PASS" : "FAIL")}  \"{s}\" -> {(clip != null ? $"{clip.length:0.00}초, {clip.frequency}Hz" : "음성 없음")}");
                if (clip != null) Object.DestroyImmediate(clip);
            }
            var msg = $"[ModeReportVerify] 모범 발음 TTS {ok}/{sentences.Length} (목소리 {TrainingModeContext.VoiceFor(TrainingMode.Accuracy)})\n{sb}";
            if (ok == sentences.Length) Debug.Log(msg); else Debug.LogError(msg);
        }

        // 대화 AI 평가 확인: 예시 대화 두 개(잘 통한 대화 / 대답이 엇나간 대화)를 실제 Gemini로 채점해 Console에 보여 준다
        [MenuItem("Artti/Debug/Verify Conversation AI Evaluation")]
        static void VerifyEvaluationMenu() => VerifyEvaluationAsync().Forget();

        static async Cysharp.Threading.Tasks.UniTaskVoid VerifyEvaluationAsync()
        {
            ApiKeyLoader.Reload();   // 에디터에선 .env를 바꿔도 캐시가 남아 있어 새로 읽는다
            var service = ConversationEvaluationService.FromEnv();
            Debug.Log($"[ModeReportVerify] 대화 AI 평가 시작 (Gemini 키 {(string.IsNullOrEmpty(ApiKeyLoader.Get(ApiKeyLoader.GeminiApi)) ? "없음" : "있음")}, " +
                      $"대체 OpenAI {OpenAiChatClient.FromEnv().Model} 키 {(OpenAiChatClient.FromEnv().IsAvailable ? "있음" : "없음")})");
            ConversationLine Npc(string t) => new ConversationLine { isUser = false, text = t };
            ConversationLine Me(string t, string heard = null) => new ConversationLine { isUser = true, text = t, spoken = true, heard = heard ?? t };
            ConversationLine Card(string t) => new ConversationLine { isUser = true, text = t };
            var good = new List<ConversationLine>
            {
                Npc("어서 오세요. 무엇을 찾으세요?"), Me("생수 어디에 있나요?", "생수 어디 있나요"), Npc("오른쪽 음료 코너에 있어요."),
                Me("감사합니다"), Npc("봉투 필요하세요?"), Card("아니요, 괜찮아요"), Npc("결제는 어떻게 하시겠어요?"), Me("카드로 할게요"),
            };
            var poor = new List<ConversationLine>
            {
                Npc("어서 오세요. 무엇을 찾으세요?"), Me("카드요"), Npc("봉투 필요하세요?"), Me("생수", "샘수"),
                Npc("결제는 어떻게 하시겠어요?"), Me("안녕히 계세요"),
            };
            var sb = new StringBuilder();
            int ok = 0;
            foreach (var (name, lines) in new[] { ("잘 통한 대화", good), ("엇나간 대화", poor) })
            {
                var e = await service.EvaluateAsync(lines);
                if (e != null && e.IsValid) ok++;
                sb.AppendLine(e == null ? $"FAIL  {name}: 평가 없음"
                    : $"PASS  {name} ({service.LastProvider}): 적절성 {e.appropriateness}, 자연스러움 {e.naturalness} | 잘한 점: {e.praise} | 다음: {e.tip}");
            }
            var msg = $"[ModeReportVerify] 대화 AI 평가 {ok}/2\n{sb}";
            if (ok == 2) Debug.Log(msg); else Debug.LogError(msg);
        }

        // 변경점: Gemini 키를 바꾼 뒤 키를 쓰는 기능을 실제로 한 번씩 불러 본다(점원 대화 3종 + 리포트 응원·피드백·미션·세션 피드백).
        //   폴백 문구가 아니라 Gemini가 만든 답인지 본다. 요청 사이를 띄워 결과는 약 1분 30초 뒤 Console에 나온다.
        [MenuItem("Artti/Debug/Verify Gemini Features")]
        static void VerifyGeminiMenu() => VerifyGeminiAsync().Forget();

        static async Cysharp.Threading.Tasks.UniTaskVoid VerifyGeminiAsync()
        {
            ApiKeyLoader.Reload();
            var key = ApiKeyLoader.Get(ApiKeyLoader.GeminiApi);
            if (string.IsNullOrEmpty(key)) { Debug.LogError("[ModeReportVerify] .env GEMINI_API_KEY 없음"); return; }
            Debug.Log($"[ModeReportVerify] Gemini 기능 확인 시작 (모델 {GeminiConfig.Model})");
            var sb = new StringBuilder();
            int ok = 0, total = 0;
            void Report(string name, bool pass, string detail)
            {
                total++; if (pass) ok++;
                sb.AppendLine($"{(pass ? "PASS" : "FAIL")}  {name}: {detail}");
            }
            string Clip(string s) => string.IsNullOrEmpty(s) ? "(없음)" : (s.Length > 60 ? s.Substring(0, 60) + "..." : s).Replace('\n', ' ');
            // 무료 등급은 모델당 분당 횟수 제한이 있어(2.5-flash는 1분에 5번이었다) 요청 사이를 띄운다
            Cysharp.Threading.Tasks.UniTask Gap() =>
                Cysharp.Threading.Tasks.UniTask.Delay(TimeSpan.FromSeconds(13), Cysharp.Threading.Tasks.DelayType.Realtime);

            // 점원 대화: 훈련 씬과 같은 도구 목록·시스템 프롬프트
            string decls = null;
            var tools = Resources.Load<TextAsset>("AAC/dialogue_tools");
            if (tools != null) decls = Newtonsoft.Json.Linq.JObject.Parse(tools.text)["function_declarations"]?.ToString(Newtonsoft.Json.Formatting.None);
            var prompts = Resources.Load<TextAsset>("AAC/system_prompts");
            var provider = prompts != null ? new Artti.Training.SystemPromptProvider(prompts.text) : null;
            string system = provider?.BuildSystemPrompt(Artti.AAC.ScenarioIds.Convenience) ?? "너는 편의점 점원이다.";
            string persona = provider?.BuildPersona(Artti.AAC.ScenarioIds.Convenience) ?? "너는 편의점 점원이다.";
            var dialogue = new Artti.Training.GeminiDialogueService(key, decls);
            try
            {
                var turn = await dialogue.RequestNextTurnAsync(system,
                    "[active_objective: greeting] [attempt: 1, scaffold_level: 0]\nUser selected card 'greet_hello' (안녕하세요) and said: \"안녕하세요\"");
                Report("점원 대화(도구 호출)", turn != null && turn.HasSpeech, turn == null ? "응답 없음" : $"{turn.Tool} / {Clip(turn.NpcSpeech)}");
                await Gap();
                var free = await dialogue.RequestFreeTalkAsync(persona, "안녕하세요, 생수 어디 있어요?");
                Report("점원 자유 대화", !string.IsNullOrWhiteSpace(free), Clip(free));
                await Gap();
                int match = await dialogue.RequestPhraseMatchAsync("샘수 어디 이써요", new[] { "카드로 할게요", "생수 어디에 있나요?", "봉투 주세요" });
                Report("말 뜻 맞추기", match == 1, $"고른 번호 {match} (정답 1)");

                // 리포트 문구: 예시 기록으로 요약을 만들어 넣는다(폴백인지 보려고 LLM 경로를 직접 부른다)
                string log = Path.Combine(Path.GetTempPath(), "artti_verify_" + Guid.NewGuid().ToString("N") + ".jsonl");
                try
                {
                    WriteSampleLog(log);
                    var data = new ReportDataService(log);
                    var overview = data.GetOverview();
                    var enc = new ReportEncouragementService(key);
                    var tryLlm = typeof(ReportEncouragementService).GetMethod("TryGenerateLlmAsync", Any);
                    await Gap();
                    var cheer = tryLlm != null
                        ? await (Cysharp.Threading.Tasks.UniTask<string>)tryLlm.Invoke(enc, new object[] { overview, "테스트", false, System.Threading.CancellationToken.None })
                        : null;
                    Report("리포트 응원 문구", !string.IsNullOrWhiteSpace(cheer), Clip(cheer));
                    var ct = default(System.Threading.CancellationToken);
                    var tryFeedback = typeof(ReportEncouragementService).GetMethod("TryGenerateFeedbackLlmAsync", Any);
                    await Gap();
                    var feedback = tryFeedback != null
                        ? await (Cysharp.Threading.Tasks.UniTask<string>)tryFeedback.Invoke(enc, new object[] { overview, "테스트", ct })
                        : null;
                    Report("리포트 AI 피드백", !string.IsNullOrWhiteSpace(feedback), Clip(feedback));
                    var tryMissions = typeof(ReportEncouragementService).GetMethod("TryGenerateMissionsLlmAsync", Any);
                    await Gap();
                    var missions = tryMissions != null
                        ? await (Cysharp.Threading.Tasks.UniTask<ReportEncouragementService.Missions?>)tryMissions.Invoke(enc, new object[] { overview, "테스트", ct })
                        : null;
                    Report("리포트 미션", missions.HasValue, missions.HasValue ? $"오늘 {Clip(missions.Value.daily)} / 이번 주 {Clip(missions.Value.weekly)}" : "응답 없음");
                    var sessions = data.GetSessions();
                    var detail = sessions.Count > 0 ? data.GetRecordReport(sessions[0].sessionId) : null;
                    await Gap();
                    var fb = detail != null ? await new ReportFeedbackService(key).GenerateAsync(detail, "테스트") : null;
                    Report("세션 상세 피드백", fb != null, fb == null ? "응답 없음" : Clip(fb.main));
                }
                finally { if (File.Exists(log)) File.Delete(log); }
            }
            catch (Exception e) { Report("예외", false, e.Message); }
            var msg = $"[ModeReportVerify] Gemini 기능 {ok}/{total} (모델 {GeminiConfig.Model})\n{sb}";
            if (ok == total) Debug.Log(msg); else Debug.LogError(msg);
        }

        // ===== 대화 모드 =====
        static void VerifyConversation(ReportDataService service, string outDir, Action<string, bool, string> check, StringBuilder report)
        {
            var view = OpenView<ConversationReportView>(ScenePaths.ConversationReport, service);
            var so = new SerializedObject(view);
            var tabs = Objects<Button>(so, "tabButtons");
            var values = Objects<TMP_Text>(so, "statValues");
            var chips = Objects<GameObject>(so, "statChips");
            var comments = Objects<TMP_Text>(so, "comments");
            for (int p = 0; p < 3; p++)
            {
                tabs[p].onClick.Invoke();   // 실제 탭 버튼(오늘은 처음 상태라 누르지 않은 것과 같다)
                var period = (ReportPeriod)p;
                var d = ConversationReportBuilder.Build(service, period, DateTime.Now);
                var c = d.current;
                string tab = TabNames[p];
                report.AppendLine($"-- 대화 [{tab}] 세션 {c.sessions}, 완료 {c.completed}, 자연스러움 {c.naturalness}, 종합 {c.Overall}");
                check($"대화 {tab}: 탭 상태", Period(view) == period, "");
                check($"대화 {tab}: 총 대화 시간", values[0].text == (c.sessions > 0 ? ModeReportText.Value(ModeReportText.Minutes(c.talkSeconds), "분") : "-"), values[0].text);
                check($"대화 {tab}: 자연스러움", values[1].text == ModeReportText.Value(c.naturalness, "%"), values[1].text);
                check($"대화 {tab}: 완료한 상황", values[3].text == (c.sessions > 0 ? ModeReportText.Value(c.completed, "개") : "-"), values[3].text);
                check($"대화 {tab}: 변화 칩(전체 탭은 숨김)", chips[0].activeSelf == ConversationReportLabels.TalkDelta(d).HasValue,
                    chips[0].activeSelf ? "보임" : "숨김");
                check($"대화 {tab}: AI 코멘트", comments[0].text == ConversationReportLabels.Comments(c, d.latestEvaluation)[0], comments[0].text);
                int shown = CountActiveRows(so);
                check($"대화 {tab}: 대화 기록 말풍선 수", shown == Mathf.Min(4, d.lines.Count), $"{shown}개");

                // '전체 보기' 버튼 -> 창이 열리고 그 대화의 모든 말이 줄로 나온다
                var viewAll = (Button)so.FindProperty("viewAllBtn").objectReferenceValue;
                var overlay = (GameObject)so.FindProperty("historyOverlay").objectReferenceValue;
                var content = (RectTransform)so.FindProperty("historyContent").objectReferenceValue;
                viewAll.onClick.Invoke();
                var all = ConversationReportBuilder.Transcript(service, d.latestSessionId).lines.Count;
                int rows = content.Cast<Transform>().Count(t => t.gameObject.activeSelf);
                check($"대화 {tab}: 전체 보기 창 열림", overlay.activeSelf, "");
                check($"대화 {tab}: 전체 보기 말 수", rows == all, $"{rows} / {all}");
                ShowNow(overlay);
                Capture(outDir, $"verify_conversation_{tab}_viewall.png");
                overlay.SetActive(false);
                Capture(outDir, $"verify_conversation_{tab}.png");
            }
        }

        // ===== 정확도 모드 =====
        static void VerifyAccuracy(ReportDataService service, string outDir, Action<string, bool, string> check, StringBuilder report)
        {
            var view = OpenView<AccuracyReportView>(ScenePaths.AccuracyReport, service);
            var so = new SerializedObject(view);
            var tabs = Objects<Button>(so, "tabButtons");
            var values = Objects<TMP_Text>(so, "statValues");
            var total = (TMP_Text)so.FindProperty("difficultTotal").objectReferenceValue;
            var play = (Button)so.FindProperty("modelPlayBtn").objectReferenceValue;
            var rowsProp = so.FindProperty("difficultRows");
            for (int p = 0; p < 3; p++)
            {
                tabs[p].onClick.Invoke();
                var period = (ReportPeriod)p;
                var d = AccuracyReportBuilder.Build(service, period, DateTime.Now);
                var c = d.current;
                string tab = TabNames[p];
                report.AppendLine($"-- 정확도 [{tab}] 세션 {c.sessions}, 발음 {c.pronunciation}, 반복 {c.repeats}, 어려운 발음 {c.difficult.Count}, 종합 {c.Overall}");
                check($"정확도 {tab}: 탭 상태", Period(view) == period, "");
                check($"정확도 {tab}: 발음 정확도", values[1].text == ModeReportText.Value(c.pronunciation, "%"), values[1].text);
                check($"정확도 {tab}: 반복 연습", values[2].text == (c.sessions > 0 ? ModeReportText.Value(c.repeats, "회") : "-"), values[2].text);
                check($"정확도 {tab}: 어려운 발음 수", total.text == $"총 {c.difficult.Count}개", total.text);
                int shownRows = 0;
                for (int i = 0; i < rowsProp.arraySize; i++)
                {
                    var root = rowsProp.GetArrayElementAtIndex(i).FindPropertyRelative("root").objectReferenceValue as GameObject;
                    if (root != null && root.activeSelf) shownRows++;
                }
                check($"정확도 {tab}: 발음 목록 줄 수", shownRows == Mathf.Min(rowsProp.arraySize, c.difficult.Count), $"{shownRows}줄");
                check($"정확도 {tab}: 모범 발음 재생 버튼", play.interactable == d.modelSentences.Count > 0, string.Join(" / ", d.modelSentences));
                Capture(outDir, $"verify_accuracy_{tab}.png");
            }

            // 비교 듣기 화면 글자: 첫 줄 설명 자리에 '이렇게 들렸어요 → ..' / '바르게는 → ..'를 넣어 한 줄에 들어가는지 찍는다
            var all = AccuracyReportBuilder.Build(service, ReportPeriod.All, DateTime.Now).current.difficult;
            var desc = rowsProp.GetArrayElementAtIndex(0).FindPropertyRelative("description").objectReferenceValue as TMP_Text;
            if (all.Count > 0 && desc != null)
            {
                desc.text = AccuracyReportLabels.HeardLine(all[0]);
                desc.ForceMeshUpdate();
                check("정확도 비교 듣기: 들린 말 한 줄", !desc.isTextTruncated, desc.text);
                Capture(outDir, "verify_accuracy_compare_heard.png");
                desc.text = AccuracyReportLabels.RightLine(all[0]);
                desc.ForceMeshUpdate();
                check("정확도 비교 듣기: 바른 말 한 줄", !desc.isTextTruncated, desc.text);
                Capture(outDir, "verify_accuracy_compare_right.png");
            }
        }

        // 씬을 열고 View의 Awake(버튼 연결)를 부른 뒤, 기록 파일 대신 예시 기록으로 첫 화면을 채운다(실행 시 Start와 같은 순서)
        static T OpenView<T>(string scenePath, ReportDataService service) where T : ModeReportView
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var view = Object.FindFirstObjectByType<T>();
            Capture(null, null);   // 캔버스를 기기 크기로 먼저 맞춘다
            typeof(T).GetMethod("Awake", Any)?.Invoke(view, null);
            typeof(ModeReportView).GetProperty("Service", Any)?.SetValue(view, service);
            typeof(T).GetMethod("Refresh", Any)?.Invoke(view, new object[] { true });
            return view;
        }

        static ReportPeriod Period(ModeReportView view) =>
            (ReportPeriod)typeof(ModeReportView).GetProperty("Period", Any).GetValue(view);

        static int CountActiveRows(SerializedObject so)
        {
            var rows = so.FindProperty("rows");
            int n = 0;
            for (int i = 0; i < rows.arraySize; i++)
            {
                var e = rows.GetArrayElementAtIndex(i);
                if ((e.FindPropertyRelative("npcRoot").objectReferenceValue as GameObject)?.activeSelf == true) n++;
                if ((e.FindPropertyRelative("userRoot").objectReferenceValue as GameObject)?.activeSelf == true) n++;
            }
            return n;
        }

        // 에디터에서는 열기 연출(투명 -> 보임)이 진행되지 않으므로 다 열린 모습으로 맞춰 찍는다
        static void ShowNow(GameObject overlay)
        {
            if (overlay.TryGetComponent<CanvasGroup>(out var g)) g.alpha = 1f;
            var card = overlay.transform.Find("Card");
            if (card != null) card.localScale = Vector3.one;
        }

        static T[] Objects<T>(SerializedObject so, string prop) where T : Object
        {
            var p = so.FindProperty(prop);
            var list = new T[p.arraySize];
            for (int i = 0; i < p.arraySize; i++) list[i] = p.GetArrayElementAtIndex(i).objectReferenceValue as T;
            return list;
        }

        static void Capture(string outDir, string file)
        {
            string path = outDir == null ? Path.Combine(Path.GetTempPath(), "artti_verify_warmup.png") : Path.Combine(outDir, file);
            DeviceLayoutCapture.CaptureActiveScene(2560, 1600, path);
        }

        // ===== 예시 기록: 대화 모드 4번(오늘 2, 어제 1, 8일 전 1), 정확도 모드 3번(오늘, 어제, 8일 전) =====
        static void WriteSampleLog(string path)
        {
            var events = new List<AACEvent>();
            var today = DateTime.Today;
            long Ms(DateTime t) => new DateTimeOffset(t).ToUnixTimeMilliseconds();
            void Ev(string sid, AACEventType type, long ms, string obj = null, string payload = null) =>
                events.Add(new AACEvent
                {
                    profileId = "verify", sessionId = sid, eventType = type.ToString(), scenarioId = "convenience",
                    objectiveId = obj, timestampUnixMs = ms, payloadJson = payload,
                });
            string Voice(string text, string heard, int pct, string light) =>
                $"guided_voice:{text}|stt:{heard}|clarity:{pct}:{light}:{text}";

            void Conversation(string sid, DateTime start, int greetPct, bool second, bool completed, int seconds)
            {
                long t = Ms(start);
                Ev(sid, AACEventType.ScenarioEntered, t, payload: "mode:conversation");
                Ev(sid, AACEventType.ObjectiveEntered, t, "greeting");
                Ev(sid, AACEventType.TtsPlayed, t + 1000, payload: "Speak:어서 오세요. 무엇을 찾으세요?");
                Ev(sid, AACEventType.CardSelected, t + 5000, payload: Voice("안녕하세요", "안녕하세요", greetPct, "green"));
                if (second)
                {
                    Ev(sid, AACEventType.ObjectiveEntered, t + 6000, "select_items");
                    Ev(sid, AACEventType.TtsPlayed, t + 7000, payload: "Speak:찾으시는 물건 있으세요?");
                    Ev(sid, AACEventType.CardSelected, t + 19000, payload: Voice("생수 어디에 있나요?", "생수 어디 있나요", 60, "yellow"));
                    Ev(sid, AACEventType.StepRetryAttempt, t + 19500, "select_items");
                    Ev(sid, AACEventType.TtsPlayed, t + 20000, payload: "Speak:오른쪽 음료 코너에 있어요.");
                    Ev(sid, AACEventType.CardSelected, t + 30000, payload: "product_water:생수 주세요");
                    Ev(sid, AACEventType.TtsPlayed, t + 31000, payload: "Speak:감사합니다. 안녕히 가세요!");
                }
                if (completed) Ev(sid, AACEventType.SessionEnded, t + seconds * 1000L, payload: "completed");
                else Ev(sid, AACEventType.SessionAbandoned, t + seconds * 1000L, "greeting");
            }

            void Accuracy(string sid, DateTime start, bool hard, int seconds)
            {
                long t = Ms(start);
                Ev(sid, AACEventType.ScenarioEntered, t, payload: "mode:accuracy");
                Ev(sid, AACEventType.ObjectiveEntered, t, "select_items");
                Ev(sid, AACEventType.TtsPlayed, t + 1000, payload: "Speak:찾으시는 물건 있으세요?");
                if (hard)
                {
                    Ev(sid, AACEventType.CardSelected, t + 5000, payload: Voice("사과 주세요", "아과 주세요", 55, "red"));
                    Ev(sid, AACEventType.StepRetryAttempt, t + 5500, "select_items");
                    Ev(sid, AACEventType.CardSelected, t + 12000, payload: Voice("사과 주세요", "사과 주세요", 95, "green"));
                    Ev(sid, AACEventType.ObjectiveEntered, t + 13000, "checkout");
                    Ev(sid, AACEventType.TtsPlayed, t + 14000, payload: "Speak:어디 가세요?");
                    Ev(sid, AACEventType.CardSelected, t + 18000, payload: Voice("학교 가요", "하교 가요", 60, "yellow"));
                }
                else Ev(sid, AACEventType.CardSelected, t + 4000, payload: Voice("물 주세요", "물 주세요", 92, "green"));
                Ev(sid, AACEventType.SessionEnded, t + seconds * 1000L, payload: "completed");
            }

            Conversation("c_today1", today.AddMinutes(30), 90, true, true, 180);
            Conversation("c_today2", today.AddMinutes(90), 80, false, false, 60);
            Conversation("c_yesterday", today.AddDays(-1).AddHours(10), 70, false, true, 120);
            Conversation("c_old", today.AddDays(-8).AddHours(10), 50, false, true, 60);
            Accuracy("a_today", today.AddMinutes(40), true, 240);
            Accuracy("a_yesterday", today.AddDays(-1).AddHours(11), false, 120);
            Accuracy("a_old", today.AddDays(-8).AddHours(11), true, 90);
            File.WriteAllLines(path, events.Select(e => JsonUtility.ToJson(e)));
        }
    }
}

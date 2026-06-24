using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;
using Artti.Common.Speech;

namespace Artti.UI
{
    // 레포트 캐릭터 연출 상태머신.
    // 흐름: 화면 밖 walk 입장 -> 정위치 도착 idle -> 말풍선+TTS(talk 루프) -> full_motion 1회 -> idle 복귀.
    // blink는 클로즈업 크롭이라 전신 프레임과 합성 불가 -> 현재 제외(요구사항 결정).
    // full_motion은 한 종류뿐 -> 성적 차등은 모션이 아니라 말풍선 문구/색으로 표현.
    // 각 클립은 픽셀 크기가 달라 발(bottom pivot) 기준으로 정렬해 크기 튐을 막는다.
    [RequireComponent(typeof(Image))]
    public class ReportCharacterDirector : MonoBehaviour
    {
        [Serializable]
        public class Clip
        {
            public Sprite[] frames;
            public float fps = 12f;
            public Vector2 size;          // 표시 크기 (빌더가 클립 높이 정규화해 계산)
        }

        [Header("클립")]
        [SerializeField] private Clip walk;
        [SerializeField] private Clip idle;
        [SerializeField] private Clip talk;
        [SerializeField] private Clip full;

        [Header("배치 (발 기준, pivot 0.5/0)")]
        [SerializeField] private Vector2 homeFeet = new Vector2(-664f, -750f);
        [SerializeField] private float offscreenX = -2200f; // 화면 왼쪽 밖 시작점
        [SerializeField] private float walkInSeconds = 1.6f;
        [SerializeField] private bool flipWalkX = false;    // walk 프레임이 진행 방향과 반대면 체크
        // walk 프레임을 시간이 아니라 이동 거리에 동기화 -> 디딘 발이 바닥에 고정(스케이트 방지).
        // 한 보행 사이클(전체 프레임)당 이동 픽셀. 작을수록 보폭 짧고 미끄러짐 감소.
        [SerializeField] private float walkPixelsPerCycle = 300f;

        [Header("말풍선")]
        [SerializeField] private GameObject speechBubble;
        [SerializeField] private TMP_Text speechLabel;
        [SerializeField] private float minTalkSeconds = 1.5f; // TTS 실패/무음 시 최소 노출 시간

        [Header("TTS")]
        [SerializeField] private AudioSource audioSource;

        private SpriteSequencePlayer _player;
        private RectTransform _rect;
        private ITtsService _tts;
        private bool _started;
        private bool _suppressed; // 전체보기 등에서 말풍선 억제 (연출 비동기가 다시 켜는 것 방지)

        // 외부(ReportView)에서 말풍선 표시 억제. true면 즉시 숨기고 talk 단계에서도 안 띄움.
        public void SetSuppressed(bool v)
        {
            _suppressed = v;
            if (v && speechBubble != null) speechBubble.SetActive(false);
        }

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _player = GetComponent<SpriteSequencePlayer>();
            if (_player == null) _player = gameObject.AddComponent<SpriteSequencePlayer>();

            // 시작 전 대기 상태: 말풍선 숨기고 캐릭터는 화면 밖 walk 첫 프레임.
            if (speechBubble != null) speechBubble.SetActive(false);
            ApplyClipRect(walk);
            SetFacing(forward: false);
            _rect.anchoredPosition = new Vector2(offscreenX, homeFeet.y);
        }

        // 외부(ReportView)에서 레포트 기반 문구 생성 Task를 넘겨 호출. 1회만 동작.
        // messageTask는 LLM 호출이라 지연될 수 있어, walk 입장과 병렬로 두고 talk 직전에 대기한다.
        public void Play(UniTask<(string message, bool isHighScore)> messageTask)
        {
            if (_started) return;
            _started = true;
            RunAsync(messageTask, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid RunAsync(UniTask<(string message, bool isHighScore)> messageTask, CancellationToken ct)
        {
            try
            {
                // 1) walk 입장 (이 사이 messageTask는 백그라운드로 진행)
                await WalkInAsync(ct);

                // 2) idle 정착
                PlayLoop(idle);

                // 3) 레포트 기반 문구 확보 (아직이면 idle 상태로 대기)
                var m = await SafeAwaitMessage(messageTask, ct);

                // 4) 말풍선 + TTS + talk 루프
                await TalkAsync(m.message, ct);

                // 5) full_motion 1회 (성적 무관 공통, 차등은 문구에서)
                await PlayOnceAsync(full, ct);
            }
            catch (OperationCanceledException) { return; }
            finally
            {
                // 6) idle 복귀 (취소가 아니면)
                if (!ct.IsCancellationRequested) PlayLoop(idle);
            }
        }

        private static async UniTask<(string message, bool isHighScore)> SafeAwaitMessage(
            UniTask<(string message, bool isHighScore)> messageTask, CancellationToken ct)
        {
            try { return await messageTask.AttachExternalCancellation(ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogWarning($"[ReportCharacter] 문구 생성 실패(폴백): {e.Message}");
                return ("정말 잘하고 있어요!\n계속 파이팅해요!", false);
            }
        }

        private async UniTask WalkInAsync(CancellationToken ct)
        {
            ApplyClipRect(walk);
            SetFacing(forward: !flipWalkX); // 진행 방향(우측 화면 안쪽)을 바라보도록
            _player.Stop(); // 시간 기반 자동재생 끄고 거리 동기화로 직접 구동

            Vector2 start = new Vector2(offscreenX, homeFeet.y);
            Vector2 end = new Vector2(homeFeet.x, homeFeet.y);
            int n = (walk != null && walk.frames != null) ? walk.frames.Length : 0;
            float ppc = Mathf.Max(1f, walkPixelsPerCycle);

            float t = 0f;
            while (t < walkInSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / walkInSeconds));
                var pos = Vector2.Lerp(start, end, k);
                _rect.anchoredPosition = pos;

                // 프레임을 이동 거리에 맞춰 전진 -> 가감속과 무관하게 디딘 발이 바닥에 고정.
                if (n > 0)
                {
                    float traveled = Mathf.Abs(pos.x - start.x);
                    int idx = Mathf.FloorToInt(traveled / ppc * n);
                    _player.ShowFrameExternal(walk.frames, idx);
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            _rect.anchoredPosition = end;
            SetFacing(forward: true);
        }

        private async UniTask TalkAsync(string message, CancellationToken ct)
        {
            if (speechLabel != null && !string.IsNullOrEmpty(message)) speechLabel.text = message;
            if (speechBubble != null && !_suppressed) speechBubble.SetActive(true);

            PlayLoop(talk);

            // TTS 발화와 최소 노출 시간을 함께 대기.
            var minDelay = UniTask.Delay(TimeSpan.FromSeconds(minTalkSeconds), cancellationToken: ct);
            var speak = SpeakAsync(message, ct);
            await UniTask.WhenAll(minDelay, speak);
        }

        private async UniTask SpeakAsync(string message, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            try
            {
                _tts ??= BuildTts();
                if (_tts != null) await _tts.SpeakAsync(message, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogWarning($"[ReportCharacter] TTS 실패(무시): {e.Message}");
            }
        }

        private ITtsService BuildTts()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }
            // 훈련모드와 동일: TTS는 Cloud(ko-KR 단말 미지원 회피), 키는 .env. 키 없으면 NoopTts로 폴백.
            var ttsKey = ApiKeyLoader.GetOrFallback(ApiKeyLoader.GoogleTtsApi, ApiKeyLoader.GeminiApi);
            if (string.IsNullOrEmpty(ttsKey)) return new NoopTtsService();
            return new CloudTtsService(ttsKey, audioSource);
        }

        // ===== 클립 적용 헬퍼 =====

        private void PlayLoop(Clip c)
        {
            ApplyClipRect(c);
            _player.Play(c?.frames, c != null ? c.fps : 12f, loop: true);
        }

        private async UniTask PlayOnceAsync(Clip c, CancellationToken ct)
        {
            ApplyClipRect(c);
            var tcs = new UniTaskCompletionSource();
            _player.Play(c?.frames, c != null ? c.fps : 12f, loop: false, onComplete: () => tcs.TrySetResult());
            using (ct.Register(() => tcs.TrySetResult()))
                await tcs.Task;
        }

        // 클립별 표시 크기 적용. pivot은 발(0.5,0)이라 anchoredPosition.y가 그대로 발 높이.
        private void ApplyClipRect(Clip c)
        {
            if (c == null || _rect == null) return;
            _rect.pivot = new Vector2(0.5f, 0f);
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
            if (c.size.sqrMagnitude > 1f) _rect.sizeDelta = c.size;
        }

        private void SetFacing(bool forward)
        {
            var s = _rect.localScale;
            s.x = Mathf.Abs(s.x) * (forward ? 1f : -1f);
            _rect.localScale = s;
        }
    }
}

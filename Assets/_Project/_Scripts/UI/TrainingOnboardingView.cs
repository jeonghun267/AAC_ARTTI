using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    /// <summary>
    /// 훈련 화면 둘러보기(온보딩). 화면 기능을 하나씩 밝게 짚고(나머지는 어둡게) 설명 문장을 보여 준다.
    /// 처음 들어왔을 때는 환영 창(ShowWelcome)을 먼저 띄워 둘러볼지 바로 시작할지 고르게 한다.
    /// 문장 읽어 주기, 첫 진입 판정, 훈련 시작 시점은 TrainingSceneRoot가 OnStepShown / OnClosed로 처리한다.
    /// 둘러보는 동안에는 전체를 덮는 막이 입력을 받아, 짚은 버튼을 눌러도 동작하지 않는다.
    /// </summary>
    public sealed class TrainingOnboardingView : MonoBehaviour
    {
        [Header("화면")]
        [SerializeField] private GameObject root;               // 화면 전체(막 + 어둡게 + 테두리 + 설명 창)
        [SerializeField] private RectTransform dimTop;
        [SerializeField] private RectTransform dimBottom;
        [SerializeField] private RectTransform dimLeft;
        [SerializeField] private RectTransform dimRight;
        [SerializeField] private RectTransform frame;           // 짚은 곳 둘레 빛 테두리
        [SerializeField] private RectTransform card;            // 설명 창
        [SerializeField] private TMP_Text counterText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextLabel;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button replayButton;          // 도움말 버튼: 누를 때마다 처음부터 다시

        [Header("단계")]
        [SerializeField] private RectTransform[] targets;
        [SerializeField] private string[] accuracyTexts;
        [SerializeField] private string[] conversationTexts;   // 비어 있는 칸은 accuracyTexts를 쓴다
        [SerializeField] private float padding = 10f;
        [SerializeField] private float cardGap = 18f;
        // 설명 창이 가리면 안 되는 곳(점원 얼굴 등). 짚은 곳 다음으로 피한다.
        [SerializeField] private RectTransform[] avoidAreas;

        [Header("처음 들어왔을 때: 환영 창")]
        [SerializeField] private GameObject welcomeRoot;       // 어두운 막 + 환영 창(글은 그림에 들어 있다)
        [SerializeField] private RectTransform welcomeCard;
        [SerializeField] private Button welcomeTourButton;     // 화면 둘러보기 -> 둘러보기 시작
        [SerializeField] private Button welcomeStartButton;    // 바로 시작하기 -> 둘러보기 없이 훈련 시작
        [SerializeField] private Button welcomeCloseButton;    // 그림 속 X = 바로 시작하기
        [SerializeField, TextArea] private string welcomeSpeech;

        // 보여 준 문장(환영 창 포함). 받는 쪽이 읽어 준다.
        public event Action<string> OnStepShown;
        public event Action OnClosed;
        public bool IsOpen => root != null && root.activeSelf;

        const float MoveSeconds = 0.28f;
        const float EdgeMargin = 16f;
        const float FramePulseHz = 0.6f;   // 숨 쉬듯 천천히(번쩍임 없음)

        private RectTransform _rootRect;
        private Graphic _frameGraphic;
        private readonly Vector3[] _corners = new Vector3[4];
        private bool _conversationMode;
        private int _index = -1;
        private Rect _hole;
        private Vector2 _cardPos;
        private CancellationTokenSource _moveCts;
        private float _pulseTime;

        private void Awake()
        {
            if (root != null) _rootRect = (RectTransform)root.transform;
            if (frame != null) _frameGraphic = frame.GetComponent<Graphic>();
            if (nextButton != null) nextButton.onClick.AddListener(Next);
            if (skipButton != null) skipButton.onClick.AddListener(Close);
            if (replayButton != null) replayButton.onClick.AddListener(Show);
            if (welcomeTourButton != null) welcomeTourButton.onClick.AddListener(StartTourFromWelcome);
            if (welcomeStartButton != null) welcomeStartButton.onClick.AddListener(SkipFromWelcome);
            if (welcomeCloseButton != null) welcomeCloseButton.onClick.AddListener(SkipFromWelcome);
            UIPressScale.Attach(nextButton);
            UIPressScale.Attach(skipButton);
            UIPressScale.Attach(replayButton);
            UIPressScale.Attach(welcomeTourButton);
            UIPressScale.Attach(welcomeStartButton);
            if (root != null) root.SetActive(false);
            if (welcomeRoot != null) welcomeRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (nextButton != null) nextButton.onClick.RemoveListener(Next);
            if (skipButton != null) skipButton.onClick.RemoveListener(Close);
            if (replayButton != null) replayButton.onClick.RemoveListener(Show);
            if (welcomeTourButton != null) welcomeTourButton.onClick.RemoveListener(StartTourFromWelcome);
            if (welcomeStartButton != null) welcomeStartButton.onClick.RemoveListener(SkipFromWelcome);
            if (welcomeCloseButton != null) welcomeCloseButton.onClick.RemoveListener(SkipFromWelcome);
            UIFx.Cancel(ref _moveCts);
        }

        // 정확도 모드 / 대화 모드에 따라 말하기 단계 문장이 다르다
        public void SetMode(bool conversationMode) => _conversationMode = conversationMode;

        /// <summary>처음 들어왔을 때: 환영 창을 먼저 띄운다. 환영 창이 없으면 바로 둘러보기.</summary>
        public void ShowWelcome()
        {
            if (welcomeRoot == null)
            {
                Show();
                return;
            }
            _welcomeOpen = true;
            UIPopupMotion.Open(welcomeRoot, welcomeCard);
            OnStepShown?.Invoke(welcomeSpeech);
        }

        // 닫히는 연출 중에 두 버튼을 잇달아 눌러도 한 번만 처리한다
        private bool _welcomeOpen;

        private void StartTourFromWelcome()
        {
            if (!_welcomeOpen) return;
            _welcomeOpen = false;
            UIPopupMotion.Close(welcomeRoot);
            Show();
        }

        private void SkipFromWelcome()
        {
            if (!_welcomeOpen) return;
            _welcomeOpen = false;
            UIPopupMotion.Close(welcomeRoot);
            OnClosed?.Invoke();
        }

        public void Show()
        {
            if (root == null || StepCount == 0) return;
            _index = -1;
            UIPopupMotion.Open(root, card);
            GoTo(0, animate: false);
        }

        private void Next()
        {
            if (_index + 1 >= StepCount) Close();
            else GoTo(_index + 1, animate: true);
        }

        private void Close()
        {
            if (!IsOpen) return;
            UIFx.Cancel(ref _moveCts);
            UIPopupMotion.Close(root);
            OnClosed?.Invoke();
        }

        private void Update()
        {
            if (_frameGraphic == null || !IsOpen) return;
            _pulseTime += Time.unscaledDeltaTime;
            float wave = 0.5f + 0.5f * Mathf.Cos(_pulseTime * FramePulseHz * 2f * Mathf.PI);
            _frameGraphic.canvasRenderer.SetAlpha(Mathf.Lerp(0.55f, 1f, wave));
        }

        private int StepCount => targets != null ? targets.Length : 0;

        private void GoTo(int index, bool animate)
        {
            _index = index;
            string text = TextFor(index);
            if (counterText != null) counterText.text = $"{index + 1} / {StepCount}";
            if (bodyText != null) bodyText.text = text;
            if (nextLabel != null) nextLabel.text = index + 1 >= StepCount ? "시작하기" : "다음";

            Canvas.ForceUpdateCanvases();
            var hole = TargetRect(index);
            var cardPos = PlaceCard(hole);
            if (!animate)
            {
                ApplyLayout(hole, cardPos);
            }
            else
            {
                var fromHole = _hole;
                var fromCard = _cardPos;
                var ct = UIFx.Restart(ref _moveCts, this);
                UIFx.Tween(this, 0f, MoveSeconds, k =>
                {
                    float e = UIFx.EaseOutCubic(k);
                    ApplyLayout(LerpRect(fromHole, hole, e), Vector2.Lerp(fromCard, cardPos, e));
                }, ct).SuppressCancellationThrow().Forget();
            }
            OnStepShown?.Invoke(text);
        }

        /// <summary>에디터 캡처용: 연출 없이 해당 단계 모습으로 바로 맞춘다.</summary>
        public void PreviewStep(int index, bool conversationMode)
        {
            if (root == null || index < 0 || index >= StepCount) return;
            if (_rootRect == null) _rootRect = (RectTransform)root.transform;
            _conversationMode = conversationMode;
            root.SetActive(true);
            _index = index;
            if (counterText != null) counterText.text = $"{index + 1} / {StepCount}";
            if (bodyText != null) bodyText.text = TextFor(index);
            if (nextLabel != null) nextLabel.text = index + 1 >= StepCount ? "시작하기" : "다음";
            Canvas.ForceUpdateCanvases();
            var hole = TargetRect(index);
            ApplyLayout(hole, PlaceCard(hole));
        }

        private string TextFor(int index)
        {
            string conversation = _conversationMode && conversationTexts != null && index < conversationTexts.Length
                ? conversationTexts[index] : null;
            if (!string.IsNullOrWhiteSpace(conversation)) return conversation;
            return accuracyTexts != null && index < accuracyTexts.Length ? accuracyTexts[index] : string.Empty;
        }

        // 짚을 대상의 사각형(막 기준 좌표, 가운데가 0) + 여백
        private Rect TargetRect(int index)
        {
            var target = targets[index];
            if (target == null) return new Rect(0f, 0f, 0f, 0f);
            target.GetWorldCorners(_corners);
            Vector2 min = _rootRect.InverseTransformPoint(_corners[0]);
            Vector2 max = _rootRect.InverseTransformPoint(_corners[2]);
            return Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding);
        }

        // 설명 창을 짚은 곳을 가리지 않는 쪽에 둔다. 가로로 긴 대상은 위·아래, 세로로 긴 대상은 옆을 먼저 본다.
        //   위·아래 자리는 가운데 -> 대상 왼쪽 맞춤 -> 오른쪽 맞춤 -> 화면 오른쪽 끝 -> 왼쪽 끝 순으로,
        //   짚은 곳과 피할 곳(점원 얼굴)을 모두 비키는 첫 자리를 고른다. 없으면 짚은 곳만 비키는 첫 자리.
        private Vector2 PlaceCard(Rect hole)
        {
            var bounds = _rootRect.rect;
            Vector2 size = card.rect.size;
            Vector2 half = size / 2f;
            float yBelow = hole.yMin - cardGap - half.y, yAbove = hole.yMax + cardGap + half.y;
            float xRight = hole.xMax + cardGap + half.x, xLeft = hole.xMin - cardGap - half.x;
            float farRight = bounds.xMax - EdgeMargin - half.x, farLeft = bounds.xMin + EdgeMargin + half.x;
            var below = new[]
            {
                new Vector2(hole.center.x, yBelow), new Vector2(hole.xMin + half.x, yBelow), new Vector2(hole.xMax - half.x, yBelow),
                new Vector2(farRight, yBelow), new Vector2(farLeft, yBelow),
            };
            var above = new[]
            {
                new Vector2(hole.center.x, yAbove), new Vector2(hole.xMin + half.x, yAbove), new Vector2(hole.xMax - half.x, yAbove),
                new Vector2(farRight, yAbove), new Vector2(farLeft, yAbove),
            };
            var sides = new[] { new Vector2(xRight, hole.center.y), new Vector2(xLeft, hole.center.y) };
            Vector2[][] order = hole.width >= hole.height ? new[] { below, above, sides } : new[] { sides, below, above };

            // 1) 짚은 곳 바로 옆(화면 끝에 밀려 다른 쪽으로 옮겨지지 않은 자리) + 얼굴도 비킴
            // 2) 바로 옆, 얼굴은 겹쳐도 됨  3) 짚은 곳만 비키는 아무 자리
            for (int pass = 0; pass < 3; pass++)
            foreach (var group in order)
            {
                bool vertical = group != sides;   // 위·아래 자리는 세로, 옆 자리는 가로가 밀리면 '바로 옆'이 아니다
                foreach (var c in group)
                {
                    var clamped = Clamp(c, half, bounds);
                    if (pass < 2 && Mathf.Abs(vertical ? clamped.y - c.y : clamped.x - c.x) > 1f) continue;
                    var rect = new Rect(clamped - half, size);
                    if (rect.Overlaps(hole)) continue;
                    if (pass == 0 && OverlapsAvoid(rect)) continue;
                    return clamped;
                }
            }
            return Clamp(Vector2.zero, half, bounds);
        }

        private bool OverlapsAvoid(Rect rect)
        {
            for (int i = 0; avoidAreas != null && i < avoidAreas.Length; i++)
            {
                var area = avoidAreas[i];
                if (area == null || !area.gameObject.activeInHierarchy) continue;
                area.GetWorldCorners(_corners);
                Vector2 min = _rootRect.InverseTransformPoint(_corners[0]);
                Vector2 max = _rootRect.InverseTransformPoint(_corners[2]);
                if (rect.Overlaps(Rect.MinMaxRect(min.x, min.y, max.x, max.y))) return true;
            }
            return false;
        }

        private static Vector2 Clamp(Vector2 center, Vector2 half, Rect bounds) => new Vector2(
            Mathf.Clamp(center.x, bounds.xMin + EdgeMargin + half.x, bounds.xMax - EdgeMargin - half.x),
            Mathf.Clamp(center.y, bounds.yMin + EdgeMargin + half.y, bounds.yMax - EdgeMargin - half.y));

        private void ApplyLayout(Rect hole, Vector2 cardPos)
        {
            _hole = hole;
            _cardPos = cardPos;
            var b = _rootRect.rect;
            SetRect(dimTop, b.xMin, hole.yMax, b.xMax, b.yMax);
            SetRect(dimBottom, b.xMin, b.yMin, b.xMax, hole.yMin);
            SetRect(dimLeft, b.xMin, hole.yMin, hole.xMin, hole.yMax);
            SetRect(dimRight, hole.xMax, hole.yMin, b.xMax, hole.yMax);
            SetRect(frame, hole.xMin, hole.yMin, hole.xMax, hole.yMax);
            if (card != null) card.anchoredPosition = cardPos;
        }

        // 막(가운데 기준) 좌표로 사각형을 놓는다. 대상 쪽 자식은 가운데 앵커·피벗이어야 한다.
        private static void SetRect(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            if (rt == null) return;
            rt.anchoredPosition = new Vector2((xMin + xMax) / 2f, (yMin + yMax) / 2f);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, yMax - yMin));
        }

        private static Rect LerpRect(Rect a, Rect b, float t) => Rect.MinMaxRect(
            Mathf.Lerp(a.xMin, b.xMin, t), Mathf.Lerp(a.yMin, b.yMin, t),
            Mathf.Lerp(a.xMax, b.xMax, t), Mathf.Lerp(a.yMax, b.yMax, t));
    }
}

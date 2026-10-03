using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;

namespace Artti.UI
{
    // 채팅창(도움/인사/결제/편의/주문 화면). 아래 입력 줄(openButton)을 누르면 화면 오른쪽에서 창(sheet)이 미끄러져 나온다.
    //   위 = 문장 입력(HelpTextInputController, 키보드 완료로 말하기), 아래 = 이미 말한 문장 목록(누르면 다시 말하기).
    //   왼쪽 어두운 칸(overlay)이나 닫기 버튼을 누르면 들어간다.
    //   목록은 지금 사용자(프로필)의 것을 ChatHistoryStore가 파일에 남긴다. 한 사용자의 목록은 모든 탭이 같이 쓴다.
    //   창 안은 그 화면 보드(board)와 같은 배율로 그려서(designWidth 보드 px) 태블릿/폰에서 글자 크기가 같다.
    //   자리/그림/연결은 에디터 스크립트(ARFieldHelpDetailLayout)가 채운다.
    [DisallowMultipleComponent]
    public sealed class HelpChatDrawerView : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Image overlay;           // 창 뒤 어두운 칸(누르면 닫힘, Button 포함)
        [SerializeField] private RectTransform sheet;     // 오른쪽 창(오른쪽 끝 기준, 위아래 꽉)
        [SerializeField] private RectTransform content;   // 창 안(보드 배율로 그림)
        [SerializeField] private RectTransform board;     // HelpBoard(BoardFitter가 배율을 정함)
        [SerializeField] private float designWidth = 760f;
        [SerializeField] private HelpTextInputController input;
        [SerializeField] private ScrollRect listScroll;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private Button itemTemplate;     // 꺼 둔 문장 칸(복제해 쓴다)
        [SerializeField] private GameObject emptyLabel;
        [SerializeField] private TTSManager tts;
        [SerializeField] private float slideSeconds = 0.3f;
        [SerializeField, Range(0f, 1f)] private float overlayAlpha = 0.45f;
        [SerializeField] private int maxItems = 30;

        private readonly List<(Button button, TMP_Text label)> _items = new List<(Button, TMP_Text)>();
        private CancellationTokenSource _slideCts;
        private float _t;              // 0 = 닫힘, 1 = 열림
        private float _scale = -1f;
        private float _sheetHeight = -1f;
        private bool _open;

        // 변경점: 열 때마다/말할 때마다 지금 사용자의 목록을 쓴다(사용자를 바꿔 들어와도 그 사람 목록)
        private ChatHistoryStore Store
        {
            get
            {
                var boot = AppBootstrap.Instance;
                string profileId = boot != null ? boot.ProfileManager?.ActiveProfile?.id : null;
                return ChatHistoryStore.ForUser(profileId, maxItems);
            }
        }

        private void Awake()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (overlay != null && overlay.TryGetComponent<Button>(out var overlayButton)) overlayButton.onClick.AddListener(Close);
            if (input != null) input.Spoken += OnSpoken;
            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (input != null) input.Spoken -= OnSpoken;
            CancelSlide();
        }

        // 도움 화면을 벗어나면 바로 닫는다
        private void OnDisable()
        {
            CancelSlide();
            _open = false;
            _t = 0f;
            SetShown(false);
        }

        private void LateUpdate()
        {
            if (!_open && _t <= 0f) return;
            float s = board != null ? board.localScale.x : 1f;
            float h = sheet.rect.height;
            if (!Mathf.Approximately(s, _scale) || !Mathf.Approximately(h, _sheetHeight)) Layout();
        }

        public void Open()
        {
            if (_open) return;
            _open = true;
            SetShown(true);
            Layout();
            Refresh();
            StartSlide(true);
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            if (input != null && input.InputField != null) input.InputField.DeactivateInputField();
            StartSlide(false);
        }

        // 에디터 캡처용: 바로 열린 모양으로 예시 문장을 보여 준다(저장하지 않음)
        public void PreviewOpen(IReadOnlyList<string> sample)
        {
            _open = true;
            _t = 1f;
            SetShown(true);
            Layout();
            Fill(sample);
            ApplyT();
        }

        private void OnSpoken(string text)
        {
            Store.Add(text);
            Refresh();
            if (listScroll != null) listScroll.verticalNormalizedPosition = 1f;   // 새 문장이 보이게 맨 위로
        }

        private void Speak(TMP_Text label)
        {
            if (tts == null || label == null || string.IsNullOrWhiteSpace(label.text)) return;
            tts.Speak(label.text);
        }

        private void Refresh() => Fill(Store.Items);

        // 문장 칸은 지우지 않고 다시 쓴다(모자라면 복제, 남으면 끈다)
        private void Fill(IReadOnlyList<string> texts)
        {
            if (listContent == null || itemTemplate == null) return;
            int count = texts != null ? texts.Count : 0;
            while (_items.Count < count)
            {
                var button = Instantiate(itemTemplate, listContent);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                button.onClick.AddListener(() => Speak(label));
                _items.Add((button, label));
            }
            for (int i = 0; i < _items.Count; i++)
            {
                bool on = i < count;
                _items[i].button.gameObject.SetActive(on);
                if (on && _items[i].label != null) _items[i].label.text = texts[i];
            }
            if (emptyLabel != null) emptyLabel.SetActive(count == 0);
        }

        // 창 폭 = designWidth(보드 px) x 보드 배율, 창 안은 보드 배율로 그린다
        private void Layout()
        {
            _scale = board != null ? board.localScale.x : 1f;
            if (_scale <= 0f) _scale = 1f;
            _sheetHeight = sheet.rect.height;
            sheet.sizeDelta = new Vector2(designWidth * _scale, 0f);
            content.localScale = new Vector3(_scale, _scale, 1f);
            content.sizeDelta = new Vector2(designWidth, _sheetHeight / _scale);
            ApplyT();
        }

        private void SetShown(bool shown)
        {
            if (overlay != null) overlay.gameObject.SetActive(shown);
            if (sheet != null) sheet.gameObject.SetActive(shown);
        }

        private void StartSlide(bool open)
        {
            CancelSlide();
            _slideCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            SlideAsync(open, _slideCts.Token).Forget();
        }

        private void CancelSlide()
        {
            _slideCts?.Cancel();
            _slideCts?.Dispose();
            _slideCts = null;
        }

        private async UniTaskVoid SlideAsync(bool open, CancellationToken ct)
        {
            float target = open ? 1f : 0f;
            float speed = slideSeconds > 0f ? 1f / slideSeconds : float.MaxValue;
            while (!Mathf.Approximately(_t, target))
            {
                _t = Mathf.MoveTowards(_t, target, speed * Time.unscaledDeltaTime);
                ApplyT();
                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow()) return;
            }
            if (open)
            {
                if (input != null && input.InputField != null) input.InputField.ActivateInputField();   // 바로 입력(키보드)
            }
            else
            {
                SetShown(false);
            }
        }

        // 부드럽게 멈추는 미끄러짐(ease out) + 뒤 칸 어둡기
        private void ApplyT()
        {
            float e = 1f - (1f - _t) * (1f - _t) * (1f - _t);
            if (sheet != null) sheet.anchoredPosition = new Vector2((1f - e) * sheet.sizeDelta.x, 0f);
            if (overlay != null)
            {
                var c = overlay.color;
                c.a = overlayAlpha * e;
                overlay.color = c;
            }
        }
    }
}

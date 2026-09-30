using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 팝업 열기/닫기 연출. 뒤 어두운 막은 페이드, 가운데 판은 살짝 작은 크기에서 커지며 나타난다.
    // 뷰가 SetActive(true/false) 대신 UIPopupMotion.Open/Close를 부르면 처음 쓸 때 이 컴포넌트를 런타임에 붙인다(씬 수정 없음).
    // 판은 루트 바로 아래 Panel/Card/Center 이름으로 찾는다. 못 찾으면 페이드만 한다(전체 화면 단계 전환 등).
    // 주의: MonoBehaviour는 클래스명과 같은 파일이어야 안드로이드 빌드에서 정상 직렬화된다.
    [DisallowMultipleComponent]
    public sealed class UIPopupMotion : MonoBehaviour
    {
        const float OpenSeconds = 0.24f;
        const float CloseSeconds = 0.14f;
        // 판 없이 화면 전체가 바뀌는 경우(프로필 등록 단계 등)는 조금 느리게 스며 바뀐다
        const float ScreenOpenSeconds = 0.3f;
        const float ScreenCloseSeconds = 0.24f;
        const float OpenFromScale = 0.9f;
        const float CloseToScale = 0.96f;
        static readonly string[] PanelNames = { "Panel", "Card", "Center" };
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private CanvasGroup _group;
        private RectTransform _panel;
        private Vector3 _panelScale = Vector3.one;
        private Vector2 _panelHome;
        private bool _panelAroundCenter;   // 피벗이 모서리인 판도 가운데에서 커지게 위치를 보정한다
        private CancellationTokenSource _cts;
        private bool _closing;

        // panel을 주면 그 판을 키운다(루트 자신도 가능: 작은 알림 배지 등). 비우면 이름으로 찾는다.
        public static void Open(GameObject root, RectTransform panel = null)
        {
            if (root == null) return;
            Get(root, panel).PlayOpen();
        }

        public static void Close(GameObject root)
        {
            if (root == null || !root.activeSelf) return;
            Get(root, null).PlayClose();
        }

        private static UIPopupMotion Get(GameObject root, RectTransform panel)
        {
            if (!root.TryGetComponent<UIPopupMotion>(out var m))
            {
                m = root.AddComponent<UIPopupMotion>();
                m.Init(panel);
            }
            return m;
        }

        // 꺼진 오브젝트에 붙이면 Awake가 켜질 때까지 안 불리므로 직접 초기화한다.
        private void Init(RectTransform panel)
        {
            _group = UIFx.Group(this);
            _panel = panel != null ? panel : FindPanel();
            if (_panel == null) return;
            _panelScale = _panel.localScale;
            _panelHome = _panel.anchoredPosition;
            _panelAroundCenter = _panel.pivot != Center
                                 && !(_panel.parent != null && _panel.parent.TryGetComponent<LayoutGroup>(out _));
        }

        private RectTransform FindPanel()
        {
            foreach (var n in PanelNames)
                if (transform.Find(n) is RectTransform rt) return rt;
            return null;
        }

        private void PlayOpen()
        {
            var ct = UIFx.Restart(ref _cts, this);
            _closing = false;
            if (!gameObject.activeSelf)
            {
                _group.alpha = 0f;
                SetPanelScale(OpenFromScale);
                gameObject.SetActive(true);
            }
            _group.interactable = true;
            _group.blocksRaycasts = true;

            float a0 = _group.alpha;
            float s0 = CurrentScale();
            UIFx.Tween(this, 0f, _panel != null ? OpenSeconds : ScreenOpenSeconds, k =>
            {
                _group.alpha = Mathf.Lerp(a0, 1f, Mathf.Clamp01(k * 1.5f));
                SetPanelScale(Mathf.LerpUnclamped(s0, 1f, UIFx.EaseOutBack(k)));
            }, ct).Forget();
        }

        private void PlayClose()
        {
            if (_closing) return;
            var ct = UIFx.Restart(ref _cts, this);
            _closing = true;
            _group.interactable = false;   // 닫히는 동안 안의 버튼을 다시 누르지 못하게(막은 계속 터치를 받아 아래로 새지 않음)
            CloseAsync(ct).Forget();
        }

        private async UniTaskVoid CloseAsync(CancellationToken ct)
        {
            float a0 = _group.alpha;
            float s0 = CurrentScale();
            bool canceled = await UIFx.Tween(this, 0f, _panel != null ? CloseSeconds : ScreenCloseSeconds, k =>
            {
                _group.alpha = Mathf.Lerp(a0, 0f, k);
                SetPanelScale(Mathf.Lerp(s0, CloseToScale, UIFx.EaseOutCubic(k)));
            }, ct).SuppressCancellationThrow();
            if (canceled || this == null) return;
            gameObject.SetActive(false);   // OnDisable에서 모양을 되돌린다
        }

        // 판 크기 배율(1 = 원래 크기)
        private void SetPanelScale(float k)
        {
            if (_panel == null) return;
            if (_panelAroundCenter) UIFx.SetScaleAround(_panel, _panelHome, Center, k);   // 모서리 피벗 판(크기 1)
            else _panel.localScale = _panelScale * k;
        }

        private float CurrentScale()
        {
            if (_panel == null) return 1f;
            if (_panelAroundCenter) return _panel.localScale.x;
            return _panelScale.x != 0f ? _panel.localScale.x / _panelScale.x : 1f;
        }

        // 누가 SetActive(false)로 바로 꺼도(부모가 꺼져도) 다음에 켤 때 온전한 모습이 되게 되돌린다.
        private void OnDisable()
        {
            UIFx.Cancel(ref _cts);
            _closing = false;
            if (_group == null) return;
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            SetPanelScale(1f);
        }
    }
}

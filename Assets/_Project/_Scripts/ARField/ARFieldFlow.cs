using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Artti.AAC;
using Artti.Common.Speech;
using Artti.UI;

namespace Artti.ARField
{
    public class ARFieldFlow : MonoBehaviour
    {
        [SerializeField] private AACDatabase database;
        [SerializeField] private ARFieldUIController uiController;
        [SerializeField] private ARFieldResultConfirmView resultConfirmView;

        private const int MaxSlots = 4;
        private string _currentScenario;
        private string _pendingScenario;       // OCR로 추론된 시나리오, '맞아요' 확정 대기
        private Texture2D _capturedTex;
        private ITtsService _tts;
        private readonly KeywordDictionary _dict = new KeywordDictionary();
        private bool _dictLoading;
        private bool _captureBusy;
        private ARSession _arSession;

        public void SetTtsService(ITtsService tts) => _tts = tts;

        // ARCore가 카메라를 점유하고 있어 인앱 카메라(WebCamTexture)가 같은 카메라를 열 수 없다.
        // 촬영 직전 AR 세션을 꺼서 카메라를 반납하고, 복귀 후 다시 켜 라이브 AR을 복구한다(카메라 양도).
        private void SetArActive(bool active)
        {
            if (_arSession == null)
                _arSession = FindFirstObjectByType<ARSession>();
            if (_arSession != null)
                _arSession.enabled = active;
        }

        // "캡처" 버튼 → 인앱 카메라 → ML Kit OCR → 키워드 사전 매칭 → ResultConfirmPanel 갱신
        public void Capture()
        {
            if (_captureBusy) return;

            if (!_dict.IsLoaded)
            {
                Debug.LogWarning("[ARFieldFlow] 키워드 사전 로딩 중 — 잠시 후 다시 시도");
                return;
            }

            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.LogWarning("[ARFieldFlow] OCR은 Android에서만 동작 (현재: " + Application.platform + ")");
                return;
            }

            _captureBusy = true;
            try
            {
                // 외부 카메라 앱(NativeCamera) 대신 인앱 카메라 사용 → 포그라운드 유지로 LMKD 재시작 차단.
                // ARCore와 카메라가 겹치므로 먼저 AR 세션을 꺼 카메라를 반납한 뒤 연다.
                SetArActive(false);
                InAppCamera.Open(OnPhotoTaken);
            }
            catch (System.Exception e)
            {
                _captureBusy = false;
                SetArActive(true);
                Debug.LogError("[ARFieldFlow] 인앱 카메라 실패: " + e);
            }
        }

        // "맞아요" → OCR이 추론한 시나리오로 진행
        public void ConfirmRecognition()
        {
            if (string.IsNullOrEmpty(_pendingScenario))
            {
                // 매칭 실패했거나 미지원 카테고리 — 수동 선택으로 폴백
                uiController.ShowManualSelect();
                return;
            }
            HandleScenarioSelected(_pendingScenario);
        }

        private void OnPhotoTaken(string path)
        {
            // 카메라에서 복귀 — AR 세션 재개 (취소/성공 무관하게 라이브 카메라 복구)
            SetArActive(true);

            if (string.IsNullOrEmpty(path))
            {
                _captureBusy = false;
                Debug.Log("[ARFieldFlow] 촬영 취소");
                return;
            }

            OcrBridge.RecognizeFromPath(
                imagePath: path,
                onResult: (text) => OnOcrSuccess(text, path),
                onError: (err) => OnOcrError(err)
            );
        }

        private void OnOcrSuccess(string text, string imagePath)
        {
            _captureBusy = false;

            var tex = LoadTextureFromFile(imagePath);
            if (string.IsNullOrWhiteSpace(text))
            {
                BindResult(tex, null, "인식 실패", "글자를 인식하지 못했어요. 더 선명하게 다시 시도해주세요.", scenario: null);
                return;
            }

            var match = _dict.Match(text);
            if (match == null)
            {
                BindResult(tex, null, "분류 불가", $"OCR 결과: {text}", scenario: null);
                return;
            }

            Sprite icon = !string.IsNullOrEmpty(match.ImageName) ? Resources.Load<Sprite>(match.ImageName) : null;
            BindResult(tex, icon, match.Category, match.Description, scenario: MapCategoryToScenario(match.Category));
        }

        private void OnOcrError(string err)
        {
            _captureBusy = false;
            Debug.LogError("[ARFieldFlow] OCR 실패: " + err);
            BindResult(null, null, "오류", err, scenario: null);
        }

        private void BindResult(Texture2D tex, Sprite icon, string category, string description, string scenario)
        {
            // 이전 캡처 텍스처 정리
            if (_capturedTex != null && _capturedTex != tex)
                Destroy(_capturedTex);
            _capturedTex = tex;

            _pendingScenario = scenario;

            if (resultConfirmView != null)
                resultConfirmView.Bind(tex, icon, category, description);
            if (uiController != null)
                uiController.ShowResultConfirm();
        }

        private static string MapCategoryToScenario(string category) => category switch
        {
            "편의점" => ScenarioIds.Convenience,
            "약국"   => ScenarioIds.Pharmacy,
            "음식점" => ScenarioIds.Restaurant,
            _        => null  // 카페 등 — 시나리오 미지원
        };

        private static Texture2D LoadTextureFromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                // 표시 전용 — 읽기 사본을 버려 텍스처 메모리 절반 절감 (OCR은 파일 경로로 별도 처리)
                if (tex.LoadImage(bytes, markNonReadable: true)) return tex;
                Destroy(tex);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ARFieldFlow] 이미지 로드 실패: " + e.Message);
            }
            return null;
        }

        private void Awake()
        {
            // KeywordDictionary 로드는 코루틴 — 메인 스레드 디스패처 살아있어야 callback이 안전
            var _ = UnityMainThreadDispatcher.Instance;
        }

        private void Start()
        {
            if (!_dictLoading && !_dict.IsLoaded)
                StartCoroutine(LoadDictRoutine());
        }

        private IEnumerator LoadDictRoutine()
        {
            _dictLoading = true;
            var en = _dict.LoadAsync();
            while (en.MoveNext()) yield return en.Current;
            _dictLoading = false;
        }

        private void OnEnable()
        {
            if (uiController == null) return;
            uiController.OnManualPlaceSelected += HandleScenarioSelected;
            uiController.OnCategorySelected    += HandleCategorySelected;
            uiController.OnSubCategorySelected += HandleSubCategorySelected;
        }

        private void OnDisable()
        {
            if (uiController == null) return;
            uiController.OnManualPlaceSelected -= HandleScenarioSelected;
            uiController.OnCategorySelected    -= HandleCategorySelected;
            uiController.OnSubCategorySelected -= HandleSubCategorySelected;
        }

        private void OnDestroy()
        {
            if (_capturedTex != null) Destroy(_capturedTex);
        }

        private void HandleScenarioSelected(string scenarioId)
        {
            if (database == null) { Debug.LogWarning("[ARFieldFlow] AACDatabase 미연결"); return; }
            _currentScenario = scenarioId;
            var cards = database.CardsForScenario(scenarioId).Take(MaxSlots).ToList();
            if (cards.Count == 0)
                Debug.LogWarning($"[ARFieldFlow] {scenarioId} 시나리오에 해당하는 카드 없음 — AACDatabase 점검 필요");
            uiController.ShowCategory(cards);
        }

        private void HandleCategorySelected(AACCard card)
        {
            if (database == null || card == null) return;

            var subCards = string.IsNullOrEmpty(card.branchId)
                ? database.CardsForScenario(_currentScenario).Where(c => c.id != card.id).Take(MaxSlots).ToList()
                : database.CardsInBranch(card.branchId).Take(MaxSlots).ToList();

            uiController.ShowSubCategory(subCards);
        }

        private void HandleSubCategorySelected(AACCard card)
        {
            if (card?.phrase != null && _tts != null)
            {
                var text = !string.IsNullOrEmpty(card.phrase.ttsText) ? card.phrase.ttsText : card.phrase.text;
                _tts.SpeakAsync(text, default).Forget();
            }
            uiController.ShowCamera();
        }
    }
}

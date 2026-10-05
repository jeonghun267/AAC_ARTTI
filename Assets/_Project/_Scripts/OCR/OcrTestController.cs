using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Artti.ARField;

/// <summary>
/// OCR 테스트 화면 컨트롤러
/// Plan A: 카카오 좌표 기반 검색
/// Plan B: 로컬 사전 매칭 (fallback)
/// </summary>
public class OcrTestController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject resultPanel;

    [Header("HomePanel - 입력 버튼")]
    [SerializeField] private Button pickImageButton;
    [SerializeField] private Button takePhotoButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("ResultPanel - 헤더")]
    [SerializeField] private Button backButton;

    [Header("ResultPanel - 사진 영역")]
    [SerializeField] private RawImage capturedImage;

    [Header("ResultPanel - 결과 카드")]
    [SerializeField] private Image aacImageComponent;
    [SerializeField] private Image categoryBadgeImage;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("카카오 API 검색 컴포넌트")]
    [SerializeField] private KakaoLocalSearcher kakaoSearcher;

    [Header("ResultPanel - 위치 지도")]
    [SerializeField] private NaverStaticMapController naverStaticMapController;

    [Header("HelpDetailPanel - 최신 메뉴")]
    [SerializeField] private LatestMenuController latestMenuController;

    // 카테고리별 배지 색상 (한글 키)
    private readonly System.Collections.Generic.Dictionary<string, Color> _categoryColors =
        new System.Collections.Generic.Dictionary<string, Color>
    {
        { "편의점", new Color(0.0f,    0.537f, 0.482f) },
        { "약국",   new Color(0.0f,    0.412f, 0.388f) },
        { "카페",   new Color(0.553f,  0.431f, 0.388f) },
        { "음식점", new Color(0.902f,  0.318f, 0.0f)   },
    };

    private readonly KeywordDictionary _dict = new KeywordDictionary();
    private bool _isProcessing = false;
    private Texture2D _currentTexture;
    private Coroutine _resultMapRoutine;

    private void Awake()
    {
        var _ = UnityMainThreadDispatcher.Instance;

        if (pickImageButton != null) pickImageButton.onClick.AddListener(OnPickImageClicked);
        if (takePhotoButton != null) takePhotoButton.onClick.AddListener(OnTakePhotoClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

        if (kakaoSearcher == null)
            kakaoSearcher = GetComponent<KakaoLocalSearcher>();

        ShowHomePanel();

        if (statusText != null)
            statusText.text = "사전을 불러오는 중...";
    }

    private void Start()
    {
        StartCoroutine(LoadDictionaryRoutine());
        // GPS 상태를 주기적으로 화면에 표시
        StartCoroutine(UpdateGpsStatusRoutine());
    }

    private void OnDestroy()
    {
        DisposeCurrentTexture();
    }

    private IEnumerator LoadDictionaryRoutine()
    {
        var enumerator = _dict.LoadAsync();
        while (enumerator.MoveNext())
        {
            yield return enumerator.Current;
        }

        if (statusText != null)
        {
            statusText.text = _dict.IsLoaded
                ? "버튼을 눌러 간판 사진을 선택하세요."
                : "[ERROR] 사전 로드에 실패했습니다.";
        }
    }

    /// <summary>
    /// 홈 화면에 있을 때 GPS 상태를 1초마다 업데이트해서 표시
    /// </summary>
    private IEnumerator UpdateGpsStatusRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            // 홈 패널이 활성화되어 있고 처리 중이 아닐 때만 GPS 상태 표시
            if (homePanel != null && homePanel.activeSelf && !_isProcessing && _dict.IsLoaded)
            {
                if (statusText != null)
                {
                    if (GPSManager.IsReady)
                    {
                        statusText.text = $"✅ GPS 준비완료\n📍 위도: {GPSManager.Latitude:F6}\n경도: {GPSManager.Longitude:F6}\n\n버튼을 눌러 간판 사진을 선택하세요.";
                    }
                    else
                    {
                        statusText.text = $"⚠️ {GPSManager.StatusMessage}\n\n버튼을 눌러 간판 사진을 선택하세요.";
                    }
                }
            }
        }
    }

    // ============================================================
    // 버튼 이벤트
    // ============================================================

    private void OnPickImageClicked()
    {
        if (!CanProcess()) return;

        GPSManager.UpdateCoords();
        SetStatus($"📍 위도: {GPSManager.Latitude:F6}\n경도: {GPSManager.Longitude:F6}\n갤러리를 여는 중...");

        try
        {
            NativeGallery.GetImageFromGallery(
                (path) => HandleImageSelected(path, "갤러리"),
                title: "간판 사진 선택",
                mime: "image/*"
            );
        }
        catch (System.Exception e)
        {
            SetStatus("[ERROR] 갤러리: " + e.Message);
            Debug.LogError("[OcrTest] Gallery error: " + e);
        }
    }

    private void OnTakePhotoClicked()
    {
        if (!CanProcess()) return;

        GPSManager.UpdateCoords();
        SetStatus($"📍 위도: {GPSManager.Latitude:F6}\n경도: {GPSManager.Longitude:F6}\n카메라를 여는 중...");

        try
        {
            // 시스템 카메라 앱(NativeCamera) 대신 앱 내부 카메라(WebCamTexture) 사용.
            // 외부 카메라 인텐트는 우리 프로세스를 백그라운드(adj=700)로 내리고, 그 순간 카메라가
            // 가용 메모리를 ~160MB까지 끌어내려 저메모리 기기(SM-T733 3GB 실측)에서 LMKD가
            // 강제 종료 → 앱이 스플래시부터 재시작. 인앱 카메라는 포그라운드를 유지해 이를 차단.
            InAppCamera.Open((path) => HandleImageSelected(path, "카메라"));
        }
        catch (System.Exception e)
        {
            SetStatus("[ERROR] 카메라: " + e.Message);
            Debug.LogError("[OcrTest] Camera error: " + e);
        }
    }

    private void OnBackClicked()
    {
        ShowHomePanel();
    }

    // ============================================================
    // 이미지 선택 후 처리
    // ============================================================

    private void HandleImageSelected(string path, string source)
    {
        if (string.IsNullOrEmpty(path))
        {
            SetStatus("취소되었습니다 (" + source + ").");
            return;
        }

        Debug.Log("[OcrTest] Image from " + source + ": " + path);
        SetStatus("OCR 인식 중...");
        _isProcessing = true;

        OcrBridge.RecognizeFromPath(
            path,
            onResult: (text) => OnOcrSuccess(text, path),
            onError: OnOcrError
        );
    }

    private void OnOcrSuccess(string text, string imagePath)
    {
        _isProcessing = false;
        Debug.Log("[OcrTest] OCR result: \n" + text);

        StartCoroutine(ProcessClassificationRoutine(text, imagePath));
    }

    /// <summary>
    /// Plan A: 카카오 좌표 검색 → Plan B: 로컬 사전 매칭
    /// </summary>
    private IEnumerator ProcessClassificationRoutine(string ocrText, string imagePath)
    {
        SetStatus("위치 기반 검색 중...");

        KeywordMatch finalMatch = null;
        KeywordMatch ocrMatch = _dict.Match(ocrText);
        string recognizedBrand = KakaoLocalSearcher.GetRecognizedCoffeeBrand(ocrText, ocrMatch?.MatchedKeyword);
        if (recognizedBrand != null)
        {
            // Kakao 실패 시에도 일반 COFFEE 키워드 대신 인식된 브랜드를 유지합니다.
            // 내부 카테고리는 기존 주문/AAC 흐름에서 사용하는 카페 그대로입니다.
            ocrMatch = new KeywordMatch("카페", "커피와 음료를 파는 곳입니다.", "AAC/cafe", recognizedBrand);
        }

        // 변경점: GPS가 준비되기 전에 사진 인식이 끝나면 좌표 0,0으로 검색해 매장을 못 찾았다.
        //   최대 8초 기다렸다가 최신 좌표로 검색하고, 그래도 좌표가 없으면 카카오 위치 검색은 건너뛴다.
        if (!HasValidGps())
        {
            SetStatus("위치 확인 중...");
            yield return WaitForGps(8f);
        }
        bool hasGps = HasValidGps();
        if (kakaoSearcher != null) kakaoSearcher.ClearSelection();
        if (hasGps) GPSManager.UpdateCoords();
        else Debug.LogWarning("[OcrTest] GPS 좌표가 없어 카카오 위치 검색을 건너뜁니다.");

        // ============================================================
        // Plan A: 카카오 좌표 기반 검색
        // ============================================================
        if (kakaoSearcher != null && hasGps)
        {
            bool kakaoFinished = false;

            yield return StartCoroutine(kakaoSearcher.SearchByCoords(
                GPSManager.Latitude,
                GPSManager.Longitude,
                onSuccess: (match) =>
                {
                    finalMatch = match;
                    kakaoFinished = true;
                    Debug.Log($"[OcrTest] Plan A 성공! [{match.Category}] 좌표 검색 매칭.");
                },
                onFail: () =>
                {
                    kakaoFinished = true;
                    Debug.Log("[OcrTest] Plan A 실패. 로컬 사전으로 넘어갑니다.");
                },
                recognizedBrand: recognizedBrand
            ));

            yield return new WaitUntil(() => kakaoFinished);
        }

        // ============================================================
        // Plan B: 로컬 사전 매칭 (fallback)
        // ============================================================
        if (finalMatch == null)
        {
            Debug.Log("[OcrTest] Plan B: 로컬 사전 검색.");
            SetStatus("텍스트 기반 검색 중...");
            finalMatch = ocrMatch;

            if (finalMatch != null)
                Debug.Log($"[OcrTest] Plan B 성공! [{finalMatch.Category}] 로컬 사전 매칭.");
        }

        // ============================================================
        // 최종 결과 표시
        // ============================================================
        if (finalMatch != null)
        {
            ShowResultPanel(finalMatch, imagePath);
        }
        else
        {
            // 매칭 실패해도 결과 화면으로 전환 (촬영 사진 + 인식 상태 표시).
            // 이렇게 해야 "촬영 후 다음 화면으로 넘어가는" 흐름이 끊기지 않음.
            ShowResultFallback(imagePath, ocrText);
        }
    }

    private void OnOcrError(string message)
    {
        _isProcessing = false;
        Debug.LogError("[OcrTest] OCR error: " + message);
        SetStatus("[ERROR] OCR: " + message);
    }

    // ============================================================
    // 화면 전환 + 결과 표시
    // ============================================================

    private void ShowHomePanel()
    {
        if (_resultMapRoutine != null)
        {
            StopCoroutine(_resultMapRoutine);
            _resultMapRoutine = null;
        }
        if (homePanel != null)
        {
            // 변경점: 홈이 사라지는 중에 돌아오면 사라지기를 멈추고 원래 모습으로
            var fade = homePanel.GetComponent<ARPanelFadeOut>();
            if (fade != null) fade.Cancel();
            homePanel.SetActive(true);
        }
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    // 변경점(사용자 결정 2026-10-06): 홈 -> 결과는 결과를 먼저 켜고, 홈을 맨 앞에서 서서히 사라지게 한다(페이드아웃).
    //   사라지는 동안 홈은 누를 수 없다. 결과 화면 자체의 등장 효과는 없다(ARPanelEnterFx가 결과에는 붙지 않음)
    private const float HomeFadeOutSeconds = 0.8f;   // 실기기에서 0.35초는 너무 빨라 안 보였다(2026-10-06)

    private void SwitchHomeToResult()
    {
        if (resultPanel != null) resultPanel.SetActive(true);
        if (homePanel == null || !homePanel.activeSelf) return;
        var fade = homePanel.GetComponent<ARPanelFadeOut>();
        if (fade == null) fade = homePanel.AddComponent<ARPanelFadeOut>();
        fade.FadeOutAndHide(HomeFadeOutSeconds);
    }

    private void ShowResultPanel(KeywordMatch match, string imagePath)
    {
        // Kakao 경로는 최종 place_name, OCR 경로는 최종 매칭 키워드를 사용한다.
        string finalPlaceName = match.MatchedKeyword ?? string.Empty;
        Debug.Log($"[LatestMenu DEBUG] finalPlaceName = '{finalPlaceName}'");
        Debug.Log($"[LatestMenu DEBUG] Category = '{match.Category}'");
        Debug.Log($"[LatestMenu DEBUG] MatchedKeyword = '{match.MatchedKeyword}'");

        // 판별된 장소를 카드 생성과 최신 메뉴 표시로 전달 (좌표/OCR 경로가 모두 여기로 모임)
        PlaceContext.Set(match.Category, finalPlaceName);
        if (latestMenuController == null)
        {
            Debug.LogError("[LatestMenu DEBUG] latestMenuController 참조가 null입니다.");
        }
        else
        {
            Debug.Log($"[LatestMenu DEBUG] UpdateForPlace 호출: '{finalPlaceName}'");
            latestMenuController.UpdateForPlace(finalPlaceName);
        }

        LoadCapturedImage(imagePath);

        if (categoryText != null)
            categoryText.text = LatestMenuController.GetSupportedBrandName(finalPlaceName) ?? match.Category;

        if (categoryBadgeImage != null && _categoryColors.TryGetValue(match.Category, out Color color))
            categoryBadgeImage.color = color;

        if (descriptionText != null)
            descriptionText.text = match.Description;

        if (aacImageComponent != null && !string.IsNullOrEmpty(match.ImageName))
        {
            Sprite sprite = Resources.Load<Sprite>(match.ImageName);
            if (sprite != null)
            {
                aacImageComponent.sprite = sprite;
                aacImageComponent.color = Color.white;
            }
            else
            {
                Debug.LogWarning("[OcrTest] AAC image not found: " + match.ImageName);
                aacImageComponent.sprite = null;
                aacImageComponent.color = new Color(0.9f, 0.9f, 0.9f);
            }
        }

        SwitchHomeToResult();

        KakaoDocument selectedPlace = kakaoSearcher != null ? kakaoSearcher.SelectedPlace : null;
        Debug.Log($"[ResultMap] GPS IsReady = {GPSManager.IsReady}");
        Debug.Log($"[ResultMap] User Latitude = {GPSManager.Latitude}");
        Debug.Log($"[ResultMap] User Longitude = {GPSManager.Longitude}");
        Debug.Log($"[ResultMap] SelectedPlace = {selectedPlace?.place_name ?? "(없음)"}");
        Debug.Log($"[ResultMap] Store Longitude(x) = {selectedPlace?.x ?? "(없음)"}");
        Debug.Log($"[ResultMap] Store Latitude(y) = {selectedPlace?.y ?? "(없음)"}");

        if (_resultMapRoutine != null)
        {
            StopCoroutine(_resultMapRoutine);
            _resultMapRoutine = null;
        }
        if (naverStaticMapController != null)
            _resultMapRoutine = StartCoroutine(UpdateResultMapWhenReady(selectedPlace));
        else
            Debug.LogWarning("[ResultMap] NaverStaticMapController 참조가 없습니다.");
    }

    private static bool HasValidGps() =>
        GPSManager.IsReady && NaverStaticMapController.IsCoordinateValid(GPSManager.Latitude, GPSManager.Longitude);

    private static IEnumerator WaitForGps(float seconds)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < deadline && !HasValidGps())
            yield return new WaitForSecondsRealtime(0.25f);
    }

    private IEnumerator UpdateResultMapWhenReady(KakaoDocument selectedPlace)
    {
        if (!GPSManager.IsReady ||
            !NaverStaticMapController.IsCoordinateValid(GPSManager.Latitude, GPSManager.Longitude))
        {
            naverStaticMapController.ShowLocationStatus("위치 확인 중");
            float deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline &&
                   (!GPSManager.IsReady ||
                    !NaverStaticMapController.IsCoordinateValid(GPSManager.Latitude, GPSManager.Longitude)))
                yield return new WaitForSecondsRealtime(0.25f);
        }

        if (!GPSManager.IsReady ||
            !NaverStaticMapController.IsCoordinateValid(GPSManager.Latitude, GPSManager.Longitude))
        {
            naverStaticMapController.ShowLocationStatus("위치 확인 실패");
            Debug.LogWarning("[ResultMap] GPS 준비를 8초 기다렸지만 유효한 좌표가 없습니다.");
            yield break;
        }

        GPSManager.UpdateCoords();
        double userLatitude = GPSManager.Latitude;
        double userLongitude = GPSManager.Longitude;
        if (!NaverStaticMapController.IsCoordinateValid(userLatitude, userLongitude))
        {
            naverStaticMapController.ShowLocationStatus("위치 확인 실패");
            Debug.LogWarning("[ResultMap] 갱신된 GPS 좌표가 유효하지 않습니다.");
            yield break;
        }

        naverStaticMapController.ShowPlaceholder();
        naverStaticMapController.ShowUserCoordinates(userLatitude, userLongitude);

        if (selectedPlace == null ||
            !double.TryParse(selectedPlace.x, NumberStyles.Float, CultureInfo.InvariantCulture, out double storeLongitude) ||
            !double.TryParse(selectedPlace.y, NumberStyles.Float, CultureInfo.InvariantCulture, out double storeLatitude) ||
            !NaverStaticMapController.IsCoordinateValid(storeLatitude, storeLongitude))
        {
            // 변경점: 매장 좌표가 없어도 내 위치만이라도 지도에 표시한다
            Debug.LogWarning("[ResultMap] Kakao에서 선택된 매장 좌표가 없어 내 위치만 지도에 표시합니다.");
            naverStaticMapController.UpdateUserOnlyMap(userLatitude, userLongitude);
            yield break;
        }

        Debug.Log($"[NaverMap] 사용자 위치: {userLatitude:F6}, {userLongitude:F6}");
        Debug.Log($"[NaverMap] 매장 위치: {storeLatitude:F6}, {storeLongitude:F6}");
        Debug.Log($"[NaverMap] 매장명: {selectedPlace.place_name}");
        naverStaticMapController.UpdateMap(userLatitude, userLongitude,
                                            storeLatitude, storeLongitude, selectedPlace.place_name);
    }

    // 카테고리 매칭에 실패했을 때도 촬영 사진과 인식 결과를 보여주며 결과 화면으로 전환
    private void ShowResultFallback(string imagePath, string ocrText)
    {
        PlaceContext.Clear();
        if (latestMenuController != null)
            latestMenuController.UpdateForPlace(string.Empty);

        LoadCapturedImage(imagePath);

        bool noText = string.IsNullOrWhiteSpace(ocrText);

        if (categoryText != null)
            categoryText.text = noText ? "인식 실패" : "분류 불가";

        if (categoryBadgeImage != null)
            categoryBadgeImage.color = new Color(0.5f, 0.5f, 0.5f);

        if (descriptionText != null)
            descriptionText.text = noText
                ? "글자를 인식하지 못했어요.\n간판이 잘 보이게 더 가까이서 다시 찍어주세요."
                : ("인식된 글자:\n" + ocrText);

        if (aacImageComponent != null)
        {
            aacImageComponent.sprite = null;
            aacImageComponent.color = new Color(0.9f, 0.9f, 0.9f);
        }

        SwitchHomeToResult();
        if (_resultMapRoutine != null)
        {
            StopCoroutine(_resultMapRoutine);
            _resultMapRoutine = null;
        }
        if (naverStaticMapController != null) naverStaticMapController.ShowPlaceholder();
    }

    private void LoadCapturedImage(string imagePath)
    {
        if (capturedImage == null) return;

        capturedImage.texture = null;
        capturedImage.color = Color.clear;

        try
        {
            if (!File.Exists(imagePath))
            {
                Debug.LogWarning("[OcrTest] Image file not found: " + imagePath);
                return;
            }

            byte[] imageData = File.ReadAllBytes(imagePath);
            DisposeCurrentTexture();

            _currentTexture = new Texture2D(2, 2);
            // 표시 전용 — 읽기 사본을 버려 텍스처 메모리를 절반으로 줄인다 (OCR은 파일 경로로 별도 처리)
            if (_currentTexture.LoadImage(imageData, markNonReadable: true))
            {
                capturedImage.texture = _currentTexture;
                capturedImage.color = Color.white;
            }
            else
            {
                Debug.LogWarning("[OcrTest] Failed to load image into texture");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[OcrTest] LoadCapturedImage error: " + e);
        }
    }

    private void DisposeCurrentTexture()
    {
        if (_currentTexture != null)
        {
            Destroy(_currentTexture);
            _currentTexture = null;
        }
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;

        Debug.Log("[OcrTest] Status: " + text);
    }

    private bool CanProcess()
    {
        if (_isProcessing)
        {
            Debug.Log("[OcrTest] Already processing, ignoring tap.");
            return false;
        }
        if (!_dict.IsLoaded)
        {
            SetStatus("사전이 아직 로드되지 않았습니다.");
            return false;
        }
        return true;
    }
}

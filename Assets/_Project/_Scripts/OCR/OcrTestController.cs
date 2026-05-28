using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// OCR 테스트 화면 컨트롤러 (네이버 API 다중 검색 로직 적용 완료)
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

    [Header("네이버 API 검색 컴포넌트")]
    [SerializeField] private NaverLocalSearcher naverSearcher;

    // 카테고리별 배지 색상 (한글 키)
    private readonly System.Collections.Generic.Dictionary<string, Color> _categoryColors =
        new System.Collections.Generic.Dictionary<string, Color>
    {
        { "편의점", new Color(0.0f,    0.537f, 0.482f) },  // #00897B
        { "약국",   new Color(0.0f,    0.412f, 0.388f) },  // #00695C
        { "카페",   new Color(0.553f,  0.431f, 0.388f) },  // #8D6E63
        { "음식점", new Color(0.902f,  0.318f, 0.0f)   },  // #E65100
    };

    private readonly KeywordDictionary _dict = new KeywordDictionary();
    private bool _isProcessing = false;
    private Texture2D _currentTexture;

    private void Awake()
    {
        var _ = UnityMainThreadDispatcher.Instance;

        if (pickImageButton != null) pickImageButton.onClick.AddListener(OnPickImageClicked);
        if (takePhotoButton != null) takePhotoButton.onClick.AddListener(OnTakePhotoClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

        // 컴포넌트 자동 할당
        if (naverSearcher == null)
        {
            naverSearcher = GetComponent<NaverLocalSearcher>();
        }

        ShowHomePanel();

        if (statusText != null)
        {
            statusText.text = "사전을 불러오는 중...";
        }
    }

    private void Start()
    {
        StartCoroutine(LoadDictionaryRoutine());
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

    // ============================================================
    // 버튼 이벤트
    // ============================================================

    private void OnPickImageClicked()
    {
        if (!CanProcess()) return;

        SetStatus("갤러리를 여는 중...");

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

        SetStatus("카메라를 여는 중...");

        try
        {
            NativeCamera.TakePicture(
                (path) => HandleImageSelected(path, "카메라"),
                maxSize: 2048
            );
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

        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("글자를 인식하지 못했습니다. 더 선명한 사진을 시도해 주세요.");
            return;
        }

        StartCoroutine(ProcessClassificationRoutine(text, imagePath));
    }

    /// <summary>
    /// 여러 줄의 텍스트를 순서대로 네이버에 검색하여 유효한 상호명을 찾아내는 핵심 로직
    /// </summary>
    private IEnumerator ProcessClassificationRoutine(string ocrText, string imagePath)
    {
        SetStatus("장소 카테고리 분석 중...");

        // 1. 줄바꿈을 기준으로 텍스트를 배열로 쪼갭니다.
        string[] lines = ocrText.Split(new char[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        KeywordMatch finalMatch = null;
        string successfulKeyword = "";

        // 2. 네이버 API를 줄 단위로 순회하며 다중 검색 진행
        if (naverSearcher != null)
        {
            foreach (string rawLine in lines)
            {
                string keyword = rawLine.Trim();

                // 1글자짜리 오인식 문자열이나 빈 문자열은 네이버에 검색하지 않고 패스하여 API 호출을 아낍니다.
                if (keyword.Length < 2) continue;

                Debug.Log($"[OcrTest] 네이버 API 검색 시도: {keyword}");

                string naverJsonResult = null;
                bool isNetworkError = false;

                // 통신 대기
                yield return StartCoroutine(naverSearcher.SearchPlaceCoroutine(
                    keyword,
                    onComplete: (json) => naverJsonResult = json,
                    onError: (error) => {
                        isNetworkError = true;
                    }
                ));

                // 응답이 오면 파싱 시도
                if (!isNetworkError && !string.IsNullOrEmpty(naverJsonResult))
                {
                    string rawCategory = naverSearcher.ParseCategoryFromJson(naverJsonResult);
                    string appCategory = naverSearcher.MapToAppCategory(rawCategory);

                    // 네이버가 유효한 카테고리(음식점, 카페 등)를 뱉어주었다면!
                    if (!string.IsNullOrEmpty(appCategory))
                    {
                        finalMatch = _dict.Match(appCategory);
                        if (finalMatch != null)
                        {
                            successfulKeyword = keyword;
                            Debug.Log($"[OcrTest] 빙고! '{keyword}'(으)로 [{appCategory}] 매칭 성공. 나머지 줄 검색을 중단합니다.");
                            break; // 정답을 찾았으므로 남은 줄은 더 이상 검색하지 않고 반복문을 즉시 탈출합니다.
                        }
                    }
                }
            }
        }

        // 3. [Plan B] 모든 줄을 다 검색했는데도 실패했다면 기존의 로컬 사전 매칭 실행
        if (finalMatch == null)
        {
            Debug.Log("[OcrTest] 네이버 다중 검색 실패. 로컬 사전을 전체 텍스트로 검색합니다.");
            finalMatch = _dict.Match(ocrText);
        }

        // 4. 최종 결과 화면 표시
        if (finalMatch != null)
        {
            ShowResultPanel(finalMatch, imagePath);
        }
        else
        {
            SetStatus("분류 가능한 카테고리가 없습니다.\n마지막 인식 테스트: " + (lines.Length > 0 ? lines[0] : "없음"));
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
        if (homePanel != null) homePanel.SetActive(true);
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    private void ShowResultPanel(KeywordMatch match, string imagePath)
    {
        LoadCapturedImage(imagePath);

        if (categoryText != null)
        {
            categoryText.text = match.Category;
        }

        if (categoryBadgeImage != null && _categoryColors.TryGetValue(match.Category, out Color color))
        {
            categoryBadgeImage.color = color;
        }

        if (descriptionText != null)
        {
            descriptionText.text = match.Description;
        }

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

        if (homePanel != null) homePanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(true);
    }

    private void LoadCapturedImage(string imagePath)
    {
        if (capturedImage == null) return;

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
            if (_currentTexture.LoadImage(imageData))
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
        {
            statusText.text = text;
        }
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
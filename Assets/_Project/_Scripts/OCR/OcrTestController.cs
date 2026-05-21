using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// OCR 테스트 화면 컨트롤러 (Panel 전환 + 사진 표시)
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
        Debug.Log("[OcrTest] OCR result: " + text);

        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("글자를 인식하지 못했습니다. 더 선명한 사진을 시도해 주세요.");
            return;
        }

        KeywordMatch match = _dict.Match(text);

        if (match == null)
        {
            SetStatus("분류 가능한 카테고리가 없습니다.\nOCR 결과: " + text);
            return;
        }

        ShowResultPanel(match, imagePath);
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
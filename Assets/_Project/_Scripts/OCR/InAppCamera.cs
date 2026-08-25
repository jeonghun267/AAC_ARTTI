using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// 앱 내부 카메라. 시스템 카메라 앱(android.media.action.IMAGE_CAPTURE 인텐트)을 띄우지 않고
/// WebCamTexture로 인앱 미리보기 + 촬영을 처리한다.
///
/// 외부 카메라 인텐트는 우리 프로세스를 백그라운드(oom adj=700, cached)로 내리고, 그 순간
/// 카메라 앱이 가용 메모리를 ~160MB까지 끌어내려 저메모리 기기에서 LMKD가 우리(~280MB)를
/// 즉시 회수한다(= 앱이 스플래시부터 재시작). 실측(SM-T733, 3GB)으로 확인됨.
/// 인앱 카메라는 우리 앱이 포그라운드(adj=0)를 유지하므로 이 재시작을 근본적으로 제거한다.
///
/// 촬영 결과는 JPG 파일로 저장되어 onResult(path)로 전달된다(취소/실패 시 null).
/// 기존 OCR 파이프라인(파일 경로 → ML Kit)을 그대로 재사용하기 위함.
/// </summary>
public class InAppCamera : MonoBehaviour
{
    private Action<string> _onResult;
    private WebCamTexture _cam;
    private bool _busy;

    /// <summary>인앱 카메라 오버레이를 띄운다. 촬영하면 JPG 경로, 취소하면 null을 콜백.</summary>
    public static void Open(Action<string> onResult)
    {
        var go = new GameObject("InAppCamera");
        var cam = go.AddComponent<InAppCamera>();
        cam._onResult = onResult;
        cam.StartCoroutine(cam.Run());
    }

    private IEnumerator Run()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android에서는 Application.RequestUserAuthorization(WebCam)가 런타임 권한 팝업을
        // 띄우지 못해, HasUserAuthorization은 true여도 WebCamTexture.devices가 비어버린다
        // (= "사용 가능한 카메라 없음"). 반드시 Permission API로 CAMERA 권한을 받아야 한다.
        yield return RequestAndroidCameraPermission();
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.LogWarning("[InAppCamera] 카메라 권한 거부됨");
            Finish(null);
            yield break;
        }

        // 권한 직후엔 디바이스 목록이 한두 프레임 비어 있을 수 있어 잠시 대기
        float dwait = 0f;
        while (WebCamTexture.devices.Length == 0 && dwait < 2f) { dwait += Time.deltaTime; yield return null; }
#else
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.LogWarning("[InAppCamera] 카메라 권한 거부됨");
            Finish(null);
            yield break;
        }
#endif

        // 후면 카메라 우선 선택
        string deviceName = null;
        foreach (var d in WebCamTexture.devices)
        {
            if (!d.isFrontFacing) { deviceName = d.name; break; }
        }
        if (deviceName == null && WebCamTexture.devices.Length > 0)
            deviceName = WebCamTexture.devices[0].name;
        if (string.IsNullOrEmpty(deviceName))
        {
            Debug.LogWarning("[InAppCamera] 사용 가능한 카메라 없음");
            Finish(null);
            yield break;
        }

        _cam = new WebCamTexture(deviceName, 1920, 1080);
        _cam.Play();

        // 첫 프레임 들어올 때까지 대기 (초기엔 width가 16 등 더미값)
        float t = 0f;
        while (_cam.width <= 16 && t < 3f) { t += Time.deltaTime; yield return null; }

        BuildUI();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private IEnumerator RequestAndroidCameraPermission()
    {
        if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            yield break;

        bool done = false;
        var callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += _ => done = true;
        callbacks.PermissionDenied += _ => done = true;
        callbacks.PermissionDeniedAndDontAskAgain += _ => done = true;
        Permission.RequestUserPermission(Permission.Camera, callbacks);

        // 콜백이 안 올 수도 있으니 권한 결정 또는 타임아웃까지 대기
        float t = 0f;
        while (!done && !Permission.HasUserAuthorizedPermission(Permission.Camera) && t < 30f)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }
#endif

    private void BuildUI()
    {
        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // 다른 UI 위에 표시
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var bg = CreateChild("Bg", canvasGo.transform);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = Color.black;
        Stretch(bg.GetComponent<RectTransform>());

        var previewGo = CreateChild("Preview", canvasGo.transform);
        var preview = previewGo.AddComponent<RawImage>();
        preview.texture = _cam;
        var pr = previewGo.GetComponent<RectTransform>();
        Stretch(pr);
        // 센서 방향 보정 (미리보기)
        pr.localEulerAngles = new Vector3(0f, 0f, -_cam.videoRotationAngle);
        pr.localScale = new Vector3(1f, _cam.videoVerticallyMirrored ? -1f : 1f, 1f);

        // 하단 중앙 원형 셔터 (진짜 카메라 셔터 느낌: 흰 링 + 갭 + 흰 중심)
        var shoot = CreateShutterButton(canvasGo.transform);
        shoot.onClick.AddListener(OnShoot);
    }

    private void OnShoot()
    {
        if (_busy || _cam == null || !_cam.isPlaying) return;
        _busy = true;

        try
        {
            var src = new Texture2D(_cam.width, _cam.height, TextureFormat.RGBA32, false);
            src.SetPixels32(_cam.GetPixels32());
            src.Apply();

            // OCR이 똑바로 인식하도록 저장 이미지에 센서 회전/미러를 픽셀에 굽는다
            var upright = RotateForUpright(src, _cam.videoRotationAngle, _cam.videoVerticallyMirrored);
            if (upright != src) Destroy(src);

            byte[] jpg = upright.EncodeToJPG(90);
            Destroy(upright);

            string path = Path.Combine(Application.temporaryCachePath, "incam_capture.jpg");
            File.WriteAllBytes(path, jpg);
            Finish(path);
        }
        catch (Exception e)
        {
            Debug.LogError("[InAppCamera] 촬영 실패: " + e);
            Finish(null);
        }
    }

    private static Texture2D RotateForUpright(Texture2D src, int angle, bool mirrored)
    {
        angle = ((angle % 360) + 360) % 360;
        if (angle == 0 && !mirrored) return src;

        int w = src.width, h = src.height;
        Color32[] sp = src.GetPixels32();
        bool swap = (angle == 90 || angle == 270);
        int dw = swap ? h : w;
        int dh = swap ? w : h;
        var dp = new Color32[sp.Length];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int sx = mirrored ? (w - 1 - x) : x;
                Color32 c = sp[y * w + sx];
                int nx, ny;
                switch (angle)
                {
                    case 90:  nx = y;           ny = (w - 1 - x); break;
                    case 180: nx = (w - 1 - x); ny = (h - 1 - y); break;
                    case 270: nx = (h - 1 - y); ny = x;           break;
                    default:  nx = x;           ny = y;           break;
                }
                dp[ny * dw + nx] = c;
            }
        }

        var dst = new Texture2D(dw, dh, TextureFormat.RGBA32, false);
        dst.SetPixels32(dp);
        dst.Apply();
        return dst;
    }

    private void Finish(string path)
    {
        var cb = _onResult;
        _onResult = null;
        StopCam();
        cb?.Invoke(path);
        Destroy(gameObject);
    }

    private void StopCam()
    {
        if (_cam == null) return;
        if (_cam.isPlaying) _cam.Stop();
        Destroy(_cam);
        _cam = null;
    }

    private void OnDestroy() => StopCam();

    // ---- 런타임 UI 헬퍼 ----
    private static GameObject CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    // 빌트인 원형 스프라이트(Knob.psd)를 쌓아 만든 카메라 셔터.
    // 그림자(입체감) + 흰 링 + 가는 갭 + 큰 흰 중심 → 실제 카메라 셔터 모양.
    private static Button CreateShutterButton(Transform parent)
    {
        var circle = CircleSprite();

        // 클릭 영역 컨테이너 (투명, 레이캐스트만 받음)
        var go = new GameObject("Shutter");
        go.transform.SetParent(parent, false);
        var hit = go.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        var btn = go.AddComponent<Button>();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 120f);
        rt.sizeDelta = new Vector2(200f, 200f);

        AddCircle("Shadow", go.transform, circle, new Color(0f, 0f, 0f, 0.30f), 198f);
        AddCircle("Ring",   go.transform, circle, Color.white,                  182f);
        AddCircle("Gap",    go.transform, circle, new Color(0.1f, 0.1f, 0.1f, 0.9f), 158f);
        var inner = AddCircle("Inner", go.transform, circle, Color.white,        146f);

        // 누를 때 중심 흰 원이 눌린 느낌으로 틴트
        btn.targetGraphic = inner;
        return btn;
    }

    // 빌트인 원형 스프라이트(Knob.psd)는 런타임 빌드에서 null이라 Image가 사각형으로 그려진다.
    // 안전하게 흰 원(가장자리 안티앨리어싱)을 코드로 만들어 캐싱한다. 색은 Image.color로 입힌다.
    private static Sprite _circleSprite;
    private static Sprite CircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = S * 0.5f;
        float c = r - 0.5f;
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = x - c, dy = y - c;
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy)); // 가장자리 1px 부드럽게
                px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return _circleSprite;
    }

    private static Image AddCircle(string name, Transform parent, Sprite circle, Color color, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = circle;
        img.color = color;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        return img;
    }

}

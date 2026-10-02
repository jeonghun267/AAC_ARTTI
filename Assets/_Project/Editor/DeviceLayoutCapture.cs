using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Artti.Editor
{
    // 기기 해상도로 각 씬의 화면을 PNG로 찍는 레이아웃 점검 도구.
    // 씬은 저장하지 않는다. 찍는 동안 바꾼 캔버스 설정은 메모리에만 있고, 다음 씬을 열 때 버려진다.
    // 결과: <프로젝트>/Temp/LayoutCaptures/<태그>_<씬>.png (Temp는 git 제외, 에디터를 끄면 지워짐)
    public static class DeviceLayoutCapture
    {
        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LayoutCaptures"));

        static readonly string[] Scenes =
        {
            ScenePaths.Splash, ScenePaths.ProfileSelect, ScenePaths.ProfileSettings, ScenePaths.ProfileCreate, ScenePaths.Main,
            ScenePaths.TrainingHub, ScenePaths.Report, ScenePaths.RecordDetail, ScenePaths.TrainingConvenience,
        };

        [MenuItem("Artti/Debug/Capture Scenes - Tab S7 FE (2560x1600)")]
        static void CaptureTablet() => CaptureAll(2560, 1600, "tab");

        [MenuItem("Artti/Debug/Capture Scenes - Phone 20x9 (2400x1080)")]
        static void CapturePhone() => CaptureAll(2400, 1080, "phone");

        [MenuItem("Artti/Debug/Capture Scenes - 16x9 (1920x1080)")]
        static void Capture169() => CaptureAll(1920, 1080, "169");

        static void CaptureAll(int w, int h, string tag)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[DeviceLayoutCapture] Play 모드에서는 실행하지 않는다."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var original = EditorSceneManager.GetActiveScene().path;
            Directory.CreateDirectory(OutDir);
            try
            {
                foreach (var path in Scenes)
                {
                    if (!File.Exists(path)) continue;
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var file = Path.Combine(OutDir, $"{tag}_{Path.GetFileNameWithoutExtension(path)}.png");
                    CaptureActiveScene(w, h, file);
                }
            }
            finally
            {
                // 찍느라 바꾼 설정을 버리고 원래 씬을 디스크 상태로 다시 연다.
                if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
            Debug.Log($"[DeviceLayoutCapture] 완료 {w}x{h} -> {OutDir}");
        }

        // 프로필 등록 2단계(프로필 설정)와 3단계(완료)는 1단계에 가려져 있어 따로 찍는다.
        // 완료는 사진 속 25, 머리가 가장 높은 27(올림머리), 22(모자). 씬은 저장하지 않는다.
        [MenuItem("Artti/Debug/Capture ProfileCreate Step 2 + Done Step - Tab S7 FE (2560x1600)")]
        static void CaptureDoneStep()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var original = EditorSceneManager.GetActiveScene().path;
            Directory.CreateDirectory(OutDir);
            try
            {
                // 변경점: 1단계 칸을 고른 모양(LED 테두리 확인용). 실행 중에만 켜지는 선택 테두리를 첫 칸에 켠다.
                EditorSceneManager.OpenScene(ScenePaths.ProfileCreate, OpenSceneMode.Single);
                foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name == "SelectedBorder" && t.parent.name == "Avatar_19") t.gameObject.SetActive(true);
                CaptureActiveScene(2560, 1600, Path.Combine(OutDir, "tab_ProfileStep1_selected.png"));

                // 변경점: 2단계(인용문 띠 확인용)
                EditorSceneManager.OpenScene(ScenePaths.ProfileCreate, OpenSceneMode.Single);
                foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "AvatarStep" || t.name == "DoneStep") t.gameObject.SetActive(false);
                        if (t.name == "ProfileStep") t.gameObject.SetActive(true);
                    }
                CaptureActiveScene(2560, 1600, Path.Combine(OutDir, "tab_ProfileStep2.png"));

                foreach (var id in new[] { "25", "27", "22" })
                {
                    EditorSceneManager.OpenScene(ScenePaths.ProfileCreate, OpenSceneMode.Single);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/UI/Home/profiles/{id}.png");
                    foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.name == "AvatarStep" || t.name == "ProfileStep") t.gameObject.SetActive(false);
                            if (t.name != "DoneStep") continue;
                            t.gameObject.SetActive(true);
                            foreach (var c in t.GetComponentsInChildren<Transform>(true))
                                if (c.name == "Avatar" && c.parent.name == "AvatarMask" && c.TryGetComponent<Image>(out var img)) img.sprite = sprite;
                        }
                    }
                    CaptureActiveScene(2560, 1600, Path.Combine(OutDir, $"tab_ProfileDone_{id}.png"));
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
            Debug.Log($"[DeviceLayoutCapture] 완료 화면 캡처 -> {OutDir}");
        }

        // 프로필 설정 화면의 삭제 확인 팝업 3가지 상태(빈 칸 / 입력 중 / 틀렸을 때 안내) + 사용자 선택 화면 카드 줄(선택 카드 튀어나옴).
        // 변경점: 삭제는 프로필 설정 화면으로 옮겼다. 카드는 실행 중에만 생기므로 프리팹을 놓고 바로 선택 모양으로 바꿔 보여 준다.
        // 씬은 저장하지 않는다.
        [MenuItem("Artti/Debug/Capture Delete Popup + Profile Cards - Tab S7 FE (2560x1600)")]
        static void CaptureDeletePopup()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var original = EditorSceneManager.GetActiveScene().path;
            Directory.CreateDirectory(OutDir);
            try
            {
                // card0/1/3: 사용자 카드 0/1/3장(첫 장 선택). View.Refresh로 줄 정렬(적으면 가운데, 넘치면 왼쪽부터)까지 확인
                // card3/card5: 사용자 3명/5명 = 원통 회전 줄. card5는 원통을 반 칸 돌린 모양(끌고 있는 중)
                foreach (var state in new[] { "empty", "typing", "hint", "card0", "card1", "card3", "card5" })
                {
                    bool cardState = state.StartsWith("card");
                    EditorSceneManager.OpenScene(cardState ? ScenePaths.ProfileSelect : ScenePaths.ProfileSettings, OpenSceneMode.Single);
                    foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.name == "Content" && cardState)
                            {
                                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Profile/ProfileCard.prefab");
                                int count = state[state.Length - 1] - '0';
                                for (int i = 0; i < count; i++)
                                    ((GameObject)PrefabUtility.InstantiatePrefab(prefab, t)).GetComponent<Artti.UI.ProfileCardView>().SetSelectedImmediate(i == 0);
                            }
                            if (t.name != "DeleteConfirmPopup" || cardState) continue;
                            t.gameObject.SetActive(true);
                            var input = t.GetComponentInChildren<TMPro.TMP_InputField>(true);
                            var cover = t.Find("Panel/FieldCover");
                            if (state == "typing") input.text = "동의한";
                            if (state == "hint" && input.placeholder is TMPro.TMP_Text ph) ph.text = "'동의한다'를 정확히 입력해 주세요";
                            if (cover != null) cover.gameObject.SetActive(state != "empty");
                        }
                    }
                    if (cardState)
                    {
                        foreach (var v in Object.FindObjectsByType<Artti.UI.ProfileSelectView>(FindObjectsSortMode.None))
                        {
                            v.Refresh();
                            v.PreviewCylinder(state == "card5" ? 20f : 0f);   // 실행 중 Start가 만드는 원통(카드 4장 이상)
                        }
                    }
                    CaptureActiveScene(2560, 1600, Path.Combine(OutDir, $"tab_DeletePopup_{state}.png"));
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
            Debug.Log($"[DeviceLayoutCapture] 삭제 팝업 캡처 -> {OutDir}");
        }

        // 변경점: AR 현장모드 결과 화면(HJ 시안). 결과 패널은 실행 중에만 켜지므로 켜고 예시 글/사진을 넣어 태블릿·폰·16:9로 찍는다.
        // 씬은 저장하지 않는다.
        [MenuItem("Artti/Debug/Capture ARField Result - Tab + Phone + 16x9")]
        static void CaptureARFieldResult()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var original = EditorSceneManager.GetActiveScene().path;
            Directory.CreateDirectory(OutDir);
            try
            {
                foreach (var (w, h, tag) in new[] { (2560, 1600, "tab"), (2400, 1080, "phone"), (1920, 1080, "169") })
                {
                    EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
                    foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        var panel = root.transform.Find("ResultPanel");
                        if (panel == null) continue;
                        foreach (Transform sib in root.transform)
                            if (sib is RectTransform) sib.gameObject.SetActive(sib == panel);
                        FillARResultSample(panel);
                    }
                    CaptureActiveScene(w, h, Path.Combine(OutDir, $"{tag}_ARFieldResult.png"));
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
            Debug.Log($"[DeviceLayoutCapture] AR 결과 화면 캡처 -> {OutDir}");
        }

        static void FillARResultSample(Transform panel)
        {
            var controller = Object.FindFirstObjectByType<OcrTestController>(FindObjectsInactive.Include);
            if (controller != null)
            {
                var so = new SerializedObject(controller);
                if (so.FindProperty("categoryText").objectReferenceValue is TMPro.TMP_Text name) name.text = "메가MGC커피";
                if (so.FindProperty("descriptionText").objectReferenceValue is TMPro.TMP_Text desc)
                    desc.text = "촬영한 이미지에서 카페 '메가MGC커피'로 인식되었습니다. 해당 장소에 대한 다양한 의사소통 도움 내용을 확인할 수 있습니다.";
                if (so.FindProperty("capturedImage").objectReferenceValue is RawImage photo)
                {
                    photo.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/UI/Home/main/2.png");
                    photo.color = Color.white;
                    if (photo.TryGetComponent<Artti.UI.RawImageCover>(out var cover)) cover.Apply();
                }
                if (so.FindProperty("aacImageComponent").objectReferenceValue is Image aac)
                {
                    var cafe = Resources.Load<Sprite>("AAC/cafe");
                    if (cafe != null) { aac.sprite = cafe; aac.color = Color.white; }
                }
            }
            var map = Object.FindFirstObjectByType<NaverStaticMapController>(FindObjectsInactive.Include);
            if (map != null)
            {
                var so = new SerializedObject(map);
                if (so.FindProperty("latitudeText").objectReferenceValue is TMPro.TMP_Text lat) lat.text = "위도: 37.566500";
                if (so.FindProperty("longitudeText").objectReferenceValue is TMPro.TMP_Text lon) lon.text = "경도: 126.978000";
            }
            var view = panel.GetComponent<Artti.UI.ARResultView>();
            if (view != null)
            {
                var so = new SerializedObject(view);
                // 변경점: 칩 = 지점 이름(긴 이름으로 칩이 두 줄로 늘어나는지 확인)
                const string chipSample = "메가커피 폴리텍인천캠퍼스점";   // 두 줄로 바뀌는 긴 이름
                if (so.FindProperty("chip").objectReferenceValue is GameObject chipGo && chipGo.TryGetComponent<Artti.UI.TextChipAutoWidth>(out var fit))
                    fit.SetText(chipSample);
                else if (so.FindProperty("chipText").objectReferenceValue is TMPro.TMP_Text chip) chip.text = chipSample;
            }
        }

        // 변경점: 점원 검증(ArttiMaleClerkCheck)이 점원 RT를 그린 뒤 같은 방식으로 화면 전체를 찍도록 internal로 연다.
        internal static void CaptureActiveScene(int w, int h, string file)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            GameObject tempCamGo = null;
            try
            {
                // 화면에 그리는 카메라(타깃 텍스처 없는 것)만 rt로 돌린다. 없으면 런타임 ClearCameraGuard와 같은 검은 카메라를 둔다.
                var screenCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(c => c.enabled && c.targetTexture == null).OrderBy(c => c.depth).ToList();
                if (screenCams.Count == 0)
                {
                    tempCamGo = new GameObject("[CaptureCamera]");
                    var cam = tempCamGo.AddComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                    cam.cullingMask = 0;
                    cam.orthographic = true;
                    tempCamGo.transform.position = new Vector3(0f, 0f, -10f);
                    screenCams.Add(cam);
                }
                var top = screenCams[screenCams.Count - 1];

                // Overlay 캔버스는 맨 위 카메라에 붙여 같이 그린다. 스케일은 CanvasScaler 공식을 기기 해상도로 직접 계산.
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                             .Where(c => c.isRootCanvas).OrderBy(c => c.sortingOrder))
                {
                    var scaler = canvas.GetComponent<CanvasScaler>();
                    float scale = scaler != null ? ScaleFactor(scaler, w, h) : 1f;
                    if (scaler != null) scaler.enabled = false;
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = top;
                        canvas.planeDistance = top.nearClipPlane + 0.05f;
                        top.cullingMask |= 1 << canvas.gameObject.layer;
                    }
                    canvas.scaleFactor = scale;
                }

                foreach (var c in screenCams) { c.targetTexture = rt; c.aspect = (float)w / h; }
                // 시작 연출용 전체 암전(스플래시 Fade)은 치운다
                foreach (var g in Object.FindObjectsByType<Image>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (g.name == "Fade") g.gameObject.SetActive(false);

                RefreshLayout();
                foreach (var c in screenCams) RenderNow(c, rt);

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);

                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(c => c.isRootCanvas))
                    Debug.Log($"[DeviceLayoutCapture] {Path.GetFileName(file)} canvas '{canvas.name}' = {((RectTransform)canvas.transform).rect.size}");
                foreach (var c in screenCams) c.targetTexture = null;
            }
            finally
            {
                if (tempCamGo != null) Object.DestroyImmediate(tempCamGo);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        // 캔버스 크기가 바뀐 뒤 AspectRatioFitter/레이아웃을 즉시 다시 계산한다(에디터에서는 다음 Update까지 미뤄짐).
        static void RefreshLayout()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var f in Object.FindObjectsByType<AspectRatioFitter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                f.enabled = false;
                f.enabled = true;
            }
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(c => c.isRootCanvas))
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
            Canvas.ForceUpdateCanvases();
        }

        // CanvasScaler.HandleScaleWithScreenSize와 같은 계산
        static float ScaleFactor(CanvasScaler s, int w, int h)
        {
            if (s.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize) return s.scaleFactor;
            if (s.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return 1f;
            var r = s.referenceResolution;
            switch (s.screenMatchMode)
            {
                case CanvasScaler.ScreenMatchMode.Expand: return Mathf.Min(w / r.x, h / r.y);
                case CanvasScaler.ScreenMatchMode.Shrink: return Mathf.Max(w / r.x, h / r.y);
                default:
                    float lw = Mathf.Log(w / r.x, 2f), lh = Mathf.Log(h / r.y, 2f);
                    return Mathf.Pow(2f, Mathf.Lerp(lw, lh, s.matchWidthOrHeight));
            }
        }

        static void RenderNow(Camera camera, RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else camera.Render();
        }
    }
}

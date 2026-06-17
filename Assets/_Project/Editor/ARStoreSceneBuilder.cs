using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARFoundation;
using Artti.ARStore;

namespace Artti.Editor
{
    // Blender 제작 편의점(ARTTI_Store.fbx)을 현실 바닥에 월드 고정으로 띄우는 AR 쇼케이스 씬 빌더.
    // 탭으로 배치 → 폰을 좌우로 돌리면 매장 양끝이 보이는 "진짜 거기 있는" AR 경험 + 실물/미니어처 스케일 토글.
    // 메뉴: Artti > Build ARStoreScene Hierarchy
    // ARFieldScene(오정훈 손배치 OCR 씬)과는 별도 씬이라 그쪽을 건드리지 않는다.
    public static class ARStoreSceneBuilder
    {
        const string StoreFbxPath = "Assets/_Project/Models/Props/ConvenienceStore/ARTTI_Store.fbx";
        // 점원 합본 프리팹(나중에 store+점원을 묶어 만들면 이 경로로 교체). 지금은 매장만 배치.
        const string StagePrefabPath = StoreFbxPath;

        static readonly Color32 Primary = new Color32(26, 86, 219, 255);  // #1A56DB
        static readonly Color White = Color.white;

        [MenuItem("Artti/Build ARStoreScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            // 새 씬이면 빈 씬 생성, 이미 있으면 열기
            if (System.IO.File.Exists(ScenePaths.ARStore))
                EditorSceneManager.OpenScene(ScenePaths.ARStore, OpenSceneMode.Single);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SceneBuilderUtils.ClearRootObjects();
            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();

            // ===== [AR] AR Foundation 6.x 표준 구조 (ARFieldScene과 동일 패턴) + 평면/레이캐스트 =====
            var arRoot = new GameObject("[AR]");

            var arSession = new GameObject("AR Session");
            arSession.transform.SetParent(arRoot.transform);
            arSession.AddComponent<ARSession>();
            arSession.AddComponent<ARInputManager>();

            var originGo = new GameObject("XR Origin");
            originGo.transform.SetParent(arRoot.transform);
            var xrOrigin = originGo.AddComponent<XROrigin>();

            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(originGo.transform, false);
            xrOrigin.CameraFloorOffsetObject = cameraOffset;

            var arCameraGo = new GameObject("AR Camera");
            arCameraGo.transform.SetParent(cameraOffset.transform, false);
            arCameraGo.tag = "MainCamera";
            var cam = arCameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            arCameraGo.AddComponent<ARCameraManager>();
            arCameraGo.AddComponent<ARCameraBackground>();
            xrOrigin.Camera = cam;

            // 평면 인식 + 화면 탭 레이캐스트 (배치용) — XR Origin에 부착
            var planeManager = originGo.AddComponent<ARPlaneManager>();
            var raycastManager = originGo.AddComponent<ARRaycastManager>();

            // ===== [Canvas] UI (ScreenSpaceOverlay) =====
            var canvasGo = SceneBuilderUtils.CreateCanvas();
            var font = SceneBuilderUtils.GetKoreanFont();

            // 안내 패널 (배치 전 표시, 배치되면 placement가 끔)
            var guide = SceneBuilderUtils.CreatePanel("GuideRoot", canvasGo.transform);
            var guideText = SceneBuilderUtils.CreateTMPText("GuideText", guide.transform,
                "바닥을 천천히 비춘 뒤\n화면을 탭하면 매장이 놓여요", 60);
            var gRect = guideText.rectTransform;
            gRect.anchorMin = new Vector2(0.5f, 1f);
            gRect.anchorMax = new Vector2(0.5f, 1f);
            gRect.pivot = new Vector2(0.5f, 1f);
            gRect.anchoredPosition = new Vector2(0, -160);
            gRect.sizeDelta = new Vector2(960, 220);
            guideText.color = White;
            guideText.fontStyle = FontStyles.Bold;
            guideText.textWrappingMode = TextWrappingModes.Normal;
            guideText.raycastTarget = false; // 배치 탭을 막지 않게 (GuideRoot에 Image 미부착 → 전체화면 탭 그대로 통과)

            // 하단 버튼 행: [크기 전환] [다시 놓기]
            var scaleBtn = SceneBuilderUtils.CreateButton("크기 전환", canvasGo.transform, 48);
            scaleBtn.gameObject.name = "ScaleToggleBtn";
            var sRect = scaleBtn.GetComponent<RectTransform>();
            sRect.anchorMin = sRect.anchorMax = new Vector2(0.5f, 0f);
            sRect.pivot = new Vector2(1f, 0f);
            sRect.anchoredPosition = new Vector2(-20, 80);
            sRect.sizeDelta = new Vector2(440, 150);

            var resetBtn = SceneBuilderUtils.CreateButton("다시 놓기", canvasGo.transform, 48);
            resetBtn.gameObject.name = "ResetBtn";
            var rRect = resetBtn.GetComponent<RectTransform>();
            rRect.anchorMin = rRect.anchorMax = new Vector2(0.5f, 0f);
            rRect.pivot = new Vector2(0f, 0f);
            rRect.anchoredPosition = new Vector2(20, 80);
            rRect.sizeDelta = new Vector2(440, 150);

            // 뒤로가기 (Main으로)
            SceneBuilderUtils.CreateBackButton("MainScene", canvasGo.transform);

            // ===== [Managers] 배치 컨트롤러 =====
            var managers = new GameObject("[Managers]");
            var placementGo = new GameObject("ARStorePlacement");
            placementGo.transform.SetParent(managers.transform);
            var placement = placementGo.AddComponent<ARStorePlacement>();

            var storePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StagePrefabPath);
            if (storePrefab == null)
                Debug.LogWarning($"[ARStoreSceneBuilder] 매장 프리팹/FBX 없음: {StagePrefabPath}");

            var so = new SerializedObject(placement);
            so.FindProperty("raycastManager").objectReferenceValue = raycastManager;
            so.FindProperty("planeManager").objectReferenceValue = planeManager;
            so.FindProperty("storePrefab").objectReferenceValue = storePrefab;
            so.FindProperty("guideRoot").objectReferenceValue = guide;
            so.ApplyModifiedProperties();

            // 버튼 onClick 와이어링
            WireOnClick(scaleBtn, placement, nameof(ARStorePlacement.ToggleScale));
            WireOnClick(resetBtn, placement, nameof(ARStorePlacement.ResetPlacement));

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);

            // 저장 + 빌드세팅 등록 (런타임 SceneManager.LoadScene 가능하게)
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePaths.ARStore);
            RegisterInBuildSettings(ScenePaths.ARStore);

            Debug.Log("[ARStoreSceneBuilder] 완료 — AR 매장 배치 씬 생성(탭 배치 + 스케일 토글). 점원은 리깅 후 합본 예정.");
            Selection.activeGameObject = canvasGo;
        }

        static void WireOnClick(Button btn, ARStorePlacement target, string methodName)
        {
            var method = typeof(ARStorePlacement).GetMethod(methodName);
            if (method == null) { Debug.LogError($"[ARStoreSceneBuilder] {methodName} 메소드 없음"); return; }
            var action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), target, method);
            UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        static void RegisterInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[ARStoreSceneBuilder] 빌드 세팅에 씬 등록: {scenePath}");
        }
    }
}

using Artti.Editor;
using Artti.Training;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Artti.EditorTools
{
    // v09 3D 점원을 편의점 훈련 씬(PNG 대시보드)에 넣는다.
    //  - [ClerkStage] (원점에서 멀리 떨어진 x=50 위치): v09 프리팹 인스턴스 + ClerkView + ArttiLipSyncController,
    //    전용 ClerkCamera(투명 배경 -> RenderTexture) + 키/필 라이트.
    //  - 캔버스의 래스터 "Clerk" 이미지와 같은 Rect에 RawImage "Clerk3D"를 놓고 RenderTexture를 표시한다.
    //  - 래스터 "Clerk"는 삭제하지 않고 비활성화해 롤백 가능하게 보존한다.
    //  - TrainingSceneRoot.clerkView 를 새 ClerkView로 연결한다 (인사/끄덕임 호출이 실제 애니로 이어짐).
    // 씬 빌더(ConvenienceTrainingDashboardBuilder.Build)도 같은 Apply 를 호출하므로 재빌드해도 재현된다.
    // 메뉴: Artti > Integrate ARTTI Clerk v09 (TrainingConvenienceScene)
    public static class ArttiClerkSceneIntegration
    {
        public const string PrefabPath = ArttiClerkControllerBuilder.ModelPath;
        public const string ControllerPath = ArttiClerkControllerBuilder.ControllerPath;
        public const string RenderTexturePath = ArttiClerkControllerBuilder.RenderTexturePath;
        public const string StageName = "[ClerkStage]";
        public const string InstanceName = "ARTTI_Clerk_v09";
        public const string CameraName = "ClerkCamera";
        public const string RawImageName = "Clerk3D";
        public const string RasterName = "Clerk";

        // 무대는 다른 카메라(완료 화면 QA 캡처 등)의 시야에 들어오지 않도록 멀리 둔다. 레이어 추가 없이 격리.
        public static readonly Vector3 StageOrigin = new Vector3(50f, 0f, 0f);
        // 캐릭터는 +Z 를 바라본다. 카메라는 대화 거리(1.55 m)에서 상반신(허리~정수리)을 세로 2:3 로 잡는다.
        public static readonly Vector3 CameraLocalPosition = new Vector3(0f, 1.20f, 1.55f);
        public static readonly Vector3 CameraLookLocal = new Vector3(0f, 1.18f, 0f);
        public const float CameraFieldOfView = 34f;

        public struct Result
        {
            public GameObject stage;
            public GameObject instance;
            public Camera camera;
            public RawImage rawImage;
            public ClerkView clerkView;
            public ArttiLipSyncController lipSync;
        }

        [MenuItem("Artti/Integrate ARTTI Clerk v09 (TrainingConvenienceScene)")]
        public static void IntegrateMenu()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.TrainingConvenience);
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var sceneRoot = Object.FindFirstObjectByType<TrainingSceneRoot>(FindObjectsInactive.Include);
            if (canvas == null || sceneRoot == null)
            {
                Debug.LogError("[ArttiClerkSceneIntegration] Canvas or TrainingSceneRoot not found in " + ScenePaths.TrainingConvenience);
                return;
            }
            Transform rasterT = canvas.transform.Find(RasterName);
            Image raster = rasterT == null ? null : rasterT.GetComponent<Image>();
            if (raster == null)
            {
                Debug.LogError("[ArttiClerkSceneIntegration] raster clerk image '" + RasterName + "' not found under the canvas");
                return;
            }
            Result r = Apply(canvas.gameObject, raster, sceneRoot);
            if (r.stage == null)
            {
                return;
            }
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ArttiClerkSceneIntegration] 완료 — " + StageName + " + " + RawImageName + " 추가, 래스터 Clerk 비활성 보존, TrainingSceneRoot.clerkView 연결. 씬 저장됨.");
        }

        // 빌더와 메뉴가 공유하는 실제 통합 작업. 재실행하면 기존 무대/RawImage 를 지우고 다시 만든다.
        public static Result Apply(GameObject canvasGo, Image clerkRaster, TrainingSceneRoot sceneRoot)
        {
            var result = new Result();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (prefab == null || controller == null || rt == null)
            {
                Debug.LogError("[ArttiClerkSceneIntegration] missing asset — prefab " + (prefab != null) + ", controller " + (controller != null) +
                               " (run Artti/Build ARTTI Clerk Controller (v10) first), render texture " + (rt != null));
                return result;
            }

            // idempotent: remove a previous stage / raw image
            var oldStage = GameObject.Find(StageName);
            if (oldStage != null)
            {
                Object.DestroyImmediate(oldStage);
            }
            Transform oldRaw = canvasGo.transform.Find(RawImageName);
            if (oldRaw != null)
            {
                Object.DestroyImmediate(oldRaw.gameObject);
            }

            // stage + character
            var stage = new GameObject(StageName);
            stage.transform.position = StageOrigin;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = InstanceName;
            instance.transform.SetParent(stage.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            var animator = instance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;

            var clerkView = instance.AddComponent<ClerkView>();
            var cvSo = new SerializedObject(clerkView);
            cvSo.FindProperty("animator").objectReferenceValue = animator;
            cvSo.FindProperty("showDebugButtons").boolValue = false;   // 대시보드 UI 위에 겹치지 않게
            cvSo.ApplyModifiedPropertiesWithoutUndo();
            var lipSync = instance.AddComponent<ArttiLipSyncController>();
            lipSync.Bind();

            // camera -> render texture (transparent background)
            var camGo = new GameObject(CameraName);
            camGo.transform.SetParent(stage.transform, false);
            camGo.transform.localPosition = CameraLocalPosition;
            camGo.transform.LookAt(stage.transform.TransformPoint(CameraLookLocal));
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << 0;   // Default layer only (this scene has no other 3D content)
            cam.fieldOfView = CameraFieldOfView;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 10f;
            cam.depth = -1f;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.targetTexture = rt;
            UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
            camData.renderType = CameraRenderType.Base;
            camData.renderPostProcessing = false;
            camData.renderShadows = false;

            // lights (only this stage is on the Default layer, so the culling mask keeps them harmless)
            var key = new GameObject("ClerkKeyLight").AddComponent<Light>();
            key.transform.SetParent(stage.transform, false);
            key.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(38f, 205f, 0f);
            key.intensity = 1.15f;
            key.color = new Color(1f, 0.97f, 0.93f);
            key.shadows = LightShadows.None;
            key.cullingMask = 1 << 0;
            var fill = new GameObject("ClerkFillLight").AddComponent<Light>();
            fill.transform.SetParent(stage.transform, false);
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(15f, 140f, 0f);
            fill.intensity = 0.45f;
            fill.color = new Color(0.85f, 0.9f, 1f);
            fill.shadows = LightShadows.None;
            fill.cullingMask = 1 << 0;

            // raw image at the raster clerk's rect, drawn right after it in the hierarchy
            RawImage raw = SceneBuilderUtils.CreateRawImage(RawImageName, canvasGo.transform);
            RectTransform src = clerkRaster.rectTransform;
            RectTransform dst = raw.rectTransform;
            dst.anchorMin = src.anchorMin;
            dst.anchorMax = src.anchorMax;
            dst.pivot = src.pivot;
            dst.anchoredPosition = src.anchoredPosition;
            dst.sizeDelta = src.sizeDelta;
            dst.localScale = src.localScale;
            raw.transform.SetSiblingIndex(src.GetSiblingIndex() + 1);
            raw.texture = rt;
            raw.color = Color.white;
            raw.raycastTarget = false;

            // keep the raster for rollback, hidden
            clerkRaster.gameObject.SetActive(false);

            // wire the training flow
            var rootSo = new SerializedObject(sceneRoot);
            SerializedProperty prop = rootSo.FindProperty("clerkView");
            if (prop != null)
            {
                prop.objectReferenceValue = clerkView;
                rootSo.ApplyModifiedPropertiesWithoutUndo();
            }

            result.stage = stage;
            result.instance = instance;
            result.camera = cam;
            result.rawImage = raw;
            result.clerkView = clerkView;
            result.lipSync = lipSync;
            return result;
        }
    }
}

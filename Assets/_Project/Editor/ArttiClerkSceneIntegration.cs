using Artti.Editor;
using Artti.Training;
using Artti.CharacterKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Artti.EditorTools
{
    // Stationary v4 점원을 기존 편의점 대시보드에 넣는다.
    //  - [ClerkStage] (x=50): 대기/손 인사 프리팹 + ClerkView + 자동 음성 립싱크.
    //    전용 ClerkCamera(투명 배경 -> RenderTexture) + 키/필 라이트.
    //  - 캔버스의 래스터 "Clerk" 이미지와 같은 Rect에 RawImage "Clerk3D"를 놓고 RenderTexture를 표시한다.
    //  - 래스터 "Clerk"는 삭제하지 않고 비활성화해 롤백 가능하게 보존한다.
    //  - TrainingSceneRoot.clerkView 를 새 ClerkView로 연결한다 (인사/끄덕임 호출이 실제 애니로 이어짐).
    // 씬 빌더(ConvenienceTrainingDashboardBuilder.Build)도 같은 Apply 를 호출하므로 재빌드해도 재현된다.
    // 전체 대시보드를 다시 만들지 않고 점원 무대와 연결만 갱신한다.
    public static class ArttiClerkSceneIntegration
    {
        public const string KitRoot = "Assets/_Project/Art/Characters/ARTTI_Stationary_v4/";
        public const string PrefabPath = KitRoot + "ARTTI_Character.prefab";
        public const string ControllerPath = KitRoot + "Artti.controller";
        public const string RenderTexturePath = KitRoot + "Artti_ClerkRT.renderTexture";
        public const string StageName = "[ClerkStage]";
        public const string InstanceName = "ARTTI_Stationary_v4";
        public const string CameraName = "ClerkCamera";
        public const string RawImageName = "Clerk3D";
        public const string RasterName = "Clerk";

        // 무대는 다른 카메라(완료 화면 QA 캡처 등)의 시야에 들어오지 않도록 멀리 둔다. 레이어 추가 없이 격리.
        public static readonly Vector3 StageOrigin = new Vector3(50f, 0f, 0f);
        // 1.905m 모델의 머리와 손 인사를 모두 담고 기존 440x660 UI 슬롯을 유지한다.
        public static readonly Vector3 CameraLocalPosition = new Vector3(0f, 1.40f, 2.20f);
        public static readonly Vector3 CameraLookLocal = new Vector3(0f, 1.40f, 0f);
        public const float CameraFieldOfView = 40f;

        // 변경점: 대화 모드 남자 점원. 같은 무대 원점에 비활성으로 두고 ClerkCast가 모드에 맞춰 켠다.
        //   대기 자세 머리 꼭대기(머리카락 포함)가 v4 1.895m, 남자 1.884m로 거의 같아 v4와 같은 카메라를 쓴다
        //   (ArttiMaleClerkCheck 측정값). 모드별 값은 ClerkCast에 따로 저장되어 필요하면 한쪽만 조정할 수 있다.
        public const string MaleInstanceName = "ARTTI_Male";
        public static readonly Vector3 MaleCameraLocalPosition = new Vector3(0f, 1.40f, 2.20f);
        public static readonly Vector3 MaleCameraLookLocal = new Vector3(0f, 1.40f, 0f);
        public const float MaleCameraFieldOfView = 40f;

        public struct Result
        {
            public GameObject stage;
            public GameObject instance;
            public Camera camera;
            public RawImage rawImage;
            public ClerkView clerkView;
            public ArttiAudioLipSync lipSync;
        }

        [MenuItem("Artti/Integrate Stationary v4 Clerk (TrainingConvenienceScene)")]
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
            if (prefab == null || controller == null)
            {
                Debug.LogError("[ArttiClerkSceneIntegration] missing asset — prefab " + (prefab != null) + ", controller " + (controller != null) +
                               "; import the stationary v4 asset bundle first");
                return result;
            }
            if (rt == null)
            {
                rt = new RenderTexture(880, 1320, 24, RenderTextureFormat.ARGB32);
                rt.name = "Artti_ClerkRT";
                rt.antiAliasing = 4;
                AssetDatabase.CreateAsset(rt, RenderTexturePath);
            }
            // Batch imports may finish after InitializeOnLoad's first delayed material pass.
            Artti.CharacterKit.Editor.ArttiMaterialSetup.Configure();

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
            // The prefab's face driver is the sole writer of mouth shapes.
            var lipSync = instance.GetComponent<ArttiAudioLipSync>();
            var speech = instance.GetComponent<AudioSource>();
            speech.playOnAwake = false;
            speech.spatialBlend = 0f;
            lipSync.speechSource = speech;

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
            cam.aspect = (float)rt.width / rt.height;
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
            }
            rootSo.FindProperty("npcSpeechSource").objectReferenceValue = speech;
            rootSo.ApplyModifiedPropertiesWithoutUndo();
            // The dashboard builder creates its HUD later; its recipe uses the same source.
            var hud = canvasGo.GetComponent<ConvenienceHudView>();
            if (hud != null)
            {
                var hudSo = new SerializedObject(hud);
                hudSo.FindProperty("ttsSource").objectReferenceValue = speech;
                hudSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 변경점: 대화 모드 남자 점원도 같은 무대에 넣는다 (프리팹이 없으면 v4만 둔다).
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArttiMaleClerkSetup.PrefabPath) != null)
            {
                ApplyConversationClerk(stage, cam, sceneRoot);
            }

            result.stage = stage;
            result.instance = instance;
            result.camera = cam;
            result.rawImage = raw;
            result.clerkView = clerkView;
            result.lipSync = lipSync;
            return result;
        }

        // 변경점: 기존 무대는 건드리지 않고 대화 모드 남자 점원과 ClerkCast만 (다시) 넣는다.
        //   Clerk3D 슬롯·v4 점원·카메라 기본값은 그대로 두므로 손으로 옮긴 UI 배치가 있어도 유지된다.
        [MenuItem("Artti/Integrate Male Clerk for Conversation Mode (TrainingConvenienceScene)")]
        public static void IntegrateMaleMenu()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.TrainingConvenience);
            var stage = GameObject.Find(StageName);
            var sceneRoot = Object.FindFirstObjectByType<TrainingSceneRoot>(FindObjectsInactive.Include);
            Transform camT = stage == null ? null : stage.transform.Find(CameraName);
            if (stage == null || sceneRoot == null || camT == null)
            {
                Debug.LogError("[ArttiClerkSceneIntegration] " + StageName + " / " + CameraName + " / TrainingSceneRoot not found — run 'Integrate Stationary v4 Clerk' first");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArttiMaleClerkSetup.PrefabPath) == null)
            {
                Debug.LogError("[ArttiClerkSceneIntegration] male clerk prefab missing — run 'Artti/Male Clerk/Build Prefab' first");
                return;
            }
            if (ApplyConversationClerk(stage, camT.GetComponent<Camera>(), sceneRoot) == null)
            {
                return;
            }
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ArttiClerkSceneIntegration] 완료 — " + StageName + "/" + MaleInstanceName + " (대화 모드, 비활성 대기) + ClerkCast 추가, TrainingSceneRoot.clerkCast 연결. 씬 저장됨.");
        }

        public static ClerkCast ApplyConversationClerk(GameObject stage, Camera cam, TrainingSceneRoot sceneRoot)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArttiMaleClerkSetup.PrefabPath);
            Transform accuracyT = stage.transform.Find(InstanceName);
            ClerkView accuracyView = accuracyT == null ? null : accuracyT.GetComponent<ClerkView>();
            AudioSource accuracySpeech = accuracyT == null ? null : accuracyT.GetComponent<AudioSource>();
            if (prefab == null || accuracyView == null || accuracySpeech == null || cam == null)
            {
                Debug.LogError("[ArttiClerkSceneIntegration] conversation clerk skipped — prefab " + (prefab != null) +
                               ", accuracy clerk " + (accuracyView != null && accuracySpeech != null) + ", camera " + (cam != null));
                return null;
            }

            // idempotent: remove a previous male clerk / cast
            Transform oldMale = stage.transform.Find(MaleInstanceName);
            if (oldMale != null)
            {
                Object.DestroyImmediate(oldMale.gameObject);
            }
            var oldCast = stage.GetComponent<ClerkCast>();
            if (oldCast != null)
            {
                Object.DestroyImmediate(oldCast);
            }

            var male = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            male.name = MaleInstanceName;
            male.transform.SetParent(stage.transform, false);
            male.transform.localPosition = Vector3.zero;
            male.transform.localRotation = Quaternion.identity;
            var animator = male.GetComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;

            var maleView = male.AddComponent<ClerkView>();
            var cvSo = new SerializedObject(maleView);
            cvSo.FindProperty("animator").objectReferenceValue = animator;
            cvSo.FindProperty("showDebugButtons").boolValue = false;
            cvSo.ApplyModifiedPropertiesWithoutUndo();
            var maleSpeech = male.GetComponent<AudioSource>();
            maleSpeech.playOnAwake = false;
            maleSpeech.spatialBlend = 0f;
            male.GetComponent<ArttiAudioLipSync>().speechSource = maleSpeech;
            // 씬 기본값은 정확도 모드 점원. 대화 모드로 들어오면 ClerkCast가 켠다.
            male.SetActive(false);

            var cast = stage.AddComponent<ClerkCast>();
            var castSo = new SerializedObject(cast);
            castSo.FindProperty("clerkCamera").objectReferenceValue = cam;
            SetActor(castSo.FindProperty("accuracyClerk"), accuracyT.gameObject, accuracyView, accuracySpeech,
                CameraLocalPosition, CameraLookLocal, CameraFieldOfView);
            SetActor(castSo.FindProperty("conversationClerk"), male, maleView, maleSpeech,
                MaleCameraLocalPosition, MaleCameraLookLocal, MaleCameraFieldOfView);
            castSo.ApplyModifiedPropertiesWithoutUndo();

            var rootSo = new SerializedObject(sceneRoot);
            rootSo.FindProperty("clerkCast").objectReferenceValue = cast;
            rootSo.ApplyModifiedPropertiesWithoutUndo();
            return cast;
        }

        static void SetActor(SerializedProperty actor, GameObject root, ClerkView view, AudioSource speech,
                             Vector3 cameraLocal, Vector3 lookLocal, float fieldOfView)
        {
            actor.FindPropertyRelative("root").objectReferenceValue = root;
            actor.FindPropertyRelative("view").objectReferenceValue = view;
            actor.FindPropertyRelative("speech").objectReferenceValue = speech;
            actor.FindPropertyRelative("cameraLocalPosition").vector3Value = cameraLocal;
            actor.FindPropertyRelative("cameraLookLocal").vector3Value = lookLocal;
            actor.FindPropertyRelative("fieldOfView").floatValue = fieldOfView;
        }
    }
}

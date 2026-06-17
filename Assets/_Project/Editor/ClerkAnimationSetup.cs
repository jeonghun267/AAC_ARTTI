using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Artti.Editor
{
    // Mixamo 애니메이션 FBX(Without Skin)를 점원 Humanoid 아바타에 retarget하고
    // ClerkController(Idle 기본 + Greeting/HandOver/Nod 트리거)를 생성해 Clerk_Rigged.prefab에 연결한다.
    // 선행: Artti > Setup Clerk Character 로 Clerk_Avatar.asset 이 존재해야 함.
    // 메뉴: Artti > Setup Clerk Animations (Import + Controller)
    public static class ClerkAnimationSetup
    {
        const string AvatarPath       = "Assets/_Project/Models/Clerk_Avatar.asset";
        const string RiggedPrefabPath = "Assets/_Project/Models/Clerk_Rigged.prefab";
        const string ControllerPath   = "Assets/_Project/Art/ClerkController.controller";
        const string IdleFbxPath      = "Assets/_Project/Models/Animations/Idle.fbx";

        const string AnimDir = "Assets/_Project/Models/Animations";

        // 가져온 Mixamo 클립들 — 전부 원샷(루프 끔)
        const string WavingFbx   = AnimDir + "/Waving.fbx";
        const string NodFbx      = AnimDir + "/Head Nod Yes.fbx";
        const string HandOverFbx = AnimDir + "/Picking Up Object.fbx";
        const string PickUpFbx   = AnimDir + "/Picking Up.fbx";

        static readonly string[] OneShotFbx = { WavingFbx, NodFbx, HandOverFbx, PickUpFbx };

        [MenuItem("Artti/Setup Clerk Animations (Import + Controller)")]
        public static void Setup()
        {
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
            if (avatar == null)
            {
                Debug.LogError($"[ClerkAnim] Clerk_Avatar 없음: {AvatarPath} — 먼저 'Artti > Setup Clerk Character' 실행");
                return;
            }

            // 1) 각 Mixamo FBX를 Humanoid로 임포트(자체 아바타 생성), 클립 루프 끔.
            //    Humanoid 클립은 아바타를 공유할 필요 없음 — 런타임에 Animator가 Clerk_Avatar로 retarget.
            foreach (var path in OneShotFbx)
                ImportAsHumanoid(path, loop: false);

            // 2) 클립 로드
            var idle     = LoadClip(IdleFbxPath);
            var waving    = LoadClip(WavingFbx);
            var nod       = LoadClip(NodFbx);
            var handover  = LoadClip(HandOverFbx);

            // 3) 컨트롤러 (재실행 시 깨끗이 재생성)
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Greeting", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HandOver", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Nod",      AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            var idleState = sm.AddState("Idle");
            idleState.motion = idle;
            sm.defaultState = idleState;

            AddOneShot(sm, "Greeting", waving,   "Greeting", idleState, speed: 0.5f); // 손 흔들기 원본이 너무 빨라 절반 속도
            AddOneShot(sm, "HandOver", handover, "HandOver", idleState);
            AddOneShot(sm, "Nod",      nod,      "Nod",      idleState);

            AssetDatabase.SaveAssets();

            // 4) 점원 프리팹 Animator에 새 컨트롤러 연결
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedPrefabPath);
            if (prefab != null)
            {
                var root = PrefabUtility.LoadPrefabContents(RiggedPrefabPath);
                var animator = root.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.runtimeAnimatorController = controller;
                    PrefabUtility.SaveAsPrefabAsset(root, RiggedPrefabPath);
                    Debug.Log("[ClerkAnim] Clerk_Rigged.prefab Animator → ClerkController 연결");
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            else Debug.LogWarning($"[ClerkAnim] 점원 프리팹 없음: {RiggedPrefabPath}");

            Debug.Log("[ClerkAnim] 완료 — 트리거: Greeting / HandOver / Nod. 씬 빌더 재실행 시 점원에 반영됨.");
        }

        // FBX 임포터를 Humanoid(자체 아바타 생성)로 맞추고 모든 클립의 loopTime을 설정
        static void ImportAsHumanoid(string path, bool loop)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[ClerkAnim] 임포터 없음(파일/경로 확인): {path}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar  = null;

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
                clips[i].loopTime = loop;
            importer.clipAnimations = clips;

            importer.SaveAndReimport();
        }

        // 원샷 상태: AnyState -(트리거)-> 상태, 재생 끝나면 -> Idle 복귀
        static void AddOneShot(AnimatorStateMachine sm, string name, AnimationClip clip, string trigger, AnimatorState idle, float speed = 1f)
        {
            var state = sm.AddState(name);
            state.motion = clip;
            state.speed = speed;

            var enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = 0.1f;
            enter.canTransitionToSelf = false;

            var exit = state.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.15f;
        }

        static AnimationClip LoadClip(string path)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    return clip;
            Debug.LogWarning($"[ClerkAnim] 클립 없음: {path}");
            return null;
        }
    }
}

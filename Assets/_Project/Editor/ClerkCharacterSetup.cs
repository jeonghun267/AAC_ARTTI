using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Artti.Editor
{
    // VRoid 점원(Clerk.glb)을 Unity에 쓰기 위한 1회성 셋업 유틸.
    // glTFast로 들어온 .glb는 ModelImporter Rig 탭이 없어 Generic으로만 임포트되므로,
    // 코드로 J_Bip 본을 Unity HumanBodyBones에 매핑해 Humanoid 아바타를 빌드하고,
    // 기존 Convai Idle 클립(Humanoid)을 retarget한 Animator가 붙은 Clerk_Rigged.prefab을 생성한다.
    // 메뉴: Artti > Setup Clerk Character (Humanoid + Idle)
    public static class ClerkCharacterSetup
    {
        const string ClerkGlbPath       = "Assets/_Project/Models/Clerk.glb";
        const string AvatarPath         = "Assets/_Project/Models/Clerk_Avatar.asset";
        const string RiggedPrefabPath   = "Assets/_Project/Models/Clerk_Rigged.prefab";
        const string IdleAnimFbxPath    = "Assets/Convai SDK For Unity/Samples/BasicSample/Art/Animations/Convai_Anim_Sample_Locomotion_Idle_Loop.FBX";
        const string IdleControllerPath = "Assets/_Project/Art/ClerkIdle.controller";

        // Unity humanName (HumanTrait.BoneName) -> VRoid 본 Transform 이름.
        // 몸통/팔다리는 공백 없음, 손가락은 공백 있는 정식 명칭 사용.
        static readonly (string human, string bone)[] BoneMap =
        {
            ("Hips","J_Bip_C_Hips"),
            ("Spine","J_Bip_C_Spine"),
            ("Chest","J_Bip_C_Chest"),
            ("UpperChest","J_Bip_C_UpperChest"),
            ("Neck","J_Bip_C_Neck"),
            ("Head","J_Bip_C_Head"),
            ("LeftShoulder","J_Bip_L_Shoulder"),
            ("LeftUpperArm","J_Bip_L_UpperArm"),
            ("LeftLowerArm","J_Bip_L_LowerArm"),
            ("LeftHand","J_Bip_L_Hand"),
            ("RightShoulder","J_Bip_R_Shoulder"),
            ("RightUpperArm","J_Bip_R_UpperArm"),
            ("RightLowerArm","J_Bip_R_LowerArm"),
            ("RightHand","J_Bip_R_Hand"),
            ("LeftUpperLeg","J_Bip_L_UpperLeg"),
            ("LeftLowerLeg","J_Bip_L_LowerLeg"),
            ("LeftFoot","J_Bip_L_Foot"),
            ("LeftToes","J_Bip_L_ToeBase"),
            ("RightUpperLeg","J_Bip_R_UpperLeg"),
            ("RightLowerLeg","J_Bip_R_LowerLeg"),
            ("RightFoot","J_Bip_R_Foot"),
            ("RightToes","J_Bip_R_ToeBase"),
            ("Left Thumb Proximal","J_Bip_L_Thumb1"),
            ("Left Thumb Intermediate","J_Bip_L_Thumb2"),
            ("Left Thumb Distal","J_Bip_L_Thumb3"),
            ("Left Index Proximal","J_Bip_L_Index1"),
            ("Left Index Intermediate","J_Bip_L_Index2"),
            ("Left Index Distal","J_Bip_L_Index3"),
            ("Left Middle Proximal","J_Bip_L_Middle1"),
            ("Left Middle Intermediate","J_Bip_L_Middle2"),
            ("Left Middle Distal","J_Bip_L_Middle3"),
            ("Left Ring Proximal","J_Bip_L_Ring1"),
            ("Left Ring Intermediate","J_Bip_L_Ring2"),
            ("Left Ring Distal","J_Bip_L_Ring3"),
            ("Left Little Proximal","J_Bip_L_Little1"),
            ("Left Little Intermediate","J_Bip_L_Little2"),
            ("Left Little Distal","J_Bip_L_Little3"),
            ("Right Thumb Proximal","J_Bip_R_Thumb1"),
            ("Right Thumb Intermediate","J_Bip_R_Thumb2"),
            ("Right Thumb Distal","J_Bip_R_Thumb3"),
            ("Right Index Proximal","J_Bip_R_Index1"),
            ("Right Index Intermediate","J_Bip_R_Index2"),
            ("Right Index Distal","J_Bip_R_Index3"),
            ("Right Middle Proximal","J_Bip_R_Middle1"),
            ("Right Middle Intermediate","J_Bip_R_Middle2"),
            ("Right Middle Distal","J_Bip_R_Middle3"),
            ("Right Ring Proximal","J_Bip_R_Ring1"),
            ("Right Ring Intermediate","J_Bip_R_Ring2"),
            ("Right Ring Distal","J_Bip_R_Ring3"),
            ("Right Little Proximal","J_Bip_R_Little1"),
            ("Right Little Intermediate","J_Bip_R_Little2"),
            ("Right Little Distal","J_Bip_R_Little3"),
        };

        [MenuItem("Artti/Setup Clerk Character (Humanoid + Idle)")]
        public static void Setup()
        {
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(ClerkGlbPath);
            if (glb == null)
            {
                Debug.LogError($"[ClerkSetup] Clerk.glb 없음: {ClerkGlbPath}");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(glb);
            instance.name = "Clerk";
            try
            {
                // 본 이름 -> Transform (중복 이름은 첫 번째만)
                var allTransforms = instance.GetComponentsInChildren<Transform>(true);
                var byName = new Dictionary<string, Transform>();
                foreach (var t in allTransforms)
                    if (!byName.ContainsKey(t.name)) byName[t.name] = t;

                // HumanBone 매핑 (해당 본이 실제 존재할 때만)
                var human = new List<HumanBone>();
                var missing = new List<string>();
                foreach (var (humanName, boneName) in BoneMap)
                {
                    if (!byName.ContainsKey(boneName))
                    {
                        missing.Add(boneName);
                        continue;
                    }
                    var hb = new HumanBone { humanName = humanName, boneName = boneName };
                    hb.limit.useDefaultValues = true;
                    human.Add(hb);
                }
                if (missing.Count > 0)
                    Debug.LogWarning($"[ClerkSetup] 매핑 안 된 본 {missing.Count}개(손가락 등이면 무시 가능): {string.Join(", ", missing)}");

                // SkeletonBone: 인스턴스 루트 포함 모든 Transform의 바인드 포즈
                var skeleton = new List<SkeletonBone>();
                foreach (var t in allTransforms)
                {
                    skeleton.Add(new SkeletonBone
                    {
                        name = t.name,
                        position = t.localPosition,
                        rotation = t.localRotation,
                        scale = t.localScale
                    });
                }

                var hd = new HumanDescription
                {
                    human = human.ToArray(),
                    skeleton = skeleton.ToArray(),
                    upperArmTwist = 0.5f, lowerArmTwist = 0.5f,
                    upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                    armStretch = 0.05f, legStretch = 0.05f,
                    feetSpacing = 0f, hasTranslationDoF = false
                };

                var avatar = AvatarBuilder.BuildHumanAvatar(instance, hd);
                if (avatar == null || !avatar.isValid)
                {
                    Debug.LogError("[ClerkSetup] Humanoid 아바타 생성 실패 — 본 매핑/포즈 확인 필요");
                    return;
                }
                avatar.name = "Clerk_Avatar";

                if (AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath) != null)
                    AssetDatabase.DeleteAsset(AvatarPath);
                AssetDatabase.CreateAsset(avatar, AvatarPath);

                // Idle 컨트롤러 (기존 Convai Idle 클립 재사용)
                var controller = EnsureIdleController();

                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                if (controller != null) animator.runtimeAnimatorController = controller;

                PrefabUtility.SaveAsPrefabAsset(instance, RiggedPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ClerkSetup] 완료 — {RiggedPrefabPath} 생성 (Humanoid + Idle). 씬에 끌어다 놓고 Play로 idle 확인.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static RuntimeAnimatorController EnsureIdleController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(IdleControllerPath);
            if (existing != null) return existing;

            AnimationClip idle = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(IdleAnimFbxPath))
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview"))
                {
                    idle = clip;
                    break;
                }
            if (idle == null)
            {
                Debug.LogWarning($"[ClerkSetup] Idle 클립 없음: {IdleAnimFbxPath} — Animator는 컨트롤러 없이 생성됨");
                return null;
            }
            return AnimatorController.CreateAnimatorControllerAtPathWithClip(IdleControllerPath, idle);
        }
    }
}

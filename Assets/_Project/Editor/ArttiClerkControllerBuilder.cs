using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Artti.EditorTools
{
    // ARTTI 점원 v09 애니메이션 자산으로 v10 AnimatorController를 만든다.
    //  - 클립: v09@Clip.fbx 의 Humanoid 클립을 .anim 으로 복제하되 A/E/I/O/U(viseme) 커브를 제거해
    //    런타임 립싱크(ArttiLipSyncController)와 충돌하지 않게 한다. AEIOU_Test 는 원본 클립 그대로 두고 컨트롤러에 넣지 않는다.
    //  - 컨트롤러: Base 레이어 Idle_Smile(기본, 루프) -> Greeting / Wave_Hand(트리거) -> Idle_Smile 자동 복귀.
    //              Face 레이어(아바타 마스크: 몸 본 제외, 얼굴 메시 경로만) Neutral(빈 상태) -> Smile(트리거) -> Neutral.
    //  - 파라미터: Greeting, Wave, Smile (Trigger), IsTalking (Bool, 아직 전환에 미사용 — 이후 말하기 상태용 예약)
    //  - RenderTexture 자산(880x1320)도 함께 만든다 (씬 통합에서 사용).
    // 메뉴: Artti > Build ARTTI Clerk Controller (v10)
    public static class ArttiClerkControllerBuilder
    {
        public const string ModelPath = "Assets/_Project/Art/Characters/ARTTI_Clerk_v09/ARTTI_Clerk_UnityReady_v09.fbx";
        public const string OutDir = "Assets/_Project/Art/Characters/ARTTI_Clerk_v10";
        public const string AnimDir = OutDir + "/Animations";
        public const string ControllerPath = OutDir + "/ARTTI_Clerk.controller";
        public const string FaceMaskPath = OutDir + "/ARTTI_Clerk_Face.mask";
        public const string RenderTexturePath = OutDir + "/ARTTI_ClerkRT.renderTexture";
        public const int RenderTextureWidth = 880;
        public const int RenderTextureHeight = 1320;

        private static readonly string[] BodyClips = { "Idle_Smile", "Greeting", "Wave_Hand", "Smile" };
        private static readonly HashSet<string> VisemeShapes = new HashSet<string> { "blendShape.A", "blendShape.E", "blendShape.I", "blendShape.O", "blendShape.U" };
        private static readonly string[] FaceMeshNames = { "char1", "Teeth_Lower", "Teeth_Upper", "Tongue" };

        [MenuItem("Artti/Build ARTTI Clerk Controller (v10)")]
        public static void Build()
        {
            var log = new StringBuilder();
            EnsureFolder(OutDir);
            EnsureFolder(AnimDir);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Avatar avatar = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (asset is Avatar a)
                {
                    avatar = a;
                }
            }
            if (prefab == null || avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError("[ArttiClerkControllerBuilder] v09 model / humanoid avatar not found: " + ModelPath);
                return;
            }

            // 1) clip copies without viseme curves
            string modelDir = Path.GetDirectoryName(ModelPath).Replace('\\', '/');
            string stem = Path.GetFileNameWithoutExtension(ModelPath);
            var clips = new Dictionary<string, AnimationClip>();
            foreach (string name in BodyClips)
            {
                string fbx = modelDir + "/" + stem + "@" + name + ".fbx";
                EnsureHumanoidImport(fbx, avatar, name, loop: name == "Idle_Smile");
                AnimationClip source = LoadClip(fbx);
                if (source == null)
                {
                    Debug.LogError("[ArttiClerkControllerBuilder] clip missing in " + fbx);
                    return;
                }
                string outPath = AnimDir + "/" + name + ".anim";
                var copy = new AnimationClip();
                EditorUtility.CopySerialized(source, copy);
                copy.name = name;
                int removed = 0;
                foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(copy))
                {
                    if (VisemeShapes.Contains(b.propertyName))
                    {
                        AnimationUtility.SetEditorCurve(copy, b, null);
                        removed++;
                    }
                }
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
                settings.loopTime = name == "Idle_Smile";
                settings.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(copy, settings);
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(copy, existing);
                    clips[name] = existing;
                }
                else
                {
                    AssetDatabase.CreateAsset(copy, outPath);
                    clips[name] = copy;
                }
                int remaining = AnimationUtility.GetCurveBindings(clips[name]).Length;
                log.AppendLine("clip " + name + ": " + source.length.ToString("F2") + " s, loop=" + settings.loopTime + ", viseme curves removed " + removed + ", curves left " + remaining + " -> " + outPath);
            }

            // 2) face mask: no humanoid body parts, only the face mesh transforms (blendShape curves bind to those paths)
            var mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            }
            mask.AddTransformPath(prefab.transform, true);
            int enabledPaths = 0;
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                string leaf = path.Contains("/") ? path.Substring(path.LastIndexOf('/') + 1) : path;
                bool on = System.Array.IndexOf(FaceMeshNames, leaf) >= 0;
                mask.SetTransformActive(i, on);
                if (on)
                {
                    enabledPaths++;
                }
            }
            var existingMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(FaceMaskPath);
            if (existingMask != null)
            {
                EditorUtility.CopySerialized(mask, existingMask);
                mask = existingMask;
            }
            else
            {
                AssetDatabase.CreateAsset(mask, FaceMaskPath);
            }
            log.AppendLine("face mask: body parts off, transform paths enabled " + enabledPaths + " of " + mask.transformCount + " -> " + FaceMaskPath);

            // 3) controller (rebuilt from scratch every time)
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Greeting", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Wave", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Smile", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsTalking", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine baseSm = controller.layers[0].stateMachine;
            AnimatorState idle = baseSm.AddState("Idle_Smile", new Vector3(300, 0, 0));
            idle.motion = clips["Idle_Smile"];
            baseSm.defaultState = idle;
            AddOneShot(baseSm, idle, "Greeting", clips["Greeting"], "Greeting", new Vector3(600, -120, 0), log);
            AddOneShot(baseSm, idle, "Wave_Hand", clips["Wave_Hand"], "Wave", new Vector3(600, 120, 0), log);

            controller.AddLayer("Face");
            AnimatorControllerLayer[] layers = controller.layers;
            layers[1].avatarMask = mask;
            layers[1].defaultWeight = 1f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;
            AnimatorStateMachine faceSm = controller.layers[1].stateMachine;
            AnimatorState neutral = faceSm.AddState("Neutral", new Vector3(300, 0, 0));   // no motion: base layer face values pass through
            faceSm.defaultState = neutral;
            AddOneShot(faceSm, neutral, "Smile", clips["Smile"], "Smile", new Vector3(600, 0, 0), log);

            // 4) render texture for the dashboard slot (440x660 at 2x)
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (rt == null)
            {
                rt = new RenderTexture(RenderTextureWidth, RenderTextureHeight, 24, RenderTextureFormat.ARGB32);
                rt.name = "ARTTI_ClerkRT";
                rt.antiAliasing = 4;
                AssetDatabase.CreateAsset(rt, RenderTexturePath);
            }
            log.AppendLine("render texture " + rt.width + "x" + rt.height + " -> " + RenderTexturePath);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            log.AppendLine("controller: parameters Greeting/Wave/Smile (Trigger), IsTalking (Bool); Base: Idle_Smile(default) <-> Greeting, Wave_Hand; Face(mask): Neutral <-> Smile; AEIOU_Test not included");
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "ArttiIntegrationCheck"));
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "controller_build_report.txt"), log.ToString());
            Debug.Log("[ArttiClerkControllerBuilder] 완료 -> " + ControllerPath + "\n" + log);
        }

        // one-shot state: from `home` on trigger (no exit time, short blend), back to `home` when nearly finished
        private static void AddOneShot(AnimatorStateMachine sm, AnimatorState home, string stateName, AnimationClip clip, string trigger, Vector3 pos, StringBuilder log)
        {
            AnimatorState state = sm.AddState(stateName, pos);
            state.motion = clip;
            AnimatorStateTransition enter = home.AddTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            enter.duration = 0.2f;
            enter.canTransitionToSelf = false;
            AnimatorStateTransition exit = state.AddTransition(home);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.hasFixedDuration = true;
            exit.duration = 0.25f;
            log.AppendLine("state " + stateName + ": " + home.name + " -(" + trigger + ", blend 0.20 s)-> " + stateName + " -(exit 90 %, blend 0.25 s)-> " + home.name);
        }

        private static void EnsureHumanoidImport(string path, Avatar avatar, string clipName, bool loop)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null)
            {
                return;
            }
            bool dirty = false;
            if (imp.animationType != ModelImporterAnimationType.Human)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }
            if (imp.avatarSetup != ModelImporterAvatarSetup.CopyFromOther || imp.sourceAvatar != avatar)
            {
                imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                imp.sourceAvatar = avatar;
                dirty = true;
            }
            if (imp.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                dirty = true;
            }
            ModelImporterClipAnimation[] set = imp.clipAnimations;
            if (set == null || set.Length == 0)
            {
                set = imp.defaultClipAnimations;
            }
            bool changed = false;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i].name != clipName || set[i].loopTime != loop)
                {
                    set[i].name = clipName;
                    set[i].loopTime = loop;
                    changed = true;
                }
            }
            if (changed)
            {
                imp.clipAnimations = set;
                dirty = true;
            }
            if (dirty)
            {
                imp.SaveAndReimport();
            }
        }

        private static AnimationClip LoadClip(string path)
        {
            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var clip = obj as AnimationClip;
                if (clip != null && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}

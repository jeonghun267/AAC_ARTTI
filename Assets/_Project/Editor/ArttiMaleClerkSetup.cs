using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Artti.CharacterKit;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Artti.EditorTools
{
    // 대화 모드 남자 점원(ARTTI_Male) 프리팹 레시피. 메뉴: Artti > Male Clerk > Build Prefab
    //  - 원본: Artti_Male Unity 패키지 (Meshy 80k 모델 + Auto Rig 24본, 1.70m, 표정 키 10종)
    //  - 모델·동작: Generic. 동작 FBX는 모델과 같은 24본·같은 뼈 길이지만 기본 자세가 20~45도 달라서, Humanoid로
    //    옮기면 리타깃 오차로 팔꿈치가 꼬인다(손 인사 중 반대 팔). Generic은 뼈 회전을 그대로 재생해 원본 미리보기와 같다.
    //  - 클립: Idle(반복) / Wave(1회). 컨트롤러는 Stationary v4와 같은 파라미터(Speed, Wave)라 ClerkView가 그대로 동작한다.
    //    원본 Idle은 두리번거리므로(머리 로컬 최대 56도) 차분한 Idle(Artti_Male_Idle_Calm.anim)을 만들어 대신 쓴다.
    //  - 재질: URP Lit (BaseColor + Metallic/Smoothness 2048), 입 안 메시는 어두운 단색.
    //  - 표정·립싱크: 기존 CharacterKit(ArttiFaceDriver + ArttiAudioLipSync + 한국어 프로필)을 붙인다.
    //    패키지에 있던 전용 스크립트(ArttiFaceController 등)는 같은 입 모양 키를 동시에 쓰게 되므로 가져오지 않았다.
    // 재실행하면 임포트 설정·컨트롤러·재질·프리팹을 같은 경로에 다시 만든다 (GUID 유지).
    public static class ArttiMaleClerkSetup
    {
        public const string KitRoot = "Assets/_Project/Art/Characters/ARTTI_Male/";
        public const string ModelPath = KitRoot + "Models/Artti_Male.fbx";
        public const string IdlePath = KitRoot + "Animations/Artti_Male_Idle.fbx";
        public const string WavePath = KitRoot + "Animations/Artti_Male_Wave.fbx";
        public const string ControllerPath = KitRoot + "Artti_Male.controller";
        public const string PrefabPath = KitRoot + "ARTTI_Male_Character.prefab";
        public const string BodyMeshName = "Artti_Male_Body";
        public const string IdleClipName = "Artti_Male_Idle";
        public const string WaveClipName = "Artti_Male_Wave";
        public const string CalmIdleClipName = "Artti_Male_Idle_Calm";
        public const string CalmIdlePath = KitRoot + "Animations/" + CalmIdleClipName + ".anim";
        // 차분한 Idle: 첫 프레임(정면을 보는 자세)을 기준으로 몸 움직임은 이 비율만 남기고, 머리는 첫 프레임 방향을 유지한다.
        // 0.25는 머리 위치가 4.4cm 흔들려(v4 여자 점원 0.7cm) 0.1로 낮췄다.
        const float CalmBodyMotion = 0.1f;
        const int CalmKeyStep = 3;                        // 원본 30fps에서 3프레임마다 키 (10fps)
        const float ConstantCurveTolerance = 0.0001f;     // 쿼터니언 성분·위치(m)가 이보다 덜 변하면 상수 곡선
        public static readonly string[] FaceShapes = { "A", "E", "I", "O", "U", "JawOpen", "Blink_L", "Blink_R", "Smile", "MouthClose" };

        const string OralMeshName = "Artti_Oral_Cavity";
        const string BodyMaterialPath = KitRoot + "Materials/Artti_Male_Body.mat";
        const string OralMaterialPath = KitRoot + "Materials/Artti_Male_OralCavity.mat";
        const string BaseColorPath = KitRoot + "Textures/Artti_Male_BaseColor.png";
        const string MetallicPath = KitRoot + "Textures/Artti_Male_MetallicSmoothness.png";
        // 한국어 립싱크 프로필과 데모 음성은 Stationary v4 것을 같이 쓴다.
        const string VoiceProfilePath = ArttiClerkSceneIntegration.KitRoot + "Artti_KoreanVoice.asset";
        const string DemoSpeechPath = ArttiClerkSceneIntegration.KitRoot + "Audio/Artti_Korean_Demo.wav";

        // Generic 동작은 이 경로 이름으로 뼈를 찾는다. 모델과 동작 FBX 모두 이 뼈들이 있어야 한다.
        public const string RigName = "Artti_Male_Rig";
        public static readonly string[] Bones =
        {
            "Hips", "Spine02", "Spine01", "Spine", "neck", "Head",
            "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand",
            "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
        };

        [MenuItem("Artti/Male Clerk/Build Prefab")]
        public static void BuildMenu()
        {
            if (Build() != null)
                Debug.Log("[ArttiMaleClerkSetup] 완료 — " + PrefabPath + " (Generic, Idle/Wave, 표정 키 10종, URP 재질, 한국어 립싱크)");
        }

        public static GameObject Build()
        {
            GameObject instance = null;
            try
            {
                Avatar avatar = ConfigureModel();
                AnimationClip idle = ConfigureClip(IdlePath, IdleClipName, true, avatar);
                AnimationClip wave = ConfigureClip(WavePath, WaveClipName, false, avatar);
                AnimationClip calmIdle = BuildCalmIdle(idle, AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                AnimatorController controller = BuildController(calmIdle, wave);
                Material body = BuildBodyMaterial();
                Material oral = BuildOralMaterial();
                var voiceProfile = AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(VoiceProfilePath);
                var demoSpeech = AssetDatabase.LoadAssetAtPath<AudioClip>(DemoSpeechPath);
                if (voiceProfile == null) throw new FileNotFoundException("Korean lip-sync profile missing: " + VoiceProfilePath);

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = "ARTTI_Male_Character";
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                SkinnedMeshRenderer bodySkin = null;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material material = renderer.name == OralMeshName ? oral : body;
                    renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                    if (renderer is SkinnedMeshRenderer skin && skin.name == BodyMeshName) bodySkin = skin;
                }
                if (bodySkin == null || bodySkin.sharedMesh == null) throw new InvalidOperationException("Body mesh missing: " + BodyMeshName);
                foreach (string shape in FaceShapes)
                    if (bodySkin.sharedMesh.GetBlendShapeIndex(shape) < 0) throw new InvalidOperationException("Missing face shape: " + shape);

                var speech = instance.AddComponent<AudioSource>();
                speech.playOnAwake = false;
                speech.spatialBlend = 0f;
                var face = instance.AddComponent<ArttiFaceDriver>();
                var faceSo = new SerializedObject(face);
                var targets = faceSo.FindProperty("targets");
                targets.arraySize = 1;
                targets.GetArrayElementAtIndex(0).objectReferenceValue = bodySkin;
                faceSo.ApplyModifiedPropertiesWithoutUndo();
                var lipSync = instance.AddComponent<ArttiAudioLipSync>();
                lipSync.speechSource = speech;
                lipSync.voiceProfile = voiceProfile;
                lipSync.demoSpeech = demoSpeech;
                instance.AddComponent<ArttiAnimationDriver>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                if (prefab == null) throw new IOException("Failed to save " + PrefabPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                return null;
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        static Avatar ConfigureModel()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (importer == null || model == null) throw new FileNotFoundException("Missing model: " + ModelPath);
            RequireBones(model, ModelPath);

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importBlendShapes = true;
            importer.optimizeGameObjects = false;
            importer.useFileScale = true;
            importer.globalScale = 1f;
            // FBX에 4096 텍스처가 내장되어 있다. 재질은 직접 만든 URP 재질을 쓰므로 내장 재질/텍스처는 가져오지 않는다.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid)
                throw new InvalidOperationException("Generic Avatar validation failed: " + ModelPath);
            return avatar;
        }

        static void RequireBones(GameObject root, string path)
        {
            Transform rig = root.transform.Find(RigName);
            if (rig == null) throw new InvalidOperationException("Missing rig '" + RigName + "' in " + path);
            var names = new HashSet<string>(rig.GetComponentsInChildren<Transform>(true).Select(t => t.name));
            foreach (string bone in Bones)
                if (!names.Contains(bone)) throw new InvalidOperationException("Missing bone '" + bone + "' in " + path);
        }

        static AnimationClip ConfigureClip(string path, string clipName, bool loop, Avatar avatar)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new FileNotFoundException("Missing animation: " + path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            // 동작 FBX에는 리그 노드 하나만 있다(0.01배 스케일 + 축 회전). 계층을 보존하지 않으면 Unity가 그 노드를
            // 루트로 합치며 스케일·회전을 버리고, 뼈 경로도 "Artti_Male_Rig/Hips/..."로 모델과 맞지 않게 된다.
            importer.preserveHierarchy = true;

            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            if (takes.Length == 0) throw new InvalidOperationException("No animation take: " + path);
            // 여러 테이크가 있으면 가장 긴 것을 쓴다. 이동 동작은 없고(제자리), Animator의 root motion도 끈다.
            ModelImporterClipAnimation take = takes.OrderByDescending(t => t.lastFrame - t.firstFrame).First();
            take.name = clipName;
            take.loopTime = loop;
            take.loopPose = loop;
            importer.clipAnimations = new[] { take };
            importer.SaveAndReimport();
            RequireBones(AssetDatabase.LoadAssetAtPath<GameObject>(path), path);

            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
            if (clip == null) throw new InvalidOperationException("Clip import failed: " + path);
            return clip;
        }

        // 원본 Idle은 머리·목·엉덩이를 크게 돌려 두리번거린다. 첫 프레임(정면 자세, 루프 시작·끝과 같음)을 기준으로
        //  - 몸(엉덩이 이동 포함)은 원본 움직임의 CalmBodyMotion 비율만 남겨 숨쉬는 정도로 줄이고
        //  - 머리는 매 프레임 첫 프레임의 방향(캐릭터 기준)을 유지해 손님을 계속 바라보게 한다.
        // 결과는 같은 경로의 .anim에 덮어써 GUID를 유지한다.
        static AnimationClip BuildCalmIdle(AnimationClip source, GameObject model)
        {
            GameObject go = UnityEngine.Object.Instantiate(model);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Transform root = go.transform;
                root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                string[] paths = AnimationUtility.GetCurveBindings(source)
                    .Where(b => b.type == typeof(Transform)).Select(b => b.path).Distinct().ToArray();
                Transform[] bones = paths.Select(p => root.Find(p)).ToArray();
                if (paths.Length == 0 || bones.Any(b => b == null)) throw new InvalidOperationException("Idle bone paths do not match the model");
                Transform head = bones.FirstOrDefault(b => b.name == "Head");
                if (head == null) throw new InvalidOperationException("Idle clip does not animate Head");

                float rate = source.frameRate > 0f ? source.frameRate : 30f;
                int frames = Mathf.Max(1, Mathf.RoundToInt(source.length * rate));
                var rotations = new Quaternion[frames + 1, bones.Length];
                var positions = new Vector3[frames + 1, bones.Length];
                for (int f = 0; f <= frames; f++)
                {
                    source.SampleAnimation(go, f / rate);
                    for (int b = 0; b < bones.Length; b++)
                    {
                        rotations[f, b] = bones[b].localRotation;
                        positions[f, b] = bones[b].localPosition;
                    }
                }
                source.SampleAnimation(go, 0f);
                Quaternion headFacing = head.rotation; // root가 원점·무회전이라 캐릭터 기준 방향과 같다

                var values = new float[bones.Length * 7, frames + 1];
                for (int f = 0; f <= frames; f++)
                {
                    for (int b = 0; b < bones.Length; b++)
                    {
                        bones[b].localRotation = Quaternion.Slerp(rotations[0, b], rotations[f, b], CalmBodyMotion);
                        bones[b].localPosition = Vector3.Lerp(positions[0, b], positions[f, b], CalmBodyMotion);
                    }
                    head.localRotation = Quaternion.Inverse(head.parent.rotation) * headFacing;
                    for (int b = 0; b < bones.Length; b++)
                    {
                        Quaternion q = bones[b].localRotation;
                        Vector3 p = bones[b].localPosition;
                        int c = b * 7;
                        values[c, f] = q.x; values[c + 1, f] = q.y; values[c + 2, f] = q.z; values[c + 3, f] = q.w;
                        values[c + 4, f] = p.x; values[c + 5, f] = p.y; values[c + 6, f] = p.z;
                    }
                }

                // 용량: 모든 프레임을 키로 쓰면 .anim(텍스트)이 10MB가 된다. 거의 변하지 않는 곡선은 키 2개,
                // 나머지는 CalmKeyStep 프레임마다 키를 두고 부드러운 탄젠트로 잇는다 (움직임이 작고 느려 충분하다).
                var curves = new AnimationCurve[bones.Length * 7];
                for (int c = 0; c < curves.Length; c++)
                {
                    var curve = new AnimationCurve();
                    float first = values[c, 0], deviation = 0f;
                    for (int f = 1; f <= frames; f++) deviation = Mathf.Max(deviation, Mathf.Abs(values[c, f] - first));
                    if (deviation < ConstantCurveTolerance)
                    {
                        curve.AddKey(0f, first);
                        curve.AddKey(frames / rate, first);
                    }
                    else
                    {
                        for (int f = 0; f <= frames; f += CalmKeyStep) curve.AddKey(f / rate, values[c, f]);
                        if (frames % CalmKeyStep != 0) curve.AddKey(frames / rate, values[c, frames]);
                        for (int k = 0; k < curve.length; k++) curve.SmoothTangents(k, 0f);
                    }
                    curves[c] = curve;
                }

                var bindings = new EditorCurveBinding[curves.Length];
                string[] properties = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w",
                                        "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
                for (int b = 0; b < bones.Length; b++)
                    for (int p = 0; p < 7; p++)
                        bindings[b * 7 + p] = EditorCurveBinding.FloatCurve(paths[b], typeof(Transform), properties[p]);

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CalmIdlePath);
                if (clip == null)
                {
                    clip = new AnimationClip { name = CalmIdleClipName };
                    AssetDatabase.CreateAsset(clip, CalmIdlePath);
                }
                else
                {
                    clip.ClearCurves();
                }
                clip.frameRate = rate;
                AnimationUtility.SetEditorCurves(clip, bindings, curves);
                clip.EnsureQuaternionContinuity();
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);

                Vector2 before = MeasureHeadMotion(source, model);
                Vector2 after = MeasureHeadMotion(clip, model);
                Debug.Log($"[ArttiMaleClerkSetup] 차분한 Idle — 머리 방향 변화 최대 {before.x:F1}도 -> {after.x:F1}도, 머리 위치 이동 최대 {before.y * 100f:F1}cm -> {after.y * 100f:F1}cm");
                return clip;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // 클립 한 바퀴 동안 머리(Head)의 방향·위치가 첫 프레임에서 가장 많이 벗어난 값. x = 도, y = m.
        public static Vector2 MeasureHeadMotion(AnimationClip clip, GameObject model)
        {
            GameObject go = UnityEngine.Object.Instantiate(model);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Transform head = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head");
                float rate = clip.frameRate > 0f ? clip.frameRate : 30f;
                int frames = Mathf.Max(1, Mathf.RoundToInt(clip.length * rate));
                clip.SampleAnimation(go, 0f);
                Quaternion q0 = head.rotation;
                Vector3 p0 = head.position;
                float angle = 0f, move = 0f;
                for (int f = 1; f <= frames; f++)
                {
                    clip.SampleAnimation(go, f / rate);
                    angle = Mathf.Max(angle, Quaternion.Angle(q0, head.rotation));
                    move = Mathf.Max(move, Vector3.Distance(p0, head.position));
                }
                return new Vector2(angle, move);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // Stationary v4 Artti.controller와 같은 구조: Locomotion(Idle) 기본, Any State -> Wave(트리거), Wave 끝나면 Locomotion.
        static AnimatorController BuildController(AnimationClip idle, AnimationClip wave)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.parameters = new AnimatorControllerParameter[0];
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Wave", AnimatorControllerParameterType.Trigger);

            AnimatorState locomotion = machine.AddState("Locomotion", new Vector3(250f, 0f, 0f));
            locomotion.motion = idle;
            machine.defaultState = locomotion;
            AnimatorState waveState = machine.AddState("Wave", new Vector3(250f, 90f, 0f));
            waveState.motion = wave;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(waveState);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Wave");
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            enter.duration = 0.25f;
            enter.canTransitionToSelf = false;
            AnimatorStateTransition exit = waveState.AddTransition(locomotion);
            exit.hasExitTime = true;
            exit.exitTime = 0.94f;
            exit.hasFixedDuration = true;
            exit.duration = 0.3f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static Material LoadOrCreateMaterial(string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader missing");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            return material;
        }

        static Texture2D ConfigureTexture(string path, bool sRGB)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Missing texture: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = sRGB;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material BuildBodyMaterial()
        {
            Material material = LoadOrCreateMaterial(BodyMaterialPath);
            material.SetTexture("_BaseMap", ConfigureTexture(BaseColorPath, true));
            material.SetColor("_BaseColor", Color.white);
            // RGB = metallic, A = smoothness(1 - roughness). URP Lit은 메탈릭 맵 알파를 스무스니스로 읽는다.
            material.SetTexture("_MetallicGlossMap", ConfigureTexture(MetallicPath, false));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_Cull", 2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material BuildOralMaterial()
        {
            Material material = LoadOrCreateMaterial(OralMaterialPath);
            material.SetColor("_BaseColor", new Color(0.08f, 0.01f, 0.025f));
            material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

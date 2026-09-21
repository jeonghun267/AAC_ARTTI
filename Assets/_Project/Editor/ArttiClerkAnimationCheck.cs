using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Artti.EditorTools
{
    // ARTTI 점원 NPC v09 애니메이션 검증 도구.
    // 1) ARTTI_Clerk_UnityReady_v09@<Clip>.fbx 들을 Humanoid로 임포트하고 아바타를 모델 FBX의 아바타로 맞춘다 (Copy From Other Avatar).
    // 2) 클립 이름/길이/루프/커브(근육 + blendShape) 수를 보고서로 기록한다.
    // 3) 프리뷰 씬에서 Animator로 각 클립을 샘플링해 PNG로 저장한다 (열려 있는 씬은 건드리지 않는다).
    // 메뉴: Artti > Check ARTTI Clerk Animations
    public static class ArttiClerkAnimationCheck
    {
        private const string ModelPath = "Assets/_Project/Art/Characters/ARTTI_Clerk_v09/ARTTI_Clerk_UnityReady_v09.fbx";
        private static readonly string[] ClipNames = { "Idle_Smile", "Greeting", "Wave_Hand", "Smile", "AEIOU_Test" };
        private static readonly HashSet<string> LoopClips = new HashSet<string> { "Idle_Smile" };
        private const int Size = 720;

        [MenuItem("Artti/Check ARTTI Clerk Animations")]
        public static void Run()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "ArttiAnimationCheck"));
            Directory.CreateDirectory(outDir);
            var report = new StringBuilder();
            report.AppendLine("model: " + ModelPath);

            Avatar avatar = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (asset is Avatar a)
                {
                    avatar = a;
                }
            }
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError("[ArttiClerkAnimationCheck] model avatar missing or not a valid humanoid: " + ModelPath);
                return;
            }
            report.AppendLine("avatar: " + avatar.name + " isValid=" + avatar.isValid + " isHuman=" + avatar.isHuman);

            // 1) import settings of the animation FBXs
            string modelDir = Path.GetDirectoryName(ModelPath).Replace('\\', '/');
            string stem = Path.GetFileNameWithoutExtension(ModelPath);
            var clips = new List<AnimationClip>();
            foreach (string clipName in ClipNames)
            {
                string path = modelDir + "/" + stem + "@" + clipName + ".fbx";
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null)
                {
                    report.AppendLine("MISSING " + path);
                    continue;
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
                if (!imp.importAnimation)
                {
                    imp.importAnimation = true;
                    dirty = true;
                }
                if (!imp.importBlendShapes)
                {
                    imp.importBlendShapes = true;
                    dirty = true;
                }
                if (imp.materialImportMode != ModelImporterMaterialImportMode.None)
                {
                    imp.materialImportMode = ModelImporterMaterialImportMode.None;   // animation-only asset
                    dirty = true;
                }
                ModelImporterClipAnimation[] defaults = imp.defaultClipAnimations;
                if (defaults.Length > 0)
                {
                    var set = new ModelImporterClipAnimation[defaults.Length];
                    for (int i = 0; i < defaults.Length; i++)
                    {
                        ModelImporterClipAnimation c = defaults[i];
                        c.name = defaults.Length == 1 ? clipName : clipName + "_" + i;
                        c.loopTime = LoopClips.Contains(clipName);
                        c.loopPose = false;
                        c.lockRootRotation = true;
                        c.lockRootHeightY = true;
                        c.lockRootPositionXZ = true;
                        c.keepOriginalOrientation = true;
                        c.keepOriginalPositionY = true;
                        c.keepOriginalPositionXZ = true;
                        set[i] = c;
                    }
                    bool same = imp.clipAnimations.Length == set.Length;
                    if (same)
                    {
                        for (int i = 0; i < set.Length; i++)
                        {
                            if (imp.clipAnimations[i].name != set[i].name || imp.clipAnimations[i].loopTime != set[i].loopTime)
                            {
                                same = false;
                            }
                        }
                    }
                    if (!same)
                    {
                        imp.clipAnimations = set;
                        dirty = true;
                    }
                }
                if (dirty)
                {
                    imp.SaveAndReimport();
                }
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var clip = asset as AnimationClip;
                    if (clip != null && !clip.name.StartsWith("__preview__"))
                    {
                        clips.Add(clip);
                        int muscle = 0, blend = 0, other = 0;
                        var blendNames = new HashSet<string>();
                        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
                        {
                            if (b.type == typeof(Animator))
                            {
                                muscle++;
                            }
                            else if (b.propertyName.StartsWith("blendShape."))
                            {
                                blend++;
                                blendNames.Add(b.path + ":" + b.propertyName.Substring(11));
                            }
                            else
                            {
                                other++;
                            }
                        }
                        report.AppendLine("clip " + clip.name + ": length " + clip.length.ToString("F2") + " s, " + clip.frameRate + " fps, loop=" + clip.isLooping +
                                          ", humanMotion=" + clip.humanMotion + ", curves muscle/root " + muscle + ", blendShape " + blend + ", other " + other);
                        report.AppendLine("  blendShape curves: " + string.Join(", ", blendNames));
                    }
                }
            }

            // 2) sample every clip through an Animator in a preview scene
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var pru = new PreviewRenderUtility();
            var baked = new List<Mesh>();
            RuntimeAnimatorController controller = null;
            try
            {
                GameObject inst = pru.InstantiatePrefabInScene(prefab);
                var animator = inst.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = inst.AddComponent<Animator>();
                }
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var ctrl = new UnityEditor.Animations.AnimatorController();
                ctrl.name = "ArttiClerkAnimCheckController";
                ctrl.AddLayer("Base");
                var sm = ctrl.layers[0].stateMachine;
                foreach (AnimationClip clip in clips)
                {
                    UnityEditor.Animations.AnimatorState st = sm.AddState(clip.name);
                    st.motion = clip;
                }
                controller = ctrl;
                animator.runtimeAnimatorController = ctrl;

                var renderers = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Vector3 mouth = Vector3.zero;
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    if (smr.name == "Teeth_Upper")
                    {
                        mouth = smr.bounds.center;
                    }
                    smr.enabled = false;
                }
                Vector3 forward = Vector3.forward;
                Transform head = FindDeep(inst.transform, "Head");
                if (head != null)
                {
                    Vector3 f = mouth - head.position;
                    f.y = 0f;
                    if (f.sqrMagnitude > 1e-6f)
                    {
                        forward = f.normalized;
                    }
                }
                pru.camera.nearClipPlane = 0.02f;
                pru.camera.farClipPlane = 10f;
                pru.camera.backgroundColor = new Color(0.86f, 0.87f, 0.89f, 1f);
                pru.camera.clearFlags = CameraClearFlags.SolidColor;
                pru.lights[0].intensity = 1.4f;
                pru.lights[0].transform.rotation = Quaternion.LookRotation(-forward + Vector3.down * 0.6f + Vector3.right * 0.3f);
                pru.lights[1].intensity = 0.8f;
                pru.ambientColor = new Color(0.45f, 0.45f, 0.48f, 1f);

                Bounds all = renderers[0].bounds;
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    all.Encapsulate(smr.bounds);
                }
                Vector3 bodyTarget = all.center + Vector3.up * 0.1f;
                Vector3 faceTarget = mouth + Vector3.up * 0.03f;

                foreach (AnimationClip clip in clips)
                {
                    bool faceClip = clip.name == "Smile" || clip.name == "AEIOU_Test" || clip.name == "Idle_Smile";
                    int samples = clip.name == "AEIOU_Test" ? 5 : 6;
                    var times = new List<float>();
                    if (clip.name == "AEIOU_Test")
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            times.Add(i + 0.45f);
                        }
                    }
                    else if (clip.name == "Idle_Smile")
                    {
                        times.AddRange(new[] { 0f, 1.0f, 1.43f, 2.0f, 3.0f, 3.23f });
                    }
                    else
                    {
                        for (int i = 0; i < samples; i++)
                        {
                            times.Add(clip.length * i / (samples - 1));
                        }
                    }
                    var maxWeights = new Dictionary<string, float>();
                    foreach (float t in times)
                    {
                        animator.Play(clip.name, 0, Mathf.Clamp01(t / Mathf.Max(clip.length, 1e-4f)));
                        animator.Update(0f);
                        animator.Update(1f / 60f);
                        foreach (SkinnedMeshRenderer smr in renderers)
                        {
                            if (smr.name != "char1")
                            {
                                continue;
                            }
                            Mesh m = smr.sharedMesh;
                            for (int i = 0; i < m.blendShapeCount; i++)
                            {
                                string n = m.GetBlendShapeName(i);
                                float w = smr.GetBlendShapeWeight(i);
                                float prev;
                                maxWeights.TryGetValue(n, out prev);
                                if (w > prev)
                                {
                                    maxWeights[n] = w;
                                }
                            }
                        }
                        Vector3 target = faceClip && clip.name != "Idle_Smile" ? faceTarget : (clip.name == "Idle_Smile" && t > 1.4f && t < 1.5f ? faceTarget : bodyTarget);
                        float dist = target == faceTarget ? 0.5f : 2.4f;
                        pru.camera.fieldOfView = target == faceTarget ? 16f : 40f;
                        Vector3 dir = Quaternion.AngleAxis(20f, Vector3.up) * forward;
                        pru.camera.transform.position = target + dir * dist + Vector3.up * (target == faceTarget ? 0f : 0.15f);
                        pru.camera.transform.LookAt(target);
                        pru.BeginStaticPreview(new Rect(0, 0, Size, Size));
                        DrawBaked(pru, renderers, baked);
                        pru.Render(true);
                        Texture2D tex = pru.EndStaticPreview();
                        File.WriteAllBytes(Path.Combine(outDir, clip.name + "_t" + t.ToString("F2").Replace('.', '_') + ".png"), tex.EncodeToPNG());
                        // hand height for the wave / head height for the bow: quick numeric evidence
                        Transform rh = FindDeep(inst.transform, "RightHand");
                        Transform hd = FindDeep(inst.transform, "Head");
                        report.AppendLine("  " + clip.name + " t=" + t.ToString("F2") + ": RightHand y=" + (rh == null ? -1f : rh.position.y).ToString("F3") +
                                          " Head y=" + (hd == null ? -1f : hd.position.y).ToString("F3"));
                    }
                    var mw = new List<string>();
                    foreach (KeyValuePair<string, float> kv in maxWeights)
                    {
                        if (kv.Value > 0.5f)
                        {
                            mw.Add(kv.Key + "=" + kv.Value.ToString("F0"));
                        }
                    }
                    report.AppendLine("sampled " + clip.name + " (" + times.Count + " frames); blendShape max weights seen: " + string.Join(", ", mw));
                }
            }
            finally
            {
                foreach (Mesh m in baked)
                {
                    Object.DestroyImmediate(m);
                }
                if (controller != null)
                {
                    Object.DestroyImmediate(controller);
                }
                pru.Cleanup();
            }
            File.WriteAllText(Path.Combine(outDir, "unity_animation_report.txt"), report.ToString());
            Debug.Log("[ArttiClerkAnimationCheck] 완료 -> " + outDir + "\n" + report);
        }

        private static void DrawBaked(PreviewRenderUtility pru, SkinnedMeshRenderer[] renderers, List<Mesh> baked)
        {
            foreach (Mesh m in baked)
            {
                Object.DestroyImmediate(m);
            }
            baked.Clear();
            foreach (SkinnedMeshRenderer smr in renderers)
            {
                var m = new Mesh();
                smr.BakeMesh(m, true);
                baked.Add(m);
                Matrix4x4 matrix = smr.transform.localToWorldMatrix;
                Material[] mats = smr.sharedMaterials;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    Material mat = mats.Length > 0 ? mats[Mathf.Min(s, mats.Length - 1)] : null;
                    if (mat != null)
                    {
                        pru.DrawMesh(m, matrix, mat, s);
                    }
                }
            }
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name)
            {
                return t;
            }
            for (int i = 0; i < t.childCount; i++)
            {
                Transform r = FindDeep(t.GetChild(i), name);
                if (r != null)
                {
                    return r;
                }
            }
            return null;
        }
    }
}

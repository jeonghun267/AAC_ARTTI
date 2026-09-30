using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Artti.CharacterKit;
using Artti.Common;
using Artti.Editor;
using Artti.Training;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Artti.EditorTools
{
    // 대화 모드 남자 점원 검증. 씬을 열고 ClerkCast로 대화 모드 점원을 켠 뒤 확인한다. 씬은 저장하지 않는다.
    //  - 연결: TrainingSceneRoot.clerkCast, 남자 점원만 활성, Humanoid, 표정 키 10종, URP 재질, 립싱크 채널
    //  - 동작: ClerkView.PlayGreeting -> Wave -> Locomotion 복귀
    //  - 구도: 손 인사 동안 상체·손이 점원 카메라 안(가장자리 여백)에 들어오는지, 정확도 모드 점원과 머리 높이 비교
    //  - 캡처: 점원 RT + 태블릿(2560x1600) 전체 화면 PNG
    // 결과: <프로젝트>/Temp/ArttiMaleClerkCheck/
    public static class ArttiMaleClerkCheck
    {
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/ArttiMaleClerkCheck"));
        const int Frames = 540;          // 9초 @ 60fps
        const int GreetingFrame = 60;

        sealed class Framing
        {
            public float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            public int maxYFrame = -1;
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        [MenuItem("Artti/Verify Male Clerk (Conversation Mode)")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var report = new StringBuilder();
            bool pass = false;
            try
            {
                Require(!EditorApplication.isPlaying, "Stop Play Mode before the clerk check");
                EditorSceneManager.OpenScene(ScenePaths.TrainingConvenience, OpenSceneMode.Single);
                var stage = GameObject.Find(ArttiClerkSceneIntegration.StageName);
                Require(stage != null, "Missing " + ArttiClerkSceneIntegration.StageName);
                var cast = stage.GetComponent<ClerkCast>();
                Require(cast != null, "ClerkCast missing on the stage");
                var root = Object.FindFirstObjectByType<TrainingSceneRoot>(FindObjectsInactive.Include);
                Require(root != null && new SerializedObject(root).FindProperty("clerkCast").objectReferenceValue == cast,
                    "TrainingSceneRoot.clerkCast is not wired");
                var cam = stage.GetComponentsInChildren<Camera>(true).Single(c => c.name == ArttiClerkSceneIntegration.CameraName);
                Transform v4 = stage.transform.Find(ArttiClerkSceneIntegration.InstanceName);
                Transform maleT = stage.transform.Find(ArttiClerkSceneIntegration.MaleInstanceName);
                Require(v4 != null && maleT != null, "Both clerks must be on the stage");
                Require(v4.gameObject.activeSelf && !maleT.gameObject.activeSelf, "Saved scene default must be the accuracy clerk");
                report.AppendLine("Scene default: " + v4.name + " active, " + maleT.name + " inactive: PASS");

                // 정확도 모드 점원 기준 머리 높이 (같은 슬롯에서 크기 비교용)
                ClerkCast.Actor accuracy = cast.Activate(TrainingMode.Accuracy);
                Require(accuracy.root == v4.gameObject && v4.gameObject.activeInHierarchy && !maleT.gameObject.activeInHierarchy, "Accuracy mode did not select v4");
                var v4Animator = v4.GetComponent<Animator>();
                v4Animator.Rebind(); v4Animator.Play("Locomotion", 0, 0f); v4Animator.Update(1f / 60f);
                var v4Body = LargestSkin(v4.gameObject);
                Vector3 v4Head = HeadAndShoulder(v4Body, cam);
                CaptureClerk(v4.gameObject, cam, "accuracy_v4_idle", true);
                Vector2 v4Idle = IdleHeadMotion(v4Animator, FindBone(v4.gameObject, "Head"));

                // 대화 모드
                ClerkCast.Actor actor = cast.Activate(TrainingMode.Conversation);
                Require(actor.root == maleT.gameObject && maleT.gameObject.activeInHierarchy && !v4.gameObject.activeInHierarchy,
                    "Conversation mode did not select the male clerk alone");
                var animator = actor.root.GetComponent<Animator>();
                Require(animator.avatar != null && animator.avatar.isValid && !animator.applyRootMotion,
                    "Invalid avatar or root motion enabled");
                var clipNames = animator.runtimeAnimatorController.animationClips.Select(c => c.name).Distinct().OrderBy(n => n).ToArray();
                Require(clipNames.SequenceEqual(new[] { ArttiMaleClerkSetup.CalmIdleClipName, ArttiMaleClerkSetup.WaveClipName }.OrderBy(n => n)),
                    "Expected only the calm Idle and Wave clips, got " + string.Join(", ", clipNames));
                var lip = actor.root.GetComponent<ArttiAudioLipSync>();
                var face = actor.root.GetComponent<ArttiFaceDriver>();
                Require(lip != null && face != null && actor.view != null, "Male clerk is missing lip-sync / face / ClerkView");
                Require(actor.speech != null && lip.speechSource == actor.speech && actor.speech.transform == actor.root.transform,
                    "Male speech source does not feed its own lip-sync");
                Require(lip.voiceProfile != null, "Korean lip-sync profile missing");
                Require(actor.view.HasTrigger("Wave"), "Male controller has no Wave trigger");
                var skins = actor.root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var body = skins.Single(s => s.name == ArttiMaleClerkSetup.BodyMeshName);
                foreach (string shape in ArttiMaleClerkSetup.FaceShapes)
                    Require(body.sharedMesh.GetBlendShapeIndex(shape) >= 0, "Missing face shape " + shape);
                foreach (var skin in skins)
                    foreach (var material in skin.sharedMaterials)
                        Require(material != null && material.shader.name.StartsWith("Universal Render Pipeline/"), "Non-URP material on " + skin.name);
                report.AppendLine("Conversation mode -> " + maleT.name + " only; " + (animator.avatar.isHuman ? "Humanoid" : "Generic") +
                                  " avatar; clips " + string.Join("/", clipNames) + "; 10 face shapes; URP materials; speech=lip source: PASS");
                report.AppendLine($"Camera (conversation): local {cam.transform.localPosition}, FOV {cam.fieldOfView}");

                // 대기 중 두리번거림: 6초 동안 머리 방향·위치가 처음에서 가장 많이 벗어난 값
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArttiMaleClerkSetup.ModelPath);
                var originalIdle = AssetDatabase.LoadAllAssetsAtPath(ArttiMaleClerkSetup.IdlePath).OfType<AnimationClip>()
                    .First(c => c.name == ArttiMaleClerkSetup.IdleClipName);
                Vector2 maleOriginal = ArttiMaleClerkSetup.MeasureHeadMotion(originalIdle, model);
                Vector2 maleIdle = IdleHeadMotion(animator, FindBone(actor.root, "Head"));
                report.AppendLine($"Idle head motion (max from start, 6s): accuracy v4 {v4Idle.x:F1} deg / {v4Idle.y * 100f:F1} cm; " +
                                  $"male original Idle {maleOriginal.x:F1} deg / {maleOriginal.y * 100f:F1} cm; male calm Idle {maleIdle.x:F1} deg / {maleIdle.y * 100f:F1} cm");
                // 대기 1.5초 간격 스냅샷 (두리번거림 육안 확인용): 원본 Idle 클립 / 실제 컨트롤러(차분한 Idle)
                for (int i = 0; i < 4; i++)
                {
                    originalIdle.SampleAnimation(actor.root, i * 1.5f % originalIdle.length);
                    CaptureClerk(actor.root, cam, $"original_male_idle_t{i * 1.5f:0.0}s", false);
                }
                animator.Rebind(); animator.Play("Locomotion", 0, 0f); animator.Update(0f);
                for (int frame = 0; frame <= 270; frame++)
                {
                    animator.Update(1f / 60f);
                    if (frame % 90 == 0) CaptureClerk(actor.root, cam, $"conversation_male_idle_t{frame / 60f:0.0}s", false);
                }

                // 동작 + 구도 (1회차: 수치)
                animator.Rebind(); animator.Play("Locomotion", 0, 0f); animator.Update(0f);
                Transform leftHand = FindBone(actor.root, "LeftHand");
                Transform rightHand = FindBone(actor.root, "RightHand");
                Transform hips = FindBone(actor.root, "Hips");
                float restHand = Mathf.Max(leftHand.position.y, rightHand.position.y);
                Vector3 hipsStart = hips.position;
                // 단위 점검: 리그 스케일과 엉덩이 높이(정상이면 약 0.85~0.92m)를 기록한다.
                report.AppendLine($"Body lossyScale {body.transform.lossyScale}, rig '{hips.parent.name}' lossyScale {hips.parent.lossyScale}, hips start (stage local) {stage.transform.InverseTransformPoint(hipsStart)}");
                float maxLift = 0f, maxHipDrift = 0f;
                bool sawWave = false, returned = false;
                int waveStart = -1, waveEnd = -1, peakHandFrame = -1;
                var framing = new Framing();
                var baked = new Mesh();
                var vertices = new List<Vector3>();
                Vector3 maleHead = Vector3.zero;
                try
                {
                    for (int frame = 0; frame <= Frames; frame++)
                    {
                        if (frame == GreetingFrame) actor.view.PlayGreeting();
                        animator.Update(1f / 60f);
                        var state = animator.GetCurrentAnimatorStateInfo(0);
                        if (state.IsName("Wave") && waveStart < 0) waveStart = frame;
                        sawWave |= state.IsName("Wave");
                        if (sawWave && !returned && state.IsName("Locomotion") && !animator.IsInTransition(0)) { returned = true; waveEnd = frame; }
                        float lift = Mathf.Max(leftHand.position.y, rightHand.position.y) - restHand;
                        if (lift > maxLift) { maxLift = lift; peakHandFrame = frame; }
                        Vector3 drift = hips.position - hipsStart; drift.y = 0f;
                        maxHipDrift = Mathf.Max(maxHipDrift, drift.magnitude);
                        if (frame == 1) maleHead = HeadAndShoulder(body, cam);
                        if (frame % 6 == 0) Measure(body, cam, frame, framing, baked, vertices);
                        if (frame % 120 == 0)
                            report.AppendLine($"  frame {frame}: hips (stage local) {stage.transform.InverseTransformPoint(hips.position)}, state {(state.IsName("Wave") ? "Wave" : "Locomotion")}");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                }
                report.AppendLine($"Head top (idle): world v4 {v4Head.z:F3} m, male {maleHead.z:F3} m; viewport y v4 {v4Head.x:F3}, male {maleHead.x:F3}; shoulder width viewport: v4 {v4Head.y:F3}, male {maleHead.y:F3}");
                report.AppendLine($"PlayGreeting -> Wave frames {waveStart}..{waveEnd} ({(waveEnd - waveStart) / 60f:F2}s), returned to Locomotion={returned}; max hand lift {maxLift:F3} m at frame {peakHandFrame}; max hips drift {maxHipDrift * 1000f:F1} mm");
                report.AppendLine($"Upper-body framing over {Frames / 60f:F0}s: min x {framing.minX:F3}, max x {framing.maxX:F3}, max y {framing.maxY:F3} (frame {framing.maxYFrame})");
                // 2회차: 같은 흐름을 다시 돌려 대기 / 손 인사 최고점 / 말하는 입 모양을 찍는다
                animator.Rebind(); animator.Play("Locomotion", 0, 0f); animator.Update(0f);
                for (int frame = 0; frame <= peakHandFrame; frame++)
                {
                    if (frame == GreetingFrame) actor.view.PlayGreeting();
                    animator.Update(1f / 60f);
                    if (frame == 30) CaptureClerk(actor.root, cam, "conversation_male_idle", true);
                }
                CaptureClerk(actor.root, cam, "conversation_male_wave_peak", true);
                animator.Rebind(); animator.Play("Locomotion", 0, 0f); animator.Update(1f / 60f);
                face.Bind(); face.SetVisemes(80f, 0f, 0f, 0f, 0f); face.ApplyImmediate();
                Require(body.GetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex("A")) > 79f, "FaceDriver cannot drive the male visemes");
                CaptureClerk(actor.root, cam, "conversation_male_speaking_A", false);
                face.Clear();
                report.AppendLine("Viseme A=80 through ArttiFaceDriver: PASS");
                // 판정은 캡처 뒤에 해서 실패해도 그림을 남긴다.
                Require(sawWave && returned && maxLift > 0.3f, "PlayGreeting did not play Wave and return to Idle");
                Require(maleIdle.x <= 8f && maleIdle.y <= 0.02f, "Male idle head still turns/moves too much (limit 8 deg / 2 cm)");
                Require(framing.minX >= 0.01f && framing.maxX <= 0.99f && framing.maxY <= 0.98f, "Upper body / hand leaves the clerk camera margin");
                pass = true;
            }
            catch (Exception error)
            {
                report.AppendLine(error.ToString());
                Debug.LogException(error);
            }
            finally
            {
                report.AppendLine("RESULT: " + (pass ? "PASS" : "FAIL"));
                File.WriteAllText(Path.Combine(Output, "male_clerk_report.txt"), report.ToString());
                // 검증 중 바꾼 활성 상태·캔버스 설정은 저장하지 않고 버린다.
                EditorSceneManager.OpenScene(ScenePaths.TrainingConvenience, OpenSceneMode.Single);
            }
            Debug.Log("[ArttiMaleClerkCheck] " + (pass ? "PASS" : "FAIL") + " -> " + Output + "\n" + report);
        }

        // Animator로 Locomotion(대기)만 6초 재생하며 머리 방향(도)·위치(m)가 첫 프레임에서 가장 많이 벗어난 값
        static Vector2 IdleHeadMotion(Animator animator, Transform head)
        {
            animator.Rebind(); animator.Play("Locomotion", 0, 0f); animator.Update(0f);
            Quaternion q0 = head.rotation;
            Vector3 p0 = head.position;
            float angle = 0f, move = 0f;
            for (int frame = 0; frame < 360; frame++)
            {
                animator.Update(1f / 60f);
                angle = Mathf.Max(angle, Quaternion.Angle(q0, head.rotation));
                move = Mathf.Max(move, Vector3.Distance(p0, head.position));
            }
            return new Vector2(angle, move);
        }

        static Transform FindBone(GameObject actor, string name)
        {
            Transform bone = actor.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            Require(bone != null, "Bone not found: " + name);
            return bone;
        }

        static SkinnedMeshRenderer LargestSkin(GameObject actor) =>
            actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s => s.sharedMesh != null ? s.sharedMesh.vertexCount : 0).First();

        // x = 머리 꼭대기 viewport y, y = 어깨 높이 근처 몸통 폭(viewport), z = 머리 꼭대기 월드 높이(m, 무대 y=0)
        static Vector3 HeadAndShoulder(SkinnedMeshRenderer body, Camera cam)
        {
            var mesh = new Mesh();
            try
            {
                body.BakeMesh(mesh, false);
                var vertices = mesh.vertices;
                Matrix4x4 world = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
                float top = float.NegativeInfinity, topWorld = float.NegativeInfinity;
                foreach (var v in vertices) topWorld = Mathf.Max(topWorld, world.MultiplyPoint3x4(v).y);
                float shoulder = topWorld * 0.82f, minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
                foreach (var v in vertices)
                {
                    Vector3 p = world.MultiplyPoint3x4(v);
                    Vector3 vp = cam.WorldToViewportPoint(p);
                    top = Mathf.Max(top, vp.y);
                    if (Mathf.Abs(p.y - shoulder) < 0.02f) { minX = Mathf.Min(minX, vp.x); maxX = Mathf.Max(maxX, vp.x); }
                }
                return new Vector3(top, maxX - minX, topWorld);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        // 몸 높이 53% 위(상체·팔)의 정점만 본다. 하체는 카운터와 슬롯 아래로 잘려도 된다.
        static void Measure(SkinnedMeshRenderer body, Camera cam, int frame, Framing framing, Mesh baked, List<Vector3> vertices)
        {
            body.BakeMesh(baked, false);
            baked.GetVertices(vertices);
            Matrix4x4 world = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
            float minHeight = body.transform.root.position.y + 0.9f;
            foreach (var v in vertices)
            {
                Vector3 p = world.MultiplyPoint3x4(v);
                if (p.y < minHeight) continue;
                Vector3 vp = cam.WorldToViewportPoint(p);
                framing.minX = Mathf.Min(framing.minX, vp.x);
                framing.maxX = Mathf.Max(framing.maxX, vp.x);
                if (vp.y > framing.maxY) { framing.maxY = vp.y; framing.maxYFrame = frame; }
            }
        }

        // 현재 포즈를 굽힌 메시로 점원 RT를 그리고, 그 RT가 들어간 태블릿 화면을 찍는다.
        static void CaptureClerk(GameObject actor, Camera cam, string name, bool fullScreen)
        {
            var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var enabled = skins.Select(s => s.enabled).ToArray();
            var temp = new List<GameObject>();
            try
            {
                foreach (var skin in skins)
                {
                    var mesh = new Mesh();
                    skin.BakeMesh(mesh, false);
                    var go = new GameObject("Transient QA pose") { layer = skin.gameObject.layer };
                    go.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                    temp.Add(go);
                }
                var request = new RenderPipeline.StandardRequest { destination = cam.targetTexture };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                else cam.Render();
                SaveTexture(cam.targetTexture, Path.Combine(Output, name + "_rt.png"));
                if (fullScreen) DeviceLayoutCapture.CaptureActiveScene(2560, 1600, Path.Combine(Output, name + "_tab.png"));
            }
            finally
            {
                foreach (var go in temp)
                {
                    Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);
                    Object.DestroyImmediate(go);
                }
                for (int i = 0; i < skins.Length; i++) skins[i].enabled = enabled[i];
            }
        }

        static void SaveTexture(RenderTexture target, string path)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
            }
        }
    }
}

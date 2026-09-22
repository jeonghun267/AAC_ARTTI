using System.Collections.Generic;
using System.IO;
using System.Text;
using Artti.Editor;
using Artti.Training;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.EditorTools
{
    // 통합 검증: 편의점 훈련 씬을 열어 [ClerkStage] 의 Animator 를 에디터에서 실제로 스텝 실행한다.
    //  - Idle -> Greeting -> Idle, Idle -> Wave -> Idle, Smile(Face 레이어) 를 트리거로 실행하며 상태 이름/시간/손·머리 높이를 기록하고
    //    ClerkCamera 의 RenderTexture 를 프레임별 PNG 로 저장한다.
    //  - ArttiLipSyncController 로 A/E/I/O/U 를 각각 100 으로 놓고(Animator Idle 실행 중) 실제 BlendShape 가중치를 읽어 독립 동작을 확인한다.
    //  - 대시보드 배경 PNG 위에 RenderTexture 를 Clerk 슬롯 Rect 로 합성한 대화 거리 화면을 저장한다.
    // 씬은 저장하지 않고 검증 뒤 다시 열어 포즈 변경을 버린다.
    // 메뉴: Artti > Verify ARTTI Clerk Integration (v10)
    public static class ArttiClerkIntegrationCheck
    {
        private const string BackgroundPath = "Assets/_Project/Art/UI/Training/Dashboard/store_background.png";
        private const float Dt = 1f / 60f;
        private const float CaptureEvery = 0.4f;

        [MenuItem("Artti/Verify ARTTI Clerk Integration (v10)")]
        public static void Run()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "ArttiIntegrationCheck"));
            Directory.CreateDirectory(outDir);
            var report = new StringBuilder();
            bool pass = true;
            if (_referenceFrame != null)
            {
                Object.DestroyImmediate(_referenceFrame);
                _referenceFrame = null;
            }

            SceneBuilderUtils.OpenScene(ScenePaths.TrainingConvenience);
            var stage = GameObject.Find(ArttiClerkSceneIntegration.StageName);
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var sceneRoot = Object.FindFirstObjectByType<TrainingSceneRoot>(FindObjectsInactive.Include);
            if (stage == null || canvas == null)
            {
                Debug.LogError("[ArttiClerkIntegrationCheck] stage or canvas missing — run Artti/Integrate ARTTI Clerk v09 first");
                return;
            }
            Transform instT = stage.transform.Find(ArttiClerkSceneIntegration.InstanceName);
            if (instT != null && instT.GetComponent<Artti.CharacterKit.ArttiAudioLipSync>() != null)
            {
                ArttiStationaryIntegrationCheck.Run();
                return;
            }
            var animator = instT.GetComponent<Animator>();
            var clerkView = instT.GetComponent<ClerkView>();
            var lip = instT.GetComponent<ArttiLipSyncController>();
            var cam = stage.transform.Find(ArttiClerkSceneIntegration.CameraName).GetComponent<Camera>();
            RenderTexture rt = cam.targetTexture;
            Transform rawT = canvas.transform.Find(ArttiClerkSceneIntegration.RawImageName);
            Transform rasterT = canvas.transform.Find(ArttiClerkSceneIntegration.RasterName);
            var raw = rawT == null ? null : rawT.GetComponent<RawImage>();

            // 1) structure
            report.AppendLine("scene: " + ScenePaths.TrainingConvenience);
            report.AppendLine("stage at " + stage.transform.position.ToString("F2") + ", instance " + instT.name + ", animator controller " +
                              (animator.runtimeAnimatorController == null ? "none" : animator.runtimeAnimatorController.name) +
                              ", avatar " + (animator.avatar == null ? "none" : animator.avatar.name + " human=" + animator.avatar.isHuman));
            report.AppendLine("camera " + cam.name + " fov " + cam.fieldOfView + " local pos " + cam.transform.localPosition.ToString("F2") + " -> RT " +
                              (rt == null ? "none" : rt.width + "x" + rt.height));
            report.AppendLine("raw image " + (raw == null ? "MISSING" : raw.name + " rect pos " + raw.rectTransform.anchoredPosition + " size " + raw.rectTransform.sizeDelta +
                              " texture " + (raw.texture == null ? "none" : raw.texture.name)) + "; raster Clerk active=" + (rasterT == null ? "missing" : rasterT.gameObject.activeSelf.ToString()));
            SerializedProperty cvProp = sceneRoot == null ? null : new SerializedObject(sceneRoot).FindProperty("clerkView");
            report.AppendLine("TrainingSceneRoot.clerkView -> " + (cvProp == null || cvProp.objectReferenceValue == null ? "NOT WIRED" : ((ClerkView)cvProp.objectReferenceValue).name));
            var smrs = instT.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer smr in smrs)
            {
                if (smr.name == "char1")
                {
                    report.AppendLine("char1 blendShapes " + smr.sharedMesh.blendShapeCount + ", materials " + smr.sharedMaterials.Length +
                                      " [" + smr.sharedMaterials[0].name + ", " + smr.sharedMaterials[1].name + "]");
                    if (smr.sharedMesh.blendShapeCount != 11 || smr.sharedMaterials.Length != 2)
                    {
                        pass = false;
                    }
                }
            }
            var pars = new List<string>();
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                pars.Add(p.name + ":" + p.type);
            }
            report.AppendLine("animator parameters: " + string.Join(", ", pars));
            if (raw == null || rt == null || rasterT == null || rasterT.gameObject.activeSelf || cvProp == null || cvProp.objectReferenceValue == null)
            {
                pass = false;
            }

            // 2) animator runs (edit-mode stepping of the real state machine)
            animator.Rebind();
            animator.Update(0f);
            Transform hand = FindDeep(instT, "RightHand");
            Transform head = FindDeep(instT, "Head");
            float restHand = hand.position.y;
            float restHead = head.position.y;
            SkinnedMeshRenderer face = null;
            foreach (SkinnedMeshRenderer smr in smrs)
            {
                if (smr.name == "char1")
                {
                    face = smr;
                }
            }
            int smileIdx = face.sharedMesh.GetBlendShapeIndex("Smile");
            int blinkIdx = face.sharedMesh.GetBlendShapeIndex("Blink_L");
            // idle warm-up 1 s, capture
            float t = 0f;
            float maxBlink = 0f;
            for (int i = 0; i < 60; i++)
            {
                animator.Update(Dt);
                t += Dt;
                maxBlink = Mathf.Max(maxBlink, face.GetBlendShapeWeight(blinkIdx));
            }
            Texture2D idleShot = CaptureBaked(cam, smrs, Path.Combine(outDir, "10_idle_t1_00.png"));
            FrameDifference(idleShot);   // reference frame
            Object.DestroyImmediate(idleShot);
            report.AppendLine("Idle_Smile after 1.0 s: state " + StateName(animator, 0) + ", Smile " + face.GetBlendShapeWeight(smileIdx).ToString("F0") +
                              ", head y " + head.position.y.ToString("F3"));
            // a blink happens at 1.3 s in the clip: continue to 1.45 s
            for (int i = 0; i < 27; i++)
            {
                animator.Update(Dt);
                maxBlink = Mathf.Max(maxBlink, face.GetBlendShapeWeight(blinkIdx));
            }
            Texture2D blinkShot = CaptureBaked(cam, smrs, Path.Combine(outDir, "11_idle_blink_t1_45.png"));
            float blinkDiff = FrameDifference(blinkShot);
            Object.DestroyImmediate(blinkShot);
            report.AppendLine("Idle_Smile blink: max Blink_L seen up to 1.45 s = " + maxBlink.ToString("F0") + (maxBlink > 80f ? " PASS" : " FAIL") +
                              "; frame diff vs 1.0 s " + blinkDiff.ToString("F1"));
            if (maxBlink <= 80f)
            {
                pass = false;
            }

            pass &= RunOneShot(animator, clerkView.PlayGreeting, "Greeting", 0, cam, smrs, outDir, "20_greeting", report,
                               () => restHead - head.position.y, "head drop m", 0.02f);
            pass &= RunOneShot(animator, clerkView.PlayWave, "Wave_Hand", 0, cam, smrs, outDir, "30_wave", report,
                               () => hand.position.y - restHand, "hand lift m", 0.4f);
            pass &= RunOneShot(animator, clerkView.PlaySmile, "Smile", 1, cam, smrs, outDir, "40_smile", report,
                               () => face.GetBlendShapeWeight(smileIdx), "Smile weight", 95f);

            // 3) lip sync independence: animator keeps running in Idle while the controller sets each viseme
            report.AppendLine("lip sync: bound=" + lip.IsBound + ", renderers " + lip.TargetRendererCount);
            var visemes = new[] { ArttiViseme.A, ArttiViseme.E, ArttiViseme.I, ArttiViseme.O, ArttiViseme.U };
            var shapeNames = new[] { "A", "E", "I", "O", "U" };
            foreach (ArttiViseme v in visemes)
            {
                lip.SetOnly(v, 100f);
                animator.Update(Dt);        // Animator writes its curves first (no viseme curves in the v10 clips)
                lip.ApplyImmediate();       // then the lip controller (LateUpdate at runtime)
                var weights = new List<string>();
                bool ok = true;
                for (int k = 0; k < shapeNames.Length; k++)
                {
                    int idx = face.sharedMesh.GetBlendShapeIndex(shapeNames[k]);
                    float w = face.GetBlendShapeWeight(idx);
                    weights.Add(shapeNames[k] + "=" + w.ToString("F0"));
                    float expected = k == (int)v ? 100f : 0f;
                    if (Mathf.Abs(w - expected) > 1f)
                    {
                        ok = false;
                    }
                }
                Texture2D lipShot = CaptureBaked(cam, smrs, Path.Combine(outDir, "50_lipsync_" + shapeNames[(int)v] + ".png"));
                float lipDiff = FrameDifference(lipShot);
                Object.DestroyImmediate(lipShot);
                report.AppendLine("lip sync " + shapeNames[(int)v] + "=100 while " + StateName(animator, 0) + ": char1 " + string.Join(" ", weights) + (ok ? " PASS" : " FAIL") +
                                  "; frame diff vs idle " + lipDiff.ToString("F1"));
                pass &= ok;
            }
            lip.Clear();
            animator.Update(Dt);
            lip.ApplyImmediate();
            bool cleared = true;
            foreach (string n in shapeNames)
            {
                if (face.GetBlendShapeWeight(face.sharedMesh.GetBlendShapeIndex(n)) > 0.5f)
                {
                    cleared = false;
                }
            }
            report.AppendLine("lip sync Clear(): all visemes 0 " + (cleared ? "PASS" : "FAIL"));
            pass &= cleared;
            // viseme curves must be absent from the controller's body clips
            int visemeCurveClips = 0;
            var ac = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            if (ac != null)
            {
                foreach (AnimationClip c in ac.animationClips)
                {
                    foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(c))
                    {
                        if (b.propertyName == "blendShape.A" || b.propertyName == "blendShape.E" || b.propertyName == "blendShape.I" || b.propertyName == "blendShape.O" || b.propertyName == "blendShape.U")
                        {
                            visemeCurveClips++;
                            break;
                        }
                    }
                }
            }
            report.AppendLine("controller clips with viseme curves: " + visemeCurveClips + (visemeCurveClips == 0 ? " PASS" : " FAIL"));
            pass &= visemeCurveClips == 0;

            // 4) dashboard composite at the Clerk slot (conversation distance as the player sees it)
            animator.Rebind();
            animator.Update(0f);
            for (int i = 0; i < 30; i++)
            {
                animator.Update(Dt);
            }
            Texture2D frame = Capture(cam, rt, Path.Combine(outDir, "60_dashboard_character_rt.png"));
            if (raw != null)
            {
                Composite(frame, raw.rectTransform, Path.Combine(outDir, "61_dashboard_composite.png"), report);
            }
            Object.DestroyImmediate(frame);

            Texture2D bakedFrame = CaptureBaked(cam, smrs, Path.Combine(outDir, "62_dashboard_character_baked.png"));
            if (raw != null)
            {
                Composite(bakedFrame, raw.rectTransform, Path.Combine(outDir, "63_dashboard_composite_baked.png"), report);
            }
            if (_pru != null)
            {
                foreach (Mesh m in _baked)
                {
                    Object.DestroyImmediate(m);
                }
                _baked.Clear();
                _pru.Cleanup();
                _pru = null;
            }
            report.AppendLine(pass ? "RESULT: PASS" : "RESULT: FAIL");
            File.WriteAllText(Path.Combine(outDir, "integration_report.txt"), report.ToString());
            Debug.Log("[ArttiClerkIntegrationCheck] " + (pass ? "PASS" : "FAIL") + " -> " + outDir + "\n" + report);

            // discard the posed scene state
            SceneBuilderUtils.OpenScene(ScenePaths.TrainingConvenience);
        }

        private static bool RunOneShot(Animator animator, System.Action fire, string stateName, int layer, Camera cam, SkinnedMeshRenderer[] smrs, string outDir,
                                       string prefix, StringBuilder report, System.Func<float> metric, string metricName, float threshold)
        {
            string home = layer == 0 ? "Idle_Smile" : "Neutral";
            fire();
            float t = 0f;
            float nextCapture = 0f;
            bool entered = false;
            bool returned = false;
            float enterTime = -1f;
            float returnTime = -1f;
            float maxMetric = float.MinValue;
            var trace = new List<string>();
            var diffs = new List<string>();
            string last = "";
            while (t < 8f)
            {
                animator.Update(Dt);
                t += Dt;
                string cur = StateName(animator, layer);
                if (cur != last)
                {
                    trace.Add(t.ToString("F2") + "s:" + cur);
                    last = cur;
                }
                maxMetric = Mathf.Max(maxMetric, metric());
                if (!entered && cur == stateName)
                {
                    entered = true;
                    enterTime = t;
                }
                if (entered && cur == home && !animator.IsInTransition(layer))
                {
                    returned = true;
                    returnTime = t;
                }
                if (t >= nextCapture)
                {
                    Texture2D shot = CaptureBaked(cam, smrs, Path.Combine(outDir, prefix + "_t" + t.ToString("F1").Replace('.', '_') + ".png"));
                    diffs.Add(t.ToString("F1") + "s:" + FrameDifference(shot).ToString("F1"));
                    Object.DestroyImmediate(shot);
                    nextCapture += CaptureEvery;
                }
                if (returned)
                {
                    break;
                }
            }
            bool ok = entered && returned && maxMetric >= threshold;
            report.AppendLine(stateName + ": entered " + (entered ? "at " + enterTime.ToString("F2") + " s" : "NO") + ", back to " + home + " " +
                              (returned ? "at " + returnTime.ToString("F2") + " s" : "NO") + ", max " + metricName + " " + maxMetric.ToString("F3") +
                              " (>= " + threshold + ") -> " + (ok ? "PASS" : "FAIL") + "; trace " + string.Join(" > ", trace) +
                              "; frame diff vs idle " + string.Join(" ", diffs));
            return ok;
        }

        private static string StateName(Animator animator, int layer)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
            var ac = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            if (ac != null && layer < ac.layers.Length)
            {
                foreach (UnityEditor.Animations.ChildAnimatorState cs in ac.layers[layer].stateMachine.states)
                {
                    if (info.shortNameHash == cs.state.nameHash)
                    {
                        return cs.state.name;
                    }
                }
            }
            return info.shortNameHash.ToString();
        }

        // URP: Camera.Render() does not draw into the target texture from an editor script; a render request does.
        private static void RenderNow(Camera cam, RenderTexture rt)
        {
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest();
            request.destination = rt;
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request))
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
            }
            else
            {
                cam.Render();
            }
        }

        private static Texture2D _referenceFrame;
        private static PreviewRenderUtility _pru;
        private static readonly List<Mesh> _baked = new List<Mesh>();

        // Frames for the animation / lip-sync evidence: the scene camera's view re-rendered from CPU-baked skinned meshes
        // (SkinnedMeshRenderer.BakeMesh reflects the current bones and blend shape weights synchronously, which the
        // editor's own skinning path does not do inside one menu call).
        private static Texture2D CaptureBaked(Camera clerkCam, SkinnedMeshRenderer[] smrs, string path)
        {
            if (_pru == null)
            {
                _pru = new PreviewRenderUtility();
                _pru.camera.backgroundColor = new Color(0.86f, 0.87f, 0.89f, 1f);
                _pru.camera.clearFlags = CameraClearFlags.SolidColor;
                _pru.lights[0].intensity = 1.3f;
                _pru.lights[0].transform.rotation = Quaternion.Euler(38f, 205f, 0f);
                _pru.lights[1].intensity = 0.7f;
                _pru.ambientColor = new Color(0.45f, 0.45f, 0.48f, 1f);
            }
            Camera c = _pru.camera;
            c.fieldOfView = clerkCam.fieldOfView;
            c.nearClipPlane = clerkCam.nearClipPlane;
            c.farClipPlane = clerkCam.farClipPlane;
            c.transform.position = clerkCam.transform.position;
            c.transform.rotation = clerkCam.transform.rotation;
            RenderTexture rt = clerkCam.targetTexture;
            _pru.BeginStaticPreview(new Rect(0, 0, rt.width, rt.height));
            foreach (Mesh m in _baked)
            {
                Object.DestroyImmediate(m);
            }
            _baked.Clear();
            foreach (SkinnedMeshRenderer smr in smrs)
            {
                var m = new Mesh();
                smr.BakeMesh(m, true);
                _baked.Add(m);
                Material[] mats = smr.sharedMaterials;
                for (int sub = 0; sub < m.subMeshCount; sub++)
                {
                    Material mat = mats.Length > 0 ? mats[Mathf.Min(sub, mats.Length - 1)] : null;
                    if (mat != null)
                    {
                        _pru.DrawMesh(m, smr.transform.localToWorldMatrix, mat, sub);
                    }
                }
            }
            _pru.Render(true);
            Texture2D tex = _pru.EndStaticPreview();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            return tex;
        }

        // mean absolute RGB difference (0..255) against the first captured frame: proof that frames actually change
        private static float FrameDifference(Texture2D tex)
        {
            if (_referenceFrame == null)
            {
                _referenceFrame = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                _referenceFrame.SetPixels32(tex.GetPixels32());
                _referenceFrame.Apply();
                return 0f;
            }
            Color32[] a = _referenceFrame.GetPixels32();
            Color32[] b = tex.GetPixels32();
            double sum = 0;
            int step = 7;
            int n = 0;
            for (int i = 0; i < a.Length; i += step)
            {
                sum += Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
                n++;
            }
            return (float)(sum / (3.0 * n));
        }

        private static Texture2D Capture(Camera cam, RenderTexture rt, string path)
        {
            RenderNow(cam, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            // PNG viewers show transparent areas oddly; flatten onto light grey for the frame files
            var flat = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            Color[] px = tex.GetPixels();
            var bg = new Color(0.86f, 0.87f, 0.89f, 1f);
            for (int i = 0; i < px.Length; i++)
            {
                float a = px[i].a;
                px[i] = new Color(px[i].r * a + bg.r * (1f - a), px[i].g * a + bg.g * (1f - a), px[i].b * a + bg.b * (1f - a), 1f);
            }
            flat.SetPixels(px);
            flat.Apply();
            File.WriteAllBytes(path, flat.EncodeToPNG());
            Object.DestroyImmediate(flat);
            return tex;
        }

        // 1920x1080 canvas reference: background stretched, character frame alpha-blended at the RawImage rect (top-centre anchored)
        private static void Composite(Texture2D frame, RectTransform slot, string path, StringBuilder report)
        {
            const int W = 1920, H = 1080;
            var bgTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            string fullBg = Path.GetFullPath(BackgroundPath);
            if (File.Exists(fullBg))
            {
                bgTex.LoadImage(File.ReadAllBytes(fullBg));
            }
            var outTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    px[y * W + x] = bgTex.GetPixelBilinear((x + 0.5f) / W, (y + 0.5f) / H);
                }
            }
            float sw = slot.sizeDelta.x, sh = slot.sizeDelta.y;
            float left = W * slot.anchorMin.x + slot.anchoredPosition.x - sw * slot.pivot.x;
            float top = H * (1f - slot.anchorMin.y) - slot.anchoredPosition.y - sh * (1f - slot.pivot.y);
            int x0 = Mathf.RoundToInt(left), y0 = Mathf.RoundToInt(top);
            int w = Mathf.RoundToInt(sw), h = Mathf.RoundToInt(sh);
            for (int yy = 0; yy < h; yy++)
            {
                int cy = H - 1 - (y0 + yy);   // Texture2D origin is bottom-left
                if (cy < 0 || cy >= H)
                {
                    continue;
                }
                for (int xx = 0; xx < w; xx++)
                {
                    int cx = x0 + xx;
                    if (cx < 0 || cx >= W)
                    {
                        continue;
                    }
                    Color c = frame.GetPixelBilinear((xx + 0.5f) / w, 1f - (yy + 0.5f) / h);
                    Color d = px[cy * W + cx];
                    px[cy * W + cx] = new Color(c.r * c.a + d.r * (1f - c.a), c.g * c.a + d.g * (1f - c.a), c.b * c.a + d.b * (1f - c.a), 1f);
                }
            }
            outTex.SetPixels(px);
            outTex.Apply();
            File.WriteAllBytes(path, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);
            Object.DestroyImmediate(bgTex);
            report.AppendLine("dashboard composite: character slot x " + x0 + ".." + (x0 + w) + ", y " + y0 + ".." + (y0 + h) + " of 1920x1080 -> " + Path.GetFileName(path));
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

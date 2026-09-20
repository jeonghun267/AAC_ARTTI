using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Artti.EditorTools
{
    // ARTTI 점원 NPC FBX의 BlendShape 동작 확인용 에디터 도구.
    // 메뉴 실행 시: 모델을 Humanoid로 재임포트하고, 프리뷰 씬에서 각 BlendShape를 100으로 올린 화면을 PNG로 저장한다.
    // 열려 있는 씬은 건드리지 않는다 (PreviewRenderUtility + SkinnedMeshRenderer.BakeMesh 사용).
    public static class ArttiBlendShapeCheck
    {
        private const string ModelPath = "Assets/_Project/Art/Characters/ARTTI_Clerk_v09/ARTTI_Clerk_UnityReady_v09.fbx";
        private static readonly string[] KeyNames = { "JawOpen", "MouthClose", "Smile", "Frown", "A", "E", "I", "O", "U" };
        private const int Size = 900;

        [MenuItem("Artti/Check ARTTI Clerk BlendShapes")]
        public static void Run()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "ArttiBlendShapeCheck"));
            Directory.CreateDirectory(outDir);
            var report = new StringBuilder();

            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[ArttiBlendShapeCheck] ModelImporter not found: " + ModelPath);
                return;
            }
            bool needReimport = false;
            if (!importer.importBlendShapes)
            {
                importer.importBlendShapes = true;
                needReimport = true;
            }
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                needReimport = true;
            }
            if (needReimport)
            {
                importer.SaveAndReimport();
            }

            Avatar avatar = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (asset is Avatar a)
                {
                    avatar = a;
                }
            }
            report.AppendLine("model: " + ModelPath);
            report.AppendLine("importBlendShapes: " + importer.importBlendShapes + ", animationType: " + importer.animationType);
            report.AppendLine("avatar: " + (avatar == null ? "none" : avatar.name + " isValid=" + avatar.isValid + " isHuman=" + avatar.isHuman));
            if (avatar != null && avatar.isHuman)
            {
                int mapped = 0;
                foreach (HumanBone hb in avatar.humanDescription.human)
                {
                    mapped++;
                    report.AppendLine("  human bone " + hb.humanName + " <- " + hb.boneName);
                }
                report.AppendLine("mapped human bones: " + mapped);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (prefab == null)
            {
                Debug.LogError("[ArttiBlendShapeCheck] prefab not loaded: " + ModelPath);
                return;
            }

            var pru = new PreviewRenderUtility();
            var baked = new List<Mesh>();
            try
            {
                GameObject inst = pru.InstantiatePrefabInScene(prefab);
                var renderers = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var indexByRenderer = new Dictionary<SkinnedMeshRenderer, Dictionary<string, int>>();
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    Mesh mesh = smr.sharedMesh;
                    var map = new Dictionary<string, int>();
                    var names = new List<string>();
                    var deltas = new Vector3[mesh.vertexCount];
                    var deltaInfo = new List<string>();
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    {
                        string n = mesh.GetBlendShapeName(i);
                        map[n] = i;
                        names.Add(n);
                        mesh.GetBlendShapeFrameVertices(i, mesh.GetBlendShapeFrameCount(i) - 1, deltas, null, null);
                        float mx = 0f;
                        int moved = 0;
                        for (int v = 0; v < deltas.Length; v++)
                        {
                            float m = deltas[v].magnitude;
                            if (m > 1e-6f)
                            {
                                moved++;
                            }
                            if (m > mx)
                            {
                                mx = m;
                            }
                        }
                        deltaInfo.Add(n + ":" + moved + "v/" + (mx * 1000f).ToString("F1") + "mm");
                    }
                    indexByRenderer[smr] = map;
                    report.AppendLine("renderer " + smr.name + ": verts " + mesh.vertexCount + ", tris " + (mesh.triangles.Length / 3) +
                                      ", bones " + smr.bones.Length + ", blendShapes " + mesh.blendShapeCount + " [" + string.Join(", ", names) + "]" +
                                      ", materials [" + JoinMaterials(smr) + "]");
                    report.AppendLine("  blendshape deltas (moved vertices / max): " + string.Join(", ", deltaInfo));
                }

                // camera: look at the upper teeth (mouth) from the front
                Vector3 mouth = Vector3.zero;
                Transform head = FindDeep(inst.transform, "Head");
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    if (smr.name == "Teeth_Upper")
                    {
                        mouth = smr.bounds.center;
                    }
                }
                Vector3 forward = Vector3.forward;
                if (head != null)
                {
                    Vector3 f = mouth - head.position;
                    f.y = 0f;
                    if (f.sqrMagnitude > 1e-6f)
                    {
                        forward = f.normalized;
                    }
                }
                report.AppendLine("mouth (Teeth_Upper bounds centre): " + mouth.ToString("F4") + ", head bone: " + (head == null ? "none" : head.position.ToString("F4")) + ", forward: " + forward.ToString("F3"));

                pru.camera.fieldOfView = 12f;
                pru.camera.nearClipPlane = 0.02f;
                pru.camera.farClipPlane = 10f;
                pru.camera.backgroundColor = new Color(0.86f, 0.87f, 0.89f, 1f);
                pru.camera.clearFlags = CameraClearFlags.SolidColor;
                pru.lights[0].intensity = 1.4f;
                pru.lights[0].transform.rotation = Quaternion.LookRotation(-forward + Vector3.down * 0.6f + Vector3.right * 0.3f);
                pru.lights[1].intensity = 0.8f;
                pru.ambientColor = new Color(0.45f, 0.45f, 0.48f, 1f);

                // The scene instance is only used for information; rendering uses baked meshes so blend shapes are guaranteed to apply.
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    smr.enabled = false;
                }

                var shots = new List<KeyValuePair<string, Dictionary<string, float>>>();
                shots.Add(new KeyValuePair<string, Dictionary<string, float>>("00_closed", new Dictionary<string, float>()));
                shots.Add(new KeyValuePair<string, Dictionary<string, float>>("01_JawOpen_50", new Dictionary<string, float> { { "JawOpen", 50f } }));
                foreach (string k in KeyNames)
                {
                    shots.Add(new KeyValuePair<string, Dictionary<string, float>>("02_" + k + "_100", new Dictionary<string, float> { { k, 100f } }));
                }
                foreach (KeyValuePair<string, Dictionary<string, float>> shot in shots)
                {
                    foreach (SkinnedMeshRenderer smr in renderers)
                    {
                        Dictionary<string, int> map = indexByRenderer[smr];
                        foreach (KeyValuePair<string, int> kv in map)
                        {
                            float w = 0f;
                            shot.Value.TryGetValue(kv.Key, out w);
                            smr.SetBlendShapeWeight(kv.Value, w);
                        }
                    }
                    foreach (string view in new[] { "front", "45", "side" })
                    {
                        float yaw = view == "front" ? 0f : (view == "45" ? 45f : 90f);
                        Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * forward;
                        pru.camera.transform.position = mouth + dir * 0.42f + Vector3.up * 0.012f;
                        pru.camera.transform.LookAt(mouth + Vector3.up * 0.012f);
                        pru.BeginStaticPreview(new Rect(0, 0, Size, Size));
                        DrawBaked(pru, renderers, baked);
                        pru.Render(true);
                        Texture2D tex = pru.EndStaticPreview();
                        File.WriteAllBytes(Path.Combine(outDir, shot.Key + "_" + view + ".png"), tex.EncodeToPNG());
                    }
                    report.AppendLine("rendered " + shot.Key + " (front, 45, side)");
                }
                // head shots (whole head, closed mouth): front / 45 / side, for the hairline / temple / ear / collar check
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    Dictionary<string, int> map = indexByRenderer[smr];
                    foreach (KeyValuePair<string, int> kv in map)
                    {
                        smr.SetBlendShapeWeight(kv.Value, 0f);
                    }
                }
                Vector3 headCentre = mouth + Vector3.up * 0.045f;
                pru.camera.fieldOfView = 26f;
                foreach (string view in new[] { "front", "left45", "right45" })
                {
                    float yaw = view == "front" ? 0f : (view == "left45" ? -45f : 45f);
                    Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * forward;
                    pru.camera.transform.position = headCentre + dir * 0.55f;
                    pru.camera.transform.LookAt(headCentre);
                    pru.BeginStaticPreview(new Rect(0, 0, Size, Size));
                    DrawBaked(pru, renderers, baked);
                    pru.Render(true);
                    Texture2D texHead = pru.EndStaticPreview();
                    File.WriteAllBytes(Path.Combine(outDir, "04_head_" + view + ".png"), texHead.EncodeToPNG());
                }
                report.AppendLine("rendered 04_head (front, left45, right45)");

                // 05: play-distance checks (16:9 game-view framing). "far" = whole character as seen in a shop scene
                // (~3.5 m), "talk" = conversation framing, upper body (~1.4 m). Front / left45 / right45 each.
                Bounds allB = renderers[0].bounds;
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    allB.Encapsulate(smr.bounds);
                }
                Vector3 chest = mouth + Vector3.down * 0.30f;
                var playShots = new[]
                {
                    new { name = "far", target = allB.center, dist = 3.5f, fov = 35f },
                    new { name = "talk", target = chest, dist = 1.4f, fov = 35f },
                };
                foreach (var ps in playShots)
                {
                    foreach (string view in new[] { "front", "left45", "right45" })
                    {
                        float yaw = view == "front" ? 0f : (view == "left45" ? -45f : 45f);
                        Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * forward;
                        pru.camera.fieldOfView = ps.fov;
                        pru.camera.transform.position = ps.target + dir * ps.dist + Vector3.up * 0.05f;
                        pru.camera.transform.LookAt(ps.target);
                        pru.BeginStaticPreview(new Rect(0, 0, 1280, 720));
                        DrawBaked(pru, renderers, baked);
                        pru.Render(true);
                        Texture2D texPlay = pru.EndStaticPreview();
                        File.WriteAllBytes(Path.Combine(outDir, "05_play_" + ps.name + "_" + view + ".png"), texPlay.EncodeToPNG());
                    }
                }
                report.AppendLine("rendered 05_play (far 3.5 m / talk 1.4 m; front, left45, right45; 1280x720)");

                // 06: every blend shape of char1 at 0 / 50 / 100 (front, face framing) for the reuse review
                SkinnedMeshRenderer faceSmr = null;
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    if (smr.name == "char1")
                    {
                        faceSmr = smr;
                    }
                }
                if (faceSmr != null)
                {
                    Vector3 faceCentre = mouth + Vector3.up * 0.03f;
                    var allNames = new List<string>(indexByRenderer[faceSmr].Keys);
                    foreach (string shapeName in allNames)
                    {
                        foreach (float level in new[] { 0f, 50f, 100f })
                        {
                            foreach (SkinnedMeshRenderer smr in renderers)
                            {
                                Dictionary<string, int> map = indexByRenderer[smr];
                                foreach (KeyValuePair<string, int> kv in map)
                                {
                                    smr.SetBlendShapeWeight(kv.Value, kv.Key == shapeName ? level : 0f);
                                }
                            }
                            pru.camera.fieldOfView = 16f;
                            pru.camera.transform.position = faceCentre + forward * 0.5f;
                            pru.camera.transform.LookAt(faceCentre);
                            pru.BeginStaticPreview(new Rect(0, 0, 600, 600));
                            DrawBaked(pru, renderers, baked);
                            pru.Render(true);
                            Texture2D texBs = pru.EndStaticPreview();
                            File.WriteAllBytes(Path.Combine(outDir, "06_bs_" + shapeName + "_" + level.ToString("F0") + ".png"), texBs.EncodeToPNG());
                        }
                    }
                    report.AppendLine("rendered 06_bs (" + string.Join(", ", allNames) + ") at 0 / 50 / 100");
                }
                // full body sanity shot
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    Dictionary<string, int> map = indexByRenderer[smr];
                    foreach (KeyValuePair<string, int> kv in map)
                    {
                        smr.SetBlendShapeWeight(kv.Value, 0f);
                    }
                }
                Bounds all = renderers[0].bounds;
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    all.Encapsulate(smr.bounds);
                }
                pru.camera.fieldOfView = 30f;
                pru.camera.transform.position = all.center + forward * 3.6f;
                pru.camera.transform.LookAt(all.center);
                pru.BeginStaticPreview(new Rect(0, 0, Size, Size));
                DrawBaked(pru, renderers, baked);
                pru.Render(true);
                Texture2D body = pru.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(outDir, "03_fullbody.png"), body.EncodeToPNG());
                report.AppendLine("bounds: centre " + all.center.ToString("F3") + " size " + all.size.ToString("F3"));
            }
            finally
            {
                foreach (Mesh m in baked)
                {
                    Object.DestroyImmediate(m);
                }
                pru.Cleanup();
            }
            File.WriteAllText(Path.Combine(outDir, "unity_blendshape_report.txt"), report.ToString());
            Debug.Log("[ArttiBlendShapeCheck] 완료 -> " + outDir + "\n" + report);
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

        private static string JoinMaterials(Renderer r)
        {
            var names = new List<string>();
            foreach (Material m in r.sharedMaterials)
            {
                names.Add(m == null ? "null" : m.name + (m.mainTexture == null ? "(no tex)" : "(" + m.mainTexture.name + ")") +
                          " _Cull=" + (m.HasProperty("_Cull") ? m.GetFloat("_Cull").ToString("F0") : "n/a") +
                          " path=" + AssetDatabase.GetAssetPath(m));
            }
            return string.Join(", ", names);
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

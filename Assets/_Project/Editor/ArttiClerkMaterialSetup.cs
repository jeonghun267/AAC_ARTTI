using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

namespace Artti.EditorTools
{
    // ARTTI 점원 NPC FBX의 임베디드 머티리얼을 외부 .mat 에셋으로 추출하고 URP Lit의 Render Face를 설정하는 에디터 도구.
    // FBX에 임베디드된 머티리얼은 읽기 전용이라 Inspector에서 Render Face를 바꿀 수 없으므로
    // AssetDatabase.ExtractAsset으로 FBX 옆 "Materials" 폴더에 추출한다. ExtractAsset은 ModelImporter의
    // externalObjects 리맵을 자동으로 추가하므로, 재임포트 후 렌더러 슬롯이 추출된 에셋을 참조한다.
    // 머리카락이 오픈 셸(한 겹 면)이라 ARTTI_Face는 Render Face = Both(_Cull = 0), Material_1은 Front(_Cull = 2)를 유지한다.
    // 메뉴: Artti > Setup ARTTI Clerk Materials
    public static class ArttiClerkMaterialSetup
    {
        // 대상 모델 경로. 모델 버전이 바뀌면 이 상수만 수정한다.
        private const string ModelPath = "Assets/_Project/Art/Characters/ARTTI_Clerk_v09/ARTTI_Clerk_UnityReady_v09.fbx";
        private const string MaterialsFolderName = "Materials";
        private const string DoubleSidedMaterialName = "ARTTI_Face";
        private const string SingleSidedMaterialName = "Material_1";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        // URP BaseShaderGUI.RenderFace 는 float 프로퍼티 "_Cull"에 저장된다: Both = 0, Back = 1, Front = 2 (Lit.shader 기본값 2).
        // URP BaseShaderGUI.SurfaceType 은 float 프로퍼티 "_Surface"에 저장된다: Opaque = 0, Transparent = 1.
        private const string CullProperty = "_Cull";
        private const string SurfaceProperty = "_Surface";
        private const float CullBoth = 0f;
        private const float CullFront = 2f;
        private const float FaceCull = CullFront;
        private const float SurfaceOpaque = 0f;

        [MenuItem("Artti/Setup ARTTI Clerk Materials")]
        public static void Run()
        {
            var log = new StringBuilder();
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[ArttiClerkMaterialSetup] ModelImporter not found: " + ModelPath);
                return;
            }
            log.AppendLine("model: " + ModelPath);
            log.AppendLine("importer materialImportMode=" + importer.materialImportMode + ", materialLocation=" + importer.materialLocation);

            string materialsDir = EnsureMaterialsFolder(log);
            if (materialsDir == null)
            {
                return;
            }

            Dictionary<string, Material> external = ExtractOrRemap(importer, materialsDir, log);
            if (external.Count == 0)
            {
                Debug.LogError("[ArttiClerkMaterialSetup] no external materials after extraction. FBX: " + ModelPath + "\n" + log);
                return;
            }

            // 검증 결과: URP Lit은 뒷면의 노멀을 뒤집지 않아 한 겹 머리카락 가닥의 뒷면이 회색 덩어리로 렌더링된다.
            // 따라서 ARTTI_Face도 Front를 유지한다(양면이 필요하면 FaceCull 을 CullBoth 로 바꾼다).
            Material face;
            if (external.TryGetValue(DoubleSidedMaterialName, out face))
            {
                ConfigureRenderFace(face, FaceCull, log);
            }
            else
            {
                Debug.LogWarning("[ArttiClerkMaterialSetup] material not found: " + DoubleSidedMaterialName);
            }

            Material body;
            if (external.TryGetValue(SingleSidedMaterialName, out body))
            {
                ConfigureRenderFace(body, CullFront, log);
            }
            else
            {
                Debug.LogWarning("[ArttiClerkMaterialSetup] material not found: " + SingleSidedMaterialName);
            }

            foreach (KeyValuePair<string, Material> kv in external)
            {
                if (kv.Key != DoubleSidedMaterialName && kv.Key != SingleSidedMaterialName)
                {
                    log.AppendLine(kv.Key + ": left unchanged (_Cull=" + CullValueString(kv.Value) + ")");
                }
            }

            AssetDatabase.SaveAssets();
            Verify(log);
            Debug.Log("[ArttiClerkMaterialSetup] 완료\n" + log);
        }

        private static string EnsureMaterialsFolder(StringBuilder log)
        {
            string modelDir = Path.GetDirectoryName(ModelPath).Replace('\\', '/');
            string materialsDir = modelDir + "/" + MaterialsFolderName;
            if (!AssetDatabase.IsValidFolder(materialsDir))
            {
                string guid = AssetDatabase.CreateFolder(modelDir, MaterialsFolderName);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogError("[ArttiClerkMaterialSetup] failed to create folder: " + materialsDir);
                    return null;
                }
                log.AppendLine("created folder: " + materialsDir);
            }
            return materialsDir;
        }

        // 임베디드 머티리얼을 추출하거나, 이미 추출된 .mat이 있으면 리맵만 다시 연결한다. 반환: 머티리얼 이름 -> 외부 에셋.
        private static Dictionary<string, Material> ExtractOrRemap(ModelImporter importer, string materialsDir, StringBuilder log)
        {
            var alreadyMapped = new HashSet<string>();
            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> kv in importer.GetExternalObjectMap())
            {
                if (kv.Key.type == typeof(Material) && kv.Value != null)
                {
                    alreadyMapped.Add(kv.Key.name);
                    log.AppendLine("already remapped: " + kv.Key.name + " -> " + AssetDatabase.GetAssetPath(kv.Value));
                }
            }

            bool importerDirty = false;
            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                var embedded = obj as Material;
                if (embedded == null || alreadyMapped.Contains(embedded.name))
                {
                    continue;
                }
                if (AssetDatabase.GetAssetPath(embedded) != ModelPath)
                {
                    continue;
                }

                string destPath = materialsDir + "/" + embedded.name + ".mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(destPath);
                if (existing != null)
                {
                    // .mat 파일은 남아 있는데 리맵이 끊긴 경우: 추출 없이 리맵만 복구
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embedded), existing);
                    log.AppendLine("remapped to existing asset: " + embedded.name + " -> " + destPath);
                    importerDirty = true;
                    continue;
                }

                string error = AssetDatabase.ExtractAsset(embedded, destPath);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError("[ArttiClerkMaterialSetup] ExtractAsset failed for " + embedded.name + ": " + error);
                    continue;
                }
                log.AppendLine("extracted: " + embedded.name + " -> " + destPath);
                importerDirty = true;
            }

            if (importerDirty)
            {
                AssetDatabase.WriteImportSettingsIfDirty(ModelPath);
                AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
                log.AppendLine("reimported: " + ModelPath);
            }

            var result = new Dictionary<string, Material>();
            var refreshed = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (refreshed == null)
            {
                return result;
            }
            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> kv in refreshed.GetExternalObjectMap())
            {
                var m = kv.Value as Material;
                if (kv.Key.type == typeof(Material) && m != null)
                {
                    result[kv.Key.name] = m;
                }
            }
            return result;
        }

        private static void ConfigureRenderFace(Material mat, float cull, StringBuilder log)
        {
            string shaderName = mat.shader == null ? "null" : mat.shader.name;
            if (shaderName != UrpLitShaderName)
            {
                log.AppendLine("warning: " + mat.name + " shader is [" + shaderName + "], expected [" + UrpLitShaderName + "]");
            }
            if (!mat.HasProperty(CullProperty))
            {
                Debug.LogWarning("[ArttiClerkMaterialSetup] " + mat.name + " has no " + CullProperty + " property; skipped");
                return;
            }

            float cullBefore = mat.GetFloat(CullProperty);
            string surfaceBefore = mat.HasProperty(SurfaceProperty) ? mat.GetFloat(SurfaceProperty).ToString("F0") : "n/a";
            if (mat.HasProperty(SurfaceProperty))
            {
                mat.SetFloat(SurfaceProperty, SurfaceOpaque);
            }
            mat.SetFloat(CullProperty, cull);

            // URP Lit 인스펙터(LitShader.ValidateMaterial)와 같은 후처리: 블렌드 상태, 렌더 큐, 키워드, doubleSidedGI 동기화.
            BaseShaderGUI.SetMaterialKeywords(mat, LitGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(mat);

            log.AppendLine(mat.name + " (" + AssetDatabase.GetAssetPath(mat) + "): _Cull " + cullBefore.ToString("F0") + " -> " +
                           mat.GetFloat(CullProperty).ToString("F0") + " (" + RenderFaceName(cull) + "), _Surface " + surfaceBefore + " -> " +
                           (mat.HasProperty(SurfaceProperty) ? mat.GetFloat(SurfaceProperty).ToString("F0") : "n/a") +
                           ", doubleSidedGI=" + mat.doubleSidedGI + ", renderQueue=" + mat.renderQueue);
        }

        // 최종 확인: FBX 프리팹의 렌더러 슬롯이 외부 .mat 에셋을 참조하는지와 각 _Cull 값을 기록한다.
        private static void Verify(StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (prefab == null)
            {
                log.AppendLine("verify: prefab not loaded");
                return;
            }
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    string path = m == null ? "null" : AssetDatabase.GetAssetPath(m);
                    bool isExternal = m != null && path != ModelPath;
                    log.AppendLine("verify renderer " + r.name + "[" + i + "]: " + (m == null ? "null" : m.name) +
                                   " path=" + path + " external=" + isExternal + " _Cull=" + CullValueString(m));
                }
            }
        }

        private static string CullValueString(Material m)
        {
            if (m == null || !m.HasProperty(CullProperty))
            {
                return "n/a";
            }
            return m.GetFloat(CullProperty).ToString("F0");
        }

        private static string RenderFaceName(float cull)
        {
            if (cull == 0f)
            {
                return "Both";
            }
            if (cull == 1f)
            {
                return "Back";
            }
            return "Front";
        }
    }
}

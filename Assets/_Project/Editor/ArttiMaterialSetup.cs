using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Artti.CharacterKit.Editor
{
    [InitializeOnLoad]
    public static class ArttiMaterialSetup
    {
        private const string Root = "Assets/_Project/Art/Characters/ARTTI_Stationary_v4/";
        static ArttiMaterialSetup() { EditorApplication.delayCall += Configure; }
        [MenuItem("ARTTI/Adapt material to active render pipeline")]
        public static void Configure()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Artti_Original.mat");
            if (!material) return;
            bool urp = GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal");
            var shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            if (!shader) return;
            material.shader = shader;
            var color = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/Artti_BaseColor.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/Artti_Normal.png");
            var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/Artti_MetallicSmoothness.png");
            material.SetTexture(urp ? "_BaseMap" : "_MainTex", color);
            material.SetColor(urp ? "_BaseColor" : "_Color", Color.white);
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 1);
            material.SetTexture("_MetallicGlossMap", metallic);
            material.SetFloat(urp ? "_Smoothness" : "_GlossMapScale", 1);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICGLOSSMAP");
            if (urp) material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            foreach(string name in new[]{"Artti_Teeth","Artti_Tongue","Artti_OralCavity"}) {
                var oral=AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");if(!oral)continue;
                Color tint=name=="Artti_Teeth"?new Color(.86f,.84f,.79f):name=="Artti_Tongue"?new Color(.5f,.18f,.22f):new Color(.08f,.01f,.025f);
                oral.shader=shader;oral.SetColor(urp?"_BaseColor":"_Color",tint);oral.SetFloat(urp?"_Smoothness":"_Glossiness",.15f);EditorUtility.SetDirty(oral);
            }
            AssetDatabase.SaveAssets();
        }
    }
}

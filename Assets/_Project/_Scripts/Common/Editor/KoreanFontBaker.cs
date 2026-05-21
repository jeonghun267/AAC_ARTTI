#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Artti.Common.EditorTools
{
    public static class KoreanFontBaker
    {
        const string RegularPath = "Assets/Fonts/NotoSansKR-Regular SDF.asset";
        const string MediumPath  = "Assets/Fonts/NotoSansKR-Medium 1 SDF.asset";

        [MenuItem("Tools/Artti/Pre-bake Korean Glyphs (NotoSansKR-Regular SDF)")]
        public static void BakeRegular() => Bake(RegularPath);

        [MenuItem("Tools/Artti/Pre-bake Korean Glyphs (NotoSansKR-Medium SDF)")]
        public static void BakeMedium() => Bake(MediumPath);

        static void Bake(string assetPath)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (font == null)
            {
                Debug.LogError($"[KoreanFontBaker] Font asset not found: {assetPath}");
                EditorUtility.DisplayDialog("Korean Font Baker", $"Not found:\n{assetPath}", "OK");
                return;
            }

            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic &&
                font.atlasPopulationMode != AtlasPopulationMode.DynamicOS)
            {
                Debug.LogWarning($"[KoreanFontBaker] {font.name} is not Dynamic atlas; switching to Dynamic for baking.");
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            }

            // Ensure atlas is large enough to hold Hangul Syllables (11172 chars).
            // 4096x4096 with 32px sampling fits comfortably; multi-atlas fallback handles overflow.
            if (font.atlasWidth < 2048 || font.atlasHeight < 2048)
            {
                Debug.LogWarning($"[KoreanFontBaker] Atlas {font.atlasWidth}x{font.atlasHeight} is small for Korean. Consider regenerating via Font Asset Creator at 2048x2048+.");
            }

            var ranges = new List<(uint start, uint end, string label)>
            {
                (0x0020u, 0x007Eu, "ASCII"),
                (0x00A0u, 0x00FFu, "Latin-1 Supplement"),
                (0x2010u, 0x205Eu, "General Punctuation"),
                (0x3000u, 0x303Fu, "CJK Symbols and Punctuation"),
                (0x3130u, 0x318Fu, "Hangul Compatibility Jamo"),
                (0xAC00u, 0xD7A3u, "Hangul Syllables"),
                (0xFF00u, 0xFFEFu, "Halfwidth and Fullwidth Forms"),
            };

            int totalRequested = 0;
            int totalAdded = 0;
            int totalMissing = 0;

            foreach (var r in ranges)
            {
                var unicodes = new List<uint>();
                for (uint u = r.start; u <= r.end; u++) unicodes.Add(u);

                font.TryAddCharacters(unicodes.ToArray(), out uint[] missing);
                int added = unicodes.Count - (missing?.Length ?? 0);

                totalRequested += unicodes.Count;
                totalAdded += added;
                totalMissing += missing?.Length ?? 0;

                Debug.Log($"[KoreanFontBaker] {r.label}: +{added}/{unicodes.Count}");
            }

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary = $"Baked into: {font.name}\nAdded: {totalAdded}/{totalRequested}\nMissing: {totalMissing}\nAtlas: {font.atlasWidth}x{font.atlasHeight}";
            Debug.Log("[KoreanFontBaker] " + summary.Replace('\n', ' '));
            EditorUtility.DisplayDialog("Korean Font Baker", summary, "OK");
        }
    }
}
#endif

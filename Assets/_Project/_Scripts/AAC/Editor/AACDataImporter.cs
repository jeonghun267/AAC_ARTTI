#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Artti.AAC.EditorTools
{
    public static class AACDataImporter
    {
        const string SourceImagesRoot = @"C:\Users\admin_14\Documents\AAC_Images";

        const string DataFolder        = "Assets/_Project/_Data/AAC";
        const string SymbolsAssetDir   = "Assets/_Project/_Data/AAC/Symbols";
        const string SymbolImagesDir   = "Assets/_Project/_Data/AAC/Symbols/Images";
        const string PhrasesAssetDir   = "Assets/_Project/_Data/AAC/Phrases";
        const string CardsAssetDir     = "Assets/_Project/_Data/AAC/Cards";

        [MenuItem("Tools/AAC/Import Seed Data (Symbols + Phrases + Cards)")]
        public static void ImportAll()
        {
            try
            {
                EnsureFolder(SymbolsAssetDir);
                EnsureFolder(SymbolImagesDir);
                EnsureFolder(PhrasesAssetDir);
                EnsureFolder(CardsAssetDir);

                // Phase 1: Symbols — must run synchronously so sprite sub-assets are available.
                int symCount = ImportSymbols();
                AssetDatabase.SaveAssets();

                // Phase 2: Phrases — pure ScriptableObject, safe to batch.
                AssetDatabase.StartAssetEditing();
                int phrCount = ImportPhrases();
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();

                // Phase 3: Cards — needs symbol/phrase lookups, so symbols/phrases must be saved already.
                AssetDatabase.StartAssetEditing();
                int cardCount = ImportCards();
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Phase 4: AACDatabase에 List 채우기
                int dbSym, dbPhr, dbCard;
                PopulateDatabaseInternal(out dbSym, out dbPhr, out dbCard);

                Debug.Log($"[AAC] Import complete. Symbols={symCount}, Phrases={phrCount}, Cards={cardCount}, DB={dbSym}/{dbPhr}/{dbCard}");
                EditorUtility.DisplayDialog(
                    "AAC Import",
                    $"Done!\n\nSymbols: {symCount}\nPhrases: {phrCount}\nCards: {cardCount}\n\nAACDatabase 채움: {dbSym}/{dbPhr}/{dbCard}",
                    "OK");
            }
            catch (Exception e)
            {
                try { AssetDatabase.StopAssetEditing(); } catch { }
                Debug.LogError($"[AAC] Import failed: {e}");
                EditorUtility.DisplayDialog("AAC Import Failed", e.Message, "OK");
            }
        }

        [MenuItem("Tools/AAC/Re-link Card Symbol/Phrase References")]
        public static void RelinkCards()
        {
            int n = ImportCards();
            AssetDatabase.SaveAssets();
            Debug.Log($"[AAC] Re-linked {n} cards.");
        }

        [MenuItem("Tools/AAC/Re-link Symbol Sprites")]
        public static void RelinkSymbolSprites()
        {
            int linked = 0, missing = 0;
            var guids = AssetDatabase.FindAssets("t:AACSymbol", new[] { SymbolsAssetDir });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sym = AssetDatabase.LoadAssetAtPath<AACSymbol>(path);
                if (sym == null || string.IsNullOrEmpty(sym.id)) continue;

                var pngPath = $"{SymbolImagesDir}/{sym.id}.png";
                if (!File.Exists(pngPath))
                {
                    Debug.LogWarning($"[AAC] PNG missing for {sym.id}: {pngPath}");
                    missing++;
                    continue;
                }

                ConfigureSpriteImporter(pngPath);
                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
                if (sprite != null)
                {
                    sym.sprite = sprite;
                    EditorUtility.SetDirty(sym);
                    linked++;
                }
                else
                {
                    Debug.LogWarning($"[AAC] Sprite still null after re-import for {sym.id}");
                    missing++;
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AAC] Sprite re-link complete: {linked} linked, {missing} missing");
            EditorUtility.DisplayDialog("Re-link Symbol Sprites",
                $"Linked: {linked}\nMissing: {missing}", "OK");
        }

        [MenuItem("Tools/AAC/Populate AACDatabase")]
        public static void PopulateDatabase()
        {
            int s, p, c;
            PopulateDatabaseInternal(out s, out p, out c);
            EditorUtility.DisplayDialog("AACDatabase",
                $"채움 완료\nSymbols: {s}\nPhrases: {p}\nCards: {c}", "OK");
        }

        const string DatabaseAssetPath = "Assets/_Project/_Data/AAC/AACDatabase.asset";

        static void PopulateDatabaseInternal(out int symCount, out int phrCount, out int cardCount)
        {
            symCount = phrCount = cardCount = 0;
            var db = AssetDatabase.LoadAssetAtPath<AACDatabase>(DatabaseAssetPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<AACDatabase>();
                AssetDatabase.CreateAsset(db, DatabaseAssetPath);
            }
            db.symbols.Clear();
            db.phrases.Clear();
            db.cards.Clear();

            foreach (var guid in AssetDatabase.FindAssets("t:AACSymbol", new[] { SymbolsAssetDir }))
            {
                var a = AssetDatabase.LoadAssetAtPath<AACSymbol>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) { db.symbols.Add(a); symCount++; }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AACPhrase", new[] { PhrasesAssetDir }))
            {
                var a = AssetDatabase.LoadAssetAtPath<AACPhrase>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) { db.phrases.Add(a); phrCount++; }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AACCard", new[] { CardsAssetDir }))
            {
                var a = AssetDatabase.LoadAssetAtPath<AACCard>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) { db.cards.Add(a); cardCount++; }
            }

            db.InvalidateIndex();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        // ---------------- Symbols ----------------
        static int ImportSymbols()
        {
            var path = Path.Combine(DataFolder, "symbols.json");
            var json = File.ReadAllText(path);
            var bundle = JsonUtility.FromJson<SymbolsJson>(json);
            if (bundle == null || bundle.symbols == null) return 0;

            int n = 0;
            foreach (var seed in bundle.symbols)
            {
                if (string.IsNullOrEmpty(seed.id)) continue;

                // 1. Copy PNG (skip if already exists)
                var sourcePng = Path.Combine(SourceImagesRoot, seed.png_source.Replace('/', Path.DirectorySeparatorChar));
                var assetPng  = $"{SymbolImagesDir}/{seed.id}.png";

                if (!File.Exists(sourcePng))
                {
                    Debug.LogWarning($"[AAC] Source PNG missing for {seed.id}: {sourcePng}");
                    continue;
                }
                if (!File.Exists(assetPng))
                {
                    File.Copy(sourcePng, assetPng, false);
                    AssetDatabase.ImportAsset(assetPng, ImportAssetOptions.ForceSynchronousImport);
                }

                ConfigureSpriteImporter(assetPng);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPng);

                // 2. Create or update Symbol asset
                var assetPath = $"{SymbolsAssetDir}/{seed.id}.asset";
                var sym = AssetDatabase.LoadAssetAtPath<AACSymbol>(assetPath);
                if (sym == null)
                {
                    sym = ScriptableObject.CreateInstance<AACSymbol>();
                    AssetDatabase.CreateAsset(sym, assetPath);
                }
                sym.id = seed.id;
                sym.sprite = sprite;
                sym.semanticTags = seed.semantic_tags ?? new string[0];
                sym.description = seed.description ?? "";
                EditorUtility.SetDirty(sym);
                n++;
            }
            return n;
        }

        static void ConfigureSpriteImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.alphaIsTransparency != true) { importer.alphaIsTransparency = true; changed = true; }
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        // ---------------- Phrases ----------------
        static int ImportPhrases()
        {
            var path = Path.Combine(DataFolder, "phrases.json");
            var json = File.ReadAllText(path);
            var bundle = JsonUtility.FromJson<PhrasesJson>(json);
            if (bundle == null || bundle.phrases == null) return 0;

            int n = 0;
            foreach (var seed in bundle.phrases)
            {
                if (string.IsNullOrEmpty(seed.id)) continue;

                var assetPath = $"{PhrasesAssetDir}/{seed.id}.asset";
                var p = AssetDatabase.LoadAssetAtPath<AACPhrase>(assetPath);
                if (p == null)
                {
                    p = ScriptableObject.CreateInstance<AACPhrase>();
                    AssetDatabase.CreateAsset(p, assetPath);
                }
                p.id = seed.id;
                p.text = seed.text;
                p.ttsText = seed.tts_text;
                p.register = ParseEnum(seed.register, AACRegister.Polite);
                p.lengthClass = ParseEnum(seed.length_class, AACLengthClass.Short);
                EditorUtility.SetDirty(p);
                n++;
            }
            return n;
        }

        // ---------------- Cards ----------------
        static int ImportCards()
        {
            var path = Path.Combine(DataFolder, "cards.json");
            var json = File.ReadAllText(path);
            var bundle = JsonUtility.FromJson<CardsJson>(json);
            if (bundle == null || bundle.cards == null) return 0;

            // Build symbol/phrase lookup
            var symMap = new Dictionary<string, AACSymbol>();
            foreach (var guid in AssetDatabase.FindAssets("t:AACSymbol", new[] { SymbolsAssetDir }))
            {
                var a = AssetDatabase.LoadAssetAtPath<AACSymbol>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null && !string.IsNullOrEmpty(a.id)) symMap[a.id] = a;
            }
            var phrMap = new Dictionary<string, AACPhrase>();
            foreach (var guid in AssetDatabase.FindAssets("t:AACPhrase", new[] { PhrasesAssetDir }))
            {
                var a = AssetDatabase.LoadAssetAtPath<AACPhrase>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null && !string.IsNullOrEmpty(a.id)) phrMap[a.id] = a;
            }

            int n = 0;
            foreach (var seed in bundle.cards)
            {
                if (string.IsNullOrEmpty(seed.id)) continue;

                var assetPath = $"{CardsAssetDir}/{seed.id}.asset";
                var c = AssetDatabase.LoadAssetAtPath<AACCard>(assetPath);
                if (c == null)
                {
                    c = ScriptableObject.CreateInstance<AACCard>();
                    AssetDatabase.CreateAsset(c, assetPath);
                }
                c.id = seed.id;
                if (!symMap.TryGetValue(seed.symbol, out c.symbol))
                    Debug.LogWarning($"[AAC] Card {seed.id} references unknown symbol '{seed.symbol}'");
                if (!phrMap.TryGetValue(seed.phrase, out c.phrase))
                    Debug.LogWarning($"[AAC] Card {seed.id} references unknown phrase '{seed.phrase}'");
                c.applicableScenarios  = seed.applicable_scenarios  ?? new string[0];
                c.applicableObjectives = seed.applicable_objectives ?? new string[0];
                c.tags = seed.tags ?? new string[0];
                c.branchId = seed.branch_id ?? "";
                c.priorityHint = seed.priority_hint;
                EditorUtility.SetDirty(c);
                n++;
            }
            return n;
        }

        // ---------------- Helpers ----------------
        static void EnsureFolder(string folderAssetPath)
        {
            if (AssetDatabase.IsValidFolder(folderAssetPath)) return;
            var parent = Path.GetDirectoryName(folderAssetPath).Replace('\\', '/');
            var leaf = Path.GetFileName(folderAssetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static T ParseEnum<T>(string s, T fallback) where T : struct
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            return Enum.TryParse<T>(s, true, out var v) ? v : fallback;
        }

        // ---- JSON shape classes (JsonUtility-compatible) ----
#pragma warning disable IDE1006 // snake_case to match JSON keys
        [Serializable] class SymbolsJson { public SymbolSeed[] symbols; }
        [Serializable] class SymbolSeed
        {
            public string id;
            public string png_source;
            public string[] semantic_tags;
            public string description;
        }

        [Serializable] class PhrasesJson { public PhraseSeed[] phrases; }
        [Serializable] class PhraseSeed
        {
            public string id;
            public string text;
            public string tts_text;
            public string register;
            public string length_class;
        }

        [Serializable] class CardsJson { public CardSeed[] cards; }
        [Serializable] class CardSeed
        {
            public string id;
            public string symbol;
            public string phrase;
            public string[] applicable_scenarios;
            public string[] applicable_objectives;
            public string[] tags;
            public string branch_id;
            public int priority_hint;
        }
#pragma warning restore IDE1006
    }
}
#endif

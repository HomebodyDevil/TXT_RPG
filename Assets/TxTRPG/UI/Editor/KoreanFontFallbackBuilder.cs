using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace TxTRPG.UI.Editor
{
    public static class KoreanFontFallbackBuilder
    {
        public const string SourceFontPath = "Assets/TxTRPG/UI/Fonts/Korean/NotoSansKR-Variable.ttf";
        public const string FontAssetPath = "Assets/TxTRPG/UI/Fonts/Korean/NotoSansKR-Fallback.asset";
        public const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("Tools/TxT RPG/UI/Fonts/Apply Korean Font Fallback")]
        public static void Apply()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath)
                ?? throw new InvalidOperationException($"Korean source font was not imported at '{SourceFontPath}'.");
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fallback == null)
            {
                fallback = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                fallback.name = "NotoSansKR Fallback";
                AssetDatabase.CreateAsset(fallback, FontAssetPath);
                foreach (var atlas in fallback.atlasTextures.Where(asset => asset != null && !AssetDatabase.Contains(asset)))
                    AssetDatabase.AddObjectToAsset(atlas, fallback);
                if (fallback.material != null && !AssetDatabase.Contains(fallback.material))
                    AssetDatabase.AddObjectToAsset(fallback.material, fallback);
            }
            fallback.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fallback.isMultiAtlasTexturesEnabled = true;
            EditorUtility.SetDirty(fallback);

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath)
                ?? throw new InvalidOperationException($"TMP Settings was not found at '{TmpSettingsPath}'.");
            var serialized = new SerializedObject(settings);
            var list = serialized.FindProperty("m_fallbackFontAssets")
                ?? throw new InvalidOperationException("TMP Settings has no fallback font list.");
            var alreadyPresent = false;
            for (var i = 0; i < list.arraySize; i++)
                alreadyPresent |= list.GetArrayElementAtIndex(i).objectReferenceValue == fallback;
            if (!alreadyPresent)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = fallback;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Noto Sans KR was added as a TMP global fallback. LiberationSans remains the default font.");
        }
    }
}
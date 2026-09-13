using System.Linq;
using NUnit.Framework;
using TMPro;
using TxTRPG.UI.Editor;
using UnityEditor;

namespace TxTRPG.UI.Tests
{
    public sealed class KoreanFontFallbackTests
    {
        [Test]
        public void TmpSettings_PreservesEnglishDefaultAndIncludesKoreanFallback()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(KoreanFontFallbackBuilder.TmpSettingsPath);
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontFallbackBuilder.FontAssetPath);
            Assert.That(settings, Is.Not.Null);
            Assert.That(fallback, Is.Not.Null);
            var serialized = new SerializedObject(settings);
            Assert.That(serialized.FindProperty("m_defaultFontAsset").objectReferenceValue.name, Does.Contain("LiberationSans"));
            var fallbacks = serialized.FindProperty("m_fallbackFontAssets");
            Assert.That(Enumerable.Range(0, fallbacks.arraySize).Any(i => fallbacks.GetArrayElementAtIndex(i).objectReferenceValue == fallback), Is.True);
            Assert.That(fallback.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
            Assert.That(fallback.isMultiAtlasTexturesEnabled, Is.True);
        }

        [TestCase("주사위 결과: D6 → 5")]
        [TestCase("가방 · 설정")]
        [TestCase("가나다라마바사")]
        [TestCase("ㄱㄴㄷ ㅏㅑㅓ")]
        public void KoreanFallback_CanAddRequiredCharacters(string text)
        {
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontFallbackBuilder.FontAssetPath);
            Assert.That(fallback.TryAddCharacters(text, out var missing), Is.True, $"Missing characters: {missing}");
            Assert.That(missing, Is.Empty);
        }
    }
}
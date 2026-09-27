using TMPro;
using UnityEngine;

namespace TxTRPG.UI.Exploration
{
    public enum ExplorationCardTextRole { Title, Description, Status }

    [DisallowMultipleComponent]
    public sealed class ExplorationCardTextEffectController : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private ExplorationCardTextRole role;
        [SerializeField] private ExplorationCardPresentationProfile profile;
        private TMP_MeshInfo[] original;
        private string cachedText;
        private bool effectsActive;

        private void OnEnable() { ExplorationCardPlayerPreferences.Changed += Refresh; if (text != null) text.OnPreRenderText += OnTextRebuilt; Refresh(); }
        private void OnDisable() { ExplorationCardPlayerPreferences.Changed -= Refresh; if (text != null) text.OnPreRenderText -= OnTextRebuilt; Restore(); }
        private void LateUpdate()
        {
            var settings = GetSettings();
            ApplyBaseColor();
            var enabledNow = ExplorationCardPlayerPreferences.AllowsMotion && (settings.waveEnabled || settings.gradientEnabled);
            if (enabledNow != effectsActive) { effectsActive = enabledNow; if (text != null) text.SetVerticesDirty(); }
            if (!enabledNow) { Restore(); return; }
            if (text == null) return;
            if (text.havePropertiesChanged || cachedText != text.text || !CacheMatches()) { original = null; return; }
            var time = Time.unscaledTime;
            for (var material = 0; material < text.textInfo.meshInfo.Length; material++)
            {
                var meshInfo = text.textInfo.meshInfo[material];
                if (material >= original.Length) continue;
                System.Array.Copy(original[material].vertices, meshInfo.vertices, original[material].vertices.Length);
                System.Array.Copy(original[material].colors32, meshInfo.colors32, original[material].colors32.Length);
            }
            for (var i = 0; i < text.textInfo.characterCount; i++)
            {
                var character = text.textInfo.characterInfo[i]; if (!character.isVisible) continue;
                var mesh = text.textInfo.meshInfo[character.materialReferenceIndex]; var vertex = character.vertexIndex;
                var offset = settings.waveEnabled ? Mathf.Sin(time * settings.waveSpeed + i * settings.characterPhase) * settings.waveAmplitude : 0f;
                var blend = settings.gradientEnabled ? Mathf.PingPong(time * settings.gradientSpeed + i * .08f * settings.gradientDirection, 1f) : 0f;
                for (var corner = 0; corner < 4; corner++)
                {
                    mesh.vertices[vertex + corner].y += offset;
                    if (settings.gradientEnabled) mesh.colors32[vertex + corner] = Color.Lerp(settings.gradientStart, settings.gradientEnd, blend);
                }
            }
            for (var material = 0; material < text.textInfo.meshInfo.Length; material++) { var info = text.textInfo.meshInfo[material]; info.mesh.vertices = info.vertices; info.mesh.colors32 = info.colors32; text.UpdateGeometry(info.mesh, material); }
        }
        public void Refresh() { Restore(); ApplyBaseColor(); if (text != null) text.SetVerticesDirty(); }
        private void ApplyBaseColor()
        {
            if (text == null) return;
            var color = profile == null ? Color.white : role == ExplorationCardTextRole.Title ? profile.titleColor : role == ExplorationCardTextRole.Description ? profile.descriptionColor : profile.statusColor;
            if (text.color == color) return;
            original = null;
            text.color = color;
        }
        private ExplorationCardTextEffectSettings GetSettings() => profile == null ? default : role == ExplorationCardTextRole.Title ? profile.titleEffect : role == ExplorationCardTextRole.Description ? profile.descriptionEffect : profile.statusEffect;
        private void OnTextRebuilt(TMP_TextInfo info)
        {
            var settings = GetSettings();
            original = ExplorationCardPlayerPreferences.AllowsMotion && (settings.waveEnabled || settings.gradientEnabled) ? info.CopyMeshInfoVertexData() : null;
            cachedText = text.text;
        }
        private bool CacheMatches()
        {
            if (original == null || text == null || text.textInfo.meshInfo.Length != original.Length) return false;
            for (var i = 0; i < original.Length; i++)
                if (text.textInfo.meshInfo[i].vertices.Length != original[i].vertices.Length || text.textInfo.meshInfo[i].colors32.Length != original[i].colors32.Length) return false;
            return true;
        }
        private void Restore()
        {
            if (text == null || original == null) return;
            if (cachedText != text.text || text.havePropertiesChanged || !CacheMatches()) { original = null; text.SetVerticesDirty(); return; }
            for (var material = 0; material < text.textInfo.meshInfo.Length && material < original.Length; material++) { var info = text.textInfo.meshInfo[material]; System.Array.Copy(original[material].vertices, info.vertices, original[material].vertices.Length); System.Array.Copy(original[material].colors32, info.colors32, original[material].colors32.Length); info.mesh.vertices = info.vertices; info.mesh.colors32 = info.colors32; text.UpdateGeometry(info.mesh, material); }
            original = null;
        }
#if UNITY_EDITOR
        public void ConfigureForEditor(TMP_Text target, ExplorationCardTextRole targetRole, ExplorationCardPresentationProfile targetProfile) { text = target; role = targetRole; profile = targetProfile; ApplyBaseColor(); }
#endif
    }
}

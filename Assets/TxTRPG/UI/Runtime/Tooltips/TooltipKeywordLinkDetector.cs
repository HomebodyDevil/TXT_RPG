using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TxTRPG.UI.Tooltips
{
    // On a tooltip Text block: finds the keyword link under the pointer when the pointer moves (never every frame) and
    // reports only changes. The keyword's character area (the union of its character rectangles, several lines included)
    // is the anchor of the keyword tooltip. Touch pointers are ignored (mouse-only scope).
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TooltipKeywordLinkDetector : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text text;

        // Raised with the new link ID ("kw:id") or an empty string when the pointer leaves the link.
        public event Action<TooltipKeywordLinkDetector, string> LinkChanged;

        public TMP_Text Text => text != null ? text : text = GetComponent<TMP_Text>();
        public string CurrentLinkId { get; private set; } = string.Empty;
        // Changes whenever the shown text changes, so anchors made for an older text become invalid.
        public int Version { get; private set; }

        public void SetText(string value)
        {
            Version++;
            SetCurrent(string.Empty, false);
            if (Text != null) Text.text = value ?? string.Empty;
        }

        public void Clear()
        {
            Version++;
            SetCurrent(string.Empty, false);
            if (Text != null) Text.text = string.Empty;
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (TooltipPresenter.IsTouch(eventData) || Text == null) return;
            var index = TMP_TextUtilities.FindIntersectingLink(Text, eventData.position, EventCamera());
            var linkId = index >= 0 && index < Text.textInfo.linkCount ? Text.textInfo.linkInfo[index].GetLinkID() : string.Empty;
            SetCurrent(linkId, true);
        }

        public void OnPointerExit(PointerEventData eventData) => SetCurrent(string.Empty, true);

        // Simulates the pointer over a link or over no link (Play Mode probes).
        public void SetPointerLinkForTests(string linkId) => SetCurrent(linkId ?? string.Empty, true);

        private void OnDisable() => SetCurrent(string.Empty, false);

        public bool TryGetLinkRect(string linkId, RectTransform space, out Rect rect)
        {
            rect = default;
            if (Text == null || space == null || string.IsNullOrEmpty(linkId)) return false;
            if (Text.havePropertiesChanged || Text.textInfo == null || Text.textInfo.characterCount == 0) Text.ForceMeshUpdate();
            var info = Text.textInfo;
            if (info == null) return false;
            for (var i = 0; i < info.linkCount; i++)
            {
                var link = info.linkInfo[i];
                if (!string.Equals(link.GetLinkID(), linkId, StringComparison.Ordinal)) continue;
                var found = false;
                var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                var end = Mathf.Min(info.characterCount, link.linkTextfirstCharacterIndex + link.linkTextLength);
                for (var c = link.linkTextfirstCharacterIndex; c < end; c++)
                {
                    var character = info.characterInfo[c];
                    if (!character.isVisible) continue;
                    found = true;
                    Encapsulate(space, new Vector3(character.bottomLeft.x, character.descender), ref min, ref max);
                    Encapsulate(space, new Vector3(character.topRight.x, character.ascender), ref min, ref max);
                }
                if (!found) return false;
                rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                return true;
            }
            return false;
        }

        private void Encapsulate(RectTransform space, Vector3 local, ref Vector2 min, ref Vector2 max)
        {
            Vector2 point = space.InverseTransformPoint(Text.rectTransform.TransformPoint(local));
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        private Camera EventCamera()
        {
            var canvas = Text.canvas;
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private void SetCurrent(string linkId, bool notify)
        {
            if (string.Equals(CurrentLinkId, linkId, StringComparison.Ordinal)) return;
            CurrentLinkId = linkId;
            if (notify) LinkChanged?.Invoke(this, linkId);
        }

#if UNITY_EDITOR
        private void Reset() => text = GetComponent<TMP_Text>();
        public void ConfigureForEditor(TMP_Text target) => text = target;
#endif
    }
}

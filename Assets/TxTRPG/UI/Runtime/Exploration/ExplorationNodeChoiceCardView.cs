using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Exploration
{
    [Serializable]
    public readonly struct ExplorationNodeChoiceRequest
    {
        public ExplorationNodeChoiceRequest(string runId, string choiceSetId, string nodeId)
        { RunId = runId; ChoiceSetId = choiceSetId; NodeId = nodeId; }
        public string RunId { get; }
        public string ChoiceSetId { get; }
        public string NodeId { get; }
    }

    public readonly struct ExplorationNodeChoiceCardData
    {
        public ExplorationNodeChoiceCardData(ExplorationNodeChoiceRequest request, string title, string description,
            string status, Sprite artwork, bool interactable)
        { Request = request; Title = title; Description = description; Status = status; Artwork = artwork; Interactable = interactable; }
        public ExplorationNodeChoiceRequest Request { get; }
        public string Title { get; }
        public string Description { get; }
        public string Status { get; }
        public Sprite Artwork { get; }
        public bool Interactable { get; }
    }

    [DisallowMultipleComponent]
    public sealed class ExplorationNodeChoiceCardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private RectTransform motionRoot;
        [SerializeField] private CanvasGroup visualRoot;
        [SerializeField] private Image artwork;
        [SerializeField] private GameObject artworkViewport;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private ExplorationCardShapePresentation shapePresentation;

        private Action<ExplorationNodeChoiceRequest> selected;
        private ExplorationNodeChoiceRequest request;

        public Button Button => button;
        public ExplorationNodeChoiceRequest Request => request;
        public ExplorationCardShapePresentation ShapePresentation => shapePresentation;

        public void Bind(in ExplorationNodeChoiceCardData data, Action<ExplorationNodeChoiceRequest> onSelected)
        {
            Unbind();
            request = data.Request;
            selected = onSelected;
            titleText.text = data.Title ?? string.Empty;
            descriptionText.text = data.Description ?? string.Empty;
            statusText.text = data.Status ?? string.Empty;
            statusText.transform.parent.gameObject.SetActive(!string.IsNullOrWhiteSpace(data.Status));
            artwork.sprite = data.Artwork;
            artwork.color = data.Artwork != null ? Color.white : new Color(.16f, .2f, .28f, 1f);
            artworkViewport.SetActive(true);
            button.interactable = data.Interactable;
            ResetPresentation();
            button.onClick.AddListener(HandleSelected);
            gameObject.SetActive(true);
        }

        public void Unbind()
        {
            if (button != null) button.onClick.RemoveListener(HandleSelected);
            selected = null;
            request = default;
            if (artwork != null) artwork.sprite = null;
            ResetPresentation();
        }

        public void ResetPresentation()
        {
            if (motionRoot != null)
            {
                motionRoot.anchoredPosition3D = Vector3.zero;
                motionRoot.localRotation = Quaternion.identity;
                motionRoot.localScale = Vector3.one;
            }
            if (visualRoot != null) visualRoot.alpha = 1f;
        }

        public void ApplyShape(ExplorationCardShapeSettings settings) => shapePresentation?.Apply(settings);

        private void HandleSelected() => selected?.Invoke(request);
        private void OnDisable() => ResetPresentation();
        private void OnDestroy() => Unbind();

#if UNITY_EDITOR
        public void ConfigureForEditor(Button targetButton, RectTransform targetMotionRoot, CanvasGroup targetVisualRoot,
            Image targetArtwork, GameObject targetArtworkViewport, TMP_Text targetTitle, TMP_Text targetDescription, TMP_Text targetStatus, ExplorationCardShapePresentation targetShapePresentation)
        { button = targetButton; motionRoot = targetMotionRoot; visualRoot = targetVisualRoot; artwork = targetArtwork; artworkViewport = targetArtworkViewport; titleText = targetTitle; descriptionText = targetDescription; statusText = targetStatus; shapePresentation = targetShapePresentation; }
#endif
    }
}

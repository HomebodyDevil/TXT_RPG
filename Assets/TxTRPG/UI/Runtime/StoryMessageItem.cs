using TMPro;
using UnityEngine;

namespace TxTRPG.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class StoryMessageItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private CanvasGroup canvasGroup;

        public void Bind(in StoryMessage message)
        {
            bodyText.text = message.Text;
            speakerText.gameObject.SetActive(message.HasSpeaker);
            if (message.HasSpeaker)
            {
                speakerText.text = message.Speaker;
            }
        }

        public void SetOpacity(float opacity)
        {
            canvasGroup.alpha = Mathf.Clamp01(opacity);
        }

        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnValidate()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }
    }
}

using UnityEngine;

namespace TxTRPG.UI
{
    [DisallowMultipleComponent]
    public sealed class StoryTextPanelDemoPreviewItem : MonoBehaviour
    {
        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}

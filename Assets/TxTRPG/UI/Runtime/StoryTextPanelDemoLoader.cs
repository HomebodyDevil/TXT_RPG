using UnityEngine;

namespace TxTRPG.UI
{
    [DisallowMultipleComponent]
    public sealed class StoryTextPanelDemoLoader : MonoBehaviour
    {
        [SerializeField] private StoryTextPanel target;
        [SerializeField] private StoryTextPanelDemoData data;
        [SerializeField] private bool populateOnStart = true;
        [SerializeField] private bool clearBeforePopulate = true;

        private void Start()
        {
            if (populateOnStart)
            {
                Populate();
            }
        }

        [ContextMenu("Populate Demo Messages")]
        public void Populate()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("StoryTextPanel demo messages can be populated in Play Mode.", this);
                return;
            }

            if (target == null || data == null)
            {
                Debug.LogError("StoryTextPanel demo loader requires both a target and demo data.", this);
                return;
            }

            if (clearBeforePopulate)
            {
                target.Clear();
            }

            foreach (var entry in data.Entries)
            {
                var message = entry.ToMessage();
                target.AddMessage(message);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI
{
    [CreateAssetMenu(fileName = "StoryTextPanelDemoData", menuName = "TxT RPG/UI/Story Text Panel Demo Data")]
    public sealed class StoryTextPanelDemoData : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string speaker;
            [SerializeField, TextArea(2, 5)] private string text;

            public Entry(string text, string speaker = "")
            {
                this.text = text;
                this.speaker = speaker;
            }

            public StoryMessage ToMessage()
            {
                return new StoryMessage(text, speaker);
            }
        }

        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

#if UNITY_EDITOR
        public void ReplaceEntries(IEnumerable<Entry> replacement)
        {
            entries.Clear();
            entries.AddRange(replacement);
        }
#endif
    }
}

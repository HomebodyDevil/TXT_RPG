using System;

namespace TxTRPG.UI
{
    /// <summary>
    /// Presentation-ready story text. Resolve localization keys before passing a message to the UI.
    /// </summary>
    [Serializable]
    public readonly struct StoryMessage
    {
        public StoryMessage(string text, string speaker = null)
        {
            Text = text ?? string.Empty;
            Speaker = speaker ?? string.Empty;
        }

        public string Text { get; }
        public string Speaker { get; }
        public bool HasSpeaker => !string.IsNullOrWhiteSpace(Speaker);
    }
}

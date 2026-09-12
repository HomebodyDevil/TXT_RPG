using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace TxTRPG.UI.Windows
{
    public sealed class MessageGameWindowPage : GameWindowPage
    {
        [SerializeField] private TMP_Text message;
        [SerializeField, TextArea] private string configuredMessage = string.Empty;
        public string ConfiguredMessage => configuredMessage ?? string.Empty;
        public void SetMessage(string value) { configuredMessage = value ?? string.Empty; if (message != null) message.text = configuredMessage; }
        public override Task PrepareAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); if (message != null) message.text = configuredMessage; return Task.CompletedTask; }
#if UNITY_EDITOR
        public void ConfigureMessageForEditor(TMP_Text target, string value) { message = target; configuredMessage = value; }
#endif
    }
}

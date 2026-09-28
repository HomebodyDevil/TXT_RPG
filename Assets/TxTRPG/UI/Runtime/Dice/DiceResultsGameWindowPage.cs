using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.UI.Windows;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Dice
{
    [DisallowMultipleComponent]
    public sealed class DiceResultsGameWindowPage : GameWindowPage
    {
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private ScrollRect resultScroll;
        [SerializeField] private Button confirmButton;
        [SerializeField] private GameWindowService windowService;
        private string[] rows = System.Array.Empty<string>();

        public string DisplayedText => resultText != null ? resultText.text : string.Empty;
        public int RowCount => rows.Length;

        private void OnEnable()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(Close);
        }

        public void SetRows(IReadOnlyList<string> values)
        {
            rows = new string[values?.Count ?? 0];
            for (var i = 0; i < rows.Length; i++) rows[i] = values[i] ?? string.Empty;
            RefreshText();
        }

        public override Task PrepareAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshText();
            return Task.CompletedTask;
        }

        public override void Show()
        {
            base.Show();
            if (resultScroll != null) resultScroll.verticalNormalizedPosition = 1f;
        }

        private void RefreshText()
        {
            if (resultText == null) return;
            if (rows.Length == 0) { resultText.text = "아직 굴림 결과가 없습니다."; return; }
            var builder = new StringBuilder();
            for (var i = rows.Length - 1; i >= 0; i--)
            {
                if (builder.Length > 0) builder.Append("\n\n");
                builder.Append(rows[i]);
            }
            resultText.text = builder.ToString();
        }

        private void Close() => windowService?.Close();

#if UNITY_EDITOR
        public void ConfigureForEditor(TMP_Text text, ScrollRect scroll, Button confirm, GameWindowService service)
        { resultText = text; resultScroll = scroll; confirmButton = confirm; windowService = service; }
#endif
    }
}

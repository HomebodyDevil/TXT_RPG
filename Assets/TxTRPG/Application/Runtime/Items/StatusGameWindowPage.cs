using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.Application.Players;
using TxTRPG.UI.Windows;
using UnityEngine;

namespace TxTRPG.Application.Items
{
    public sealed class StatusGameWindowPage : GameWindowPage
    {
        [SerializeField] private PlayerSessionHost sessionHost;
        [SerializeField] private TMP_Text statusText;
        public override async Task PrepareAsync(CancellationToken cancellationToken)
        {
            var host = sessionHost != null ? sessionHost : PlayerSessionHost.Instance;
            if (host == null) throw new InvalidOperationException("Status page requires a PlayerSessionHost.");
            await host.EnsureInitializedAsync(cancellationToken);
            var character = host.Session.CurrentPlayer.ActiveCharacter;
            statusText.text = $"HP {character.Health.Current} / {character.Health.Maximum}\nAttack {character.Stats.AttackPower}";
        }
#if UNITY_EDITOR
        public void ConfigureForEditor(PlayerSessionHost host, TMP_Text text) { sessionHost = host; statusText = text; }
#endif
    }
}

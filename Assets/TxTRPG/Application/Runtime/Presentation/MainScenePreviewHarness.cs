using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.Application.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MainScenePreviewHarness : MonoBehaviour
    {
        [SerializeField] private MainScenePresentationController target;
        [SerializeField] private MainScenePreviewProfile profile;
        [SerializeField] private bool runOnEnable;
        private CancellationTokenSource lifetime;
        private async void OnEnable()
        {
            lifetime = new CancellationTokenSource();
            if (!runOnEnable) return;
            try { await RunAsync(lifetime.Token); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception exception) { Debug.LogError($"Main Scene preview failed: {exception.Message}", this); }
        }
        private void OnDisable() { lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null; }
        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            if (target == null || profile == null) throw new InvalidOperationException("Preview harness references are incomplete.");
            return target.RunPreviewAsync(profile, cancellationToken);
        }
        public void Configure(MainScenePresentationController controller, MainScenePreviewProfile preview, bool autoRun = false)
        { target = controller; profile = preview; runOnEnable = autoRun; }
    }
}

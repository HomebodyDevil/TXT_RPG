using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.SceneTransition
{
    [DisallowMultipleComponent]
    public sealed class SceneReadySignal : MonoBehaviour, ISceneReadySource
    {
        [SerializeField] private bool markReadyOnStart = true;

        private TaskCompletionSource<bool> readySource = CreateSource();

        public Task WhenReady => readySource.Task;
        public bool IsReady => readySource.Task.IsCompletedSuccessfully;

        private void Start()
        {
            if (markReadyOnStart)
            {
                MarkReady();
            }
        }

        private void OnDestroy()
        {
            readySource.TrySetCanceled();
        }

        public void Configure(bool readyAutomatically)
        {
            markReadyOnStart = readyAutomatically;
        }

        public void MarkReady()
        {
            readySource.TrySetResult(true);
        }

        public void ResetSignal()
        {
            if (readySource.Task.IsCompleted)
            {
                readySource = CreateSource();
            }
        }

        private static TaskCompletionSource<bool> CreateSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

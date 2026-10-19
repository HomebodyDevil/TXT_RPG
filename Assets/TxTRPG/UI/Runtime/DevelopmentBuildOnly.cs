using UnityEngine;

namespace TxTRPG.UI
{
    // Deactivates its GameObject in a Release player (Debug.isDebugBuild is false). In the Editor and in Development
    // Builds it does nothing. The one place that decides debug-only visibility, so feature code needs no preprocessor checks.
    [DisallowMultipleComponent]
    public sealed class DevelopmentBuildOnly : MonoBehaviour
    {
        public static bool ShouldDisable(bool isDebugBuild) => !isDebugBuild;

        private void Awake()
        {
            if (ShouldDisable(Debug.isDebugBuild)) gameObject.SetActive(false);
        }
    }
}

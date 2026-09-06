using UnityEngine;

namespace TxTRPG.SceneTransition
{
    public sealed class ScenePathAttribute : PropertyAttribute
    {
        public ScenePathAttribute(bool excludeAppScene = false)
        {
            ExcludeAppScene = excludeAppScene;
        }

        public bool ExcludeAppScene { get; }
    }
}

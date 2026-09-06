using System;
using UnityEngine;

namespace TxTRPG.SceneTransition
{
    [Obsolete("Use AppSceneRoot in Assets/Scenes/AppScene.unity instead.")]
    [DisallowMultipleComponent]
    public sealed class PersistentAppRoot : MonoBehaviour
    {
        [SerializeField] private SceneTransitionService sceneTransitionService;

        public SceneTransitionService SceneTransitions => sceneTransitionService;

        public void Configure(SceneTransitionService service)
        {
            sceneTransitionService = service;
        }
    }
}

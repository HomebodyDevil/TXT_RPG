using System.Collections.Generic;

namespace TxTRPG.Editor.Common.PrefabRebuild
{
    public sealed class ProjectPrefabRebuildExclusionProvider : IPrefabRebuildTaskProvider
    {
        public IEnumerable<PrefabRebuildTaskDescriptor> GetTasks() { yield break; }
        public IEnumerable<PrefabRebuildExclusion> GetExclusions()
        {
            yield return new("application.quick-items", "Game Menu and Modal Windows", "The current prefab core also writes a Demo prefab. Split production and Demo outputs before registration.");
            yield return new("application.main-scene-defaults", "Main Scene Default Content", "Creates profiles and modifies TMP_MainScene; Scene and content configuration are excluded.");
            yield return new("application.player-session", "Player Session/AppRoot setup", "Creates configuration assets and modifies Scene composition.");
            yield return new("content.default-character", "Default Character Content", "Creates and may roll back content and Addressables entries; it is not a prefab-only task.");
            yield return new("scene-transition.app-scene", "Scene Transition/AppScene", "Changes scenes, profiles and Build Settings in addition to AppRoot.prefab.");
            yield return new("external-and-manual", "Manual, Variant, and package prefabs", "No registered project generator owns these assets, so the batch never discovers or rewrites them.");
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Editor
{
    internal static class PanelStartupPrefabUtility
    {
        public static PanelStartupController Configure(
            GameObject root,
            PanelInitialDataLoader loader,
            RectTransform layoutRoot)
        {
            var canvasGroup = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
            var transition = root.GetComponent<FadePanelRevealTransition>() ??
                             root.AddComponent<FadePanelRevealTransition>();
            var transitionProperties = new SerializedObject(transition);
            transitionProperties.FindProperty("animateReveal").boolValue = false;
            transitionProperties.ApplyModifiedPropertiesWithoutUndo();
            var controller = root.GetComponent<PanelStartupController>() ??
                             root.AddComponent<PanelStartupController>();
            var properties = new SerializedObject(controller);
            properties.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            properties.FindProperty("initialDataLoader").objectReferenceValue = loader;
            properties.FindProperty("revealTransition").objectReferenceValue = transition;
            properties.FindProperty("layoutRoot").objectReferenceValue = layoutRoot;
            properties.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }
    }
}

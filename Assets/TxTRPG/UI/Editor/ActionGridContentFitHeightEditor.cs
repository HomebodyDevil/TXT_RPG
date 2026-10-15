using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Editor
{
    [CustomEditor(typeof(ActionGridContentFitHeight))]
    public sealed class ActionGridContentFitHeightEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var source = (ActionGridContentFitHeight)target;
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("Supply State", source.State);
                EditorGUILayout.FloatField("Resolved Height", float.IsNaN(source.ResolvedHeight) ? 0f : source.ResolvedHeight);
                var actual = ((RectTransform)source.transform).rect.height;
                EditorGUILayout.FloatField("Actual Height", actual);
            }
            if (source.IsSupplying && source.ResolvedHeight - ((RectTransform)source.transform).rect.height >= 0.5f)
                EditorGUILayout.HelpBox("The parent layout is shrinking this panel below the resolved height.", MessageType.Info);
            else if (!source.IsSupplying)
                EditorGUILayout.HelpBox(StateMessage(source.State), MessageType.None);
        }

        private static string StateMessage(ActionGridContentFitState state) => state switch
        {
            ActionGridContentFitState.GridFlow => "Not supplying: the panel uses Grid flow. The FlexibleLayoutItem values apply.",
            ActionGridContentFitState.HorizontalAxis => "Not supplying: the parent FlexibleContentLayoutGroup main axis is horizontal.",
            ActionGridContentFitState.MissingReference => "Not supplying: the ActionGridPanel or its ActionGridSurfaceLayout reference is missing.",
            ActionGridContentFitState.UnmanagedBodyLayout => "Not supplying: ActionGridSurfaceLayout does not manage the header layout and body margins.",
            _ => "Not supplying: the component or panel is disabled."
        };
    }
}

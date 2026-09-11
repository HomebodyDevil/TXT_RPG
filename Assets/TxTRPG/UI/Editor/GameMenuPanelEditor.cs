using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Editor
{
    [CustomEditor(typeof(GameMenuPanel))]
    public sealed class GameMenuPanelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "scrollbarVisibility",
                "scrollbarSpaceMode", "scrollbarHeight", "scrollbarGap");

            var panel = (GameMenuPanel)target;
            var layoutProperty = serializedObject.FindProperty("layout");
            var layout = layoutProperty.objectReferenceValue as GameMenuLayoutGroup;
            if (layout != null && layout.LayoutMode == GameMenuLayoutMode.HorizontalScroll)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Horizontal Scrollbar", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("scrollbarVisibility"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("scrollbarSpaceMode"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("scrollbarHeight"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("scrollbarGap"));
            }

            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            if (!panel.ValidateInternalConfiguration(out var reason))
                EditorGUILayout.HelpBox(reason, MessageType.Error);
            else if (panel.WindowService == null)
                EditorGUILayout.HelpBox("Internal UI is complete. External GameWindowService is not connected; menu buttons remain disabled until binding.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Internal UI and external window service are ready.", MessageType.None);
            using (new EditorGUI.DisabledScope(true))
            {
                var result = panel.CurrentLayout;
                EditorGUILayout.IntField("Calculated Columns", result.Columns);
                EditorGUILayout.IntField("Calculated Rows", result.Rows);
                EditorGUILayout.FloatField("Required Width", result.RequiredWidth);
                EditorGUILayout.FloatField("Required Height", result.RequiredHeight);
                EditorGUILayout.Toggle("Insufficient Space", result.HasInsufficientSpace);
            }
            if (GUILayout.Button("Refresh Layout")) panel.RefreshLayout();
        }
    }
}

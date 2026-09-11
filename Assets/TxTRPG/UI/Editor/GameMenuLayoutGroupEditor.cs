using TxTRPG.UI.Windows;
using UnityEditor;

namespace TxTRPG.UI.Editor
{
    [CustomEditor(typeof(GameMenuLayoutGroup))]
    public sealed class GameMenuLayoutGroupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("viewport"));
            var mode = serializedObject.FindProperty("layoutMode");
            EditorGUILayout.PropertyField(mode);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("buttonSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("spacing"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Padding"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("horizontalAlignment"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("verticalAlignment"));
            if ((GameMenuLayoutMode)mode.enumValueIndex == GameMenuLayoutMode.Wrap)
            {
                var policy = serializedObject.FindProperty("wrapColumnPolicy");
                EditorGUILayout.PropertyField(policy);
                if ((GameMenuWrapColumnPolicy)policy.enumValueIndex == GameMenuWrapColumnPolicy.MaximumColumns)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("maximumColumns"));
            }
            serializedObject.ApplyModifiedProperties();

            var result = ((GameMenuLayoutGroup)target).Current;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Calculated Columns", result.Columns);
                EditorGUILayout.IntField("Calculated Rows", result.Rows);
                EditorGUILayout.FloatField("Required Width", result.RequiredWidth);
                EditorGUILayout.FloatField("Required Height", result.RequiredHeight);
                EditorGUILayout.Toggle("Insufficient Space", result.HasInsufficientSpace);
            }
        }
    }
}

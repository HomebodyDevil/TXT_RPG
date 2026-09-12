using UnityEditor;

namespace TxTRPG.UI.Editor
{
    [CustomEditor(typeof(ActionGridCell))]
    public sealed class ActionGridCellEditor : UnityEditor.Editor
    {
        private SerializedProperty sizingMode;
        private SerializedProperty areaRatio;
        private SerializedProperty padding;
        private SerializedProperty maximumSize;
        private SerializedProperty emptySlotSizingMode;
        private SerializedProperty emptySlotAreaRatio;
        private SerializedProperty emptySlotPadding;
        private SerializedProperty emptySlotMaximumSize;

        private void OnEnable()
        {
            sizingMode = serializedObject.FindProperty("iconSizingMode");
            areaRatio = serializedObject.FindProperty("iconAreaRatio");
            padding = serializedObject.FindProperty("iconPadding");
            maximumSize = serializedObject.FindProperty("iconMaximumSize");
            emptySlotSizingMode = serializedObject.FindProperty("emptySlotSizingMode");
            emptySlotAreaRatio = serializedObject.FindProperty("emptySlotAreaRatio");
            emptySlotPadding = serializedObject.FindProperty("emptySlotPadding");
            emptySlotMaximumSize = serializedObject.FindProperty("emptySlotMaximumSize");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject,
                "m_Script", "iconSizingMode", "iconAreaRatio", "iconPadding", "iconMaximumSize",
                "emptySlotSizingMode", "emptySlotAreaRatio", "emptySlotPadding", "emptySlotMaximumSize");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Icon Layout", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(sizingMode);
            var iconMode = (ActionGridIconSizingMode)sizingMode.enumValueIndex;
            if (iconMode == ActionGridIconSizingMode.FixedPadding)
                EditorGUILayout.PropertyField(padding, true);
            else
            {
                EditorGUILayout.PropertyField(areaRatio);
                if (iconMode == ActionGridIconSizingMode.RelativeWithMaxSize)
                    EditorGUILayout.PropertyField(maximumSize);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Empty Slot Layout", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(emptySlotSizingMode);
            var emptyMode = (ActionGridIconSizingMode)emptySlotSizingMode.enumValueIndex;
            if (emptyMode == ActionGridIconSizingMode.FixedPadding)
                EditorGUILayout.PropertyField(emptySlotPadding, true);
            else
            {
                EditorGUILayout.PropertyField(emptySlotAreaRatio);
                if (emptyMode == ActionGridIconSizingMode.RelativeWithMaxSize)
                    EditorGUILayout.PropertyField(emptySlotMaximumSize);
            }            serializedObject.ApplyModifiedProperties();
        }
    }
}

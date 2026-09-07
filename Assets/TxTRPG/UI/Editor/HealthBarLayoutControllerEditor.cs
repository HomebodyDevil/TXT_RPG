using UnityEditor;

namespace TxTRPG.UI.Editor
{
    [CustomEditor(typeof(HealthBarLayoutController))]
    [CanEditMultipleObjects]
    public sealed class HealthBarLayoutControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Draw("referenceArea");
            Draw("barRoot");
            Draw("horizontalSizeMode");
            Draw("verticalSizeMode");
            Draw("fixedSize");
            Draw("horizontalAlignment");
            Draw("verticalAlignment");
            EditorGUILayout.LabelField("Padding", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            Draw("paddingLeft");
            Draw("paddingRight");
            Draw("paddingTop");
            Draw("paddingBottom");
            EditorGUI.indentLevel--;
            Draw("offset");

            var horizontal = serializedObject.FindProperty("horizontalSizeMode");
            var vertical = serializedObject.FindProperty("verticalSizeMode");
            if ((!horizontal.hasMultipleDifferentValues &&
                 horizontal.enumValueIndex == (int)HealthBarAxisSizeMode.Stretch) ||
                (!vertical.hasMultipleDifferentValues &&
                 vertical.enumValueIndex == (int)HealthBarAxisSizeMode.Stretch))
            {
                EditorGUILayout.HelpBox(
                    "Stretch is the padding-driven default. Alignment on a Stretch axis does not change placement " +
                    "because the bar fills the complete padded area. Offset may move it outside that area.",
                    MessageType.Info);
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string propertyName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null) EditorGUILayout.PropertyField(property, true);
        }
    }
}

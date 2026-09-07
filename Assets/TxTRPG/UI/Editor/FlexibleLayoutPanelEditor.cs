using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Editor
{
    [CustomEditor(typeof(FlexibleLayoutPanel))]
    [CanEditMultipleObjects]
    public sealed class FlexibleLayoutPanelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Draw("nodeId");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Content Layout", EditorStyles.boldLabel);
            Draw("fixedAxis");
            Draw("axisPolicy");
            Draw("breakpoint");
            Draw("spacing");
            Draw("m_Padding");
            Draw("m_ChildAlignment");
            Draw("overflow");
            Draw("includeInactiveChildren");
            Draw("overrideContentMargins");
            Draw("contentMargins");
            Draw("clipContent");

            using (new EditorGUI.DisabledScope(true))
            {
                Draw("contentRoot");
                Draw("contentLayout");
                Draw("contentMask");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Background Content", EditorStyles.boldLabel);
            Draw("useContentLayoutSettings");
            var shared = serializedObject.FindProperty("useContentLayoutSettings");
            using (new EditorGUI.DisabledScope(shared.hasMultipleDifferentValues || shared.boolValue))
            {
                Draw("backgroundFixedAxis");
                Draw("backgroundAxisPolicy");
                Draw("backgroundBreakpoint");
                Draw("backgroundSpacing");
                Draw("backgroundPadding");
                Draw("backgroundChildAlignment");
                Draw("backgroundOverflow");
                Draw("backgroundIncludeInactiveChildren");
                Draw("overrideBackgroundContentMargins");
                Draw("backgroundContentMargins");
                Draw("backgroundClipContent");
            }

            using (new EditorGUI.DisabledScope(true))
            {
                Draw("backgroundContentRoot");
                Draw("backgroundContentLayout");
                Draw("backgroundContentMask");
                Draw("backgroundContentCanvasGroup");
            }

            if (shared.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "BackgroundContentLayer uses Content Layout settings and its final RectTransform. " +
                    "Disable this option to edit the independent settings shown above.",
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

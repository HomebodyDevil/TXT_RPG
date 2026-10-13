using System;
using System.Collections.Generic;
using System.Linq;
using TxTRPG.Application.Exploration.Events;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Application.Editor
{
    // Dropdown for [SerializeReference, ExplorationEventReferencePicker] fields: lists the concrete [Serializable] classes
    // assignable to the field type (TypeCache, Editor only) and draws the selected object's fields below.
    [CustomPropertyDrawer(typeof(ExplorationEventReferencePickerAttribute))]
    public sealed class ExplorationEventReferencePickerDrawer : PropertyDrawer
    {
        private static readonly Dictionary<string, Type[]> CandidatesByField = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference) return EditorGUIUtility.singleLineHeight * 2f;
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.HelpBox(position, "ExplorationEventReferencePicker requires [SerializeReference].", MessageType.Error);
                return;
            }
            var candidates = Candidates(property.managedReferenceFieldTypename);
            var current = property.managedReferenceValue?.GetType();
            var names = new[] { "(None)" }.Concat(candidates.Select(type => ObjectNames.NicifyVariableName(type.Name))).ToArray();
            var index = current == null ? 0 : Array.IndexOf(candidates, current) + 1;
            var header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var popup = new Rect(header.x + EditorGUIUtility.labelWidth + 2f, header.y, Mathf.Max(60f, header.width - EditorGUIUtility.labelWidth - 2f), header.height);
            EditorGUI.BeginChangeCheck();
            var selected = EditorGUI.Popup(popup, index, names);
            if (EditorGUI.EndChangeCheck() && selected != index)
            {
                property.managedReferenceValue = selected == 0 ? null : Activator.CreateInstance(candidates[selected - 1]);
                property.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
            EditorGUI.PropertyField(position, property, label, true);
        }

        private static Type[] Candidates(string fieldTypeName)
        {
            if (CandidatesByField.TryGetValue(fieldTypeName, out var cached)) return cached;
            var fieldType = Resolve(fieldTypeName);
            var result = fieldType == null ? Array.Empty<Type>() : TypeCache.GetTypesDerivedFrom(fieldType)
                .Where(type => !type.IsAbstract && !type.IsGenericType && type.IsSerializable && !typeof(UnityEngine.Object).IsAssignableFrom(type) &&
                               type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(type => type.Name, StringComparer.Ordinal)
                .ToArray();
            CandidatesByField[fieldTypeName] = result;
            return result;
        }

        // managedReferenceFieldTypename is "Assembly Namespace.Type".
        private static Type Resolve(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            var split = typeName.IndexOf(' ');
            if (split < 0) return null;
            var assemblyName = typeName.Substring(0, split);
            var fullName = typeName.Substring(split + 1);
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == assemblyName)?.GetType(fullName);
        }
    }

    // Shows the validation result and authoring warnings (overlapping slot ranges, options that all have conditions).
    [CustomEditor(typeof(ExplorationEventDefinition))]
    public sealed class ExplorationEventDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var definition = (ExplorationEventDefinition)target;
            if (!definition.TryCreateEvent(out _, out var reason)) EditorGUILayout.HelpBox($"Invalid event: {reason}", MessageType.Error);
            foreach (var warning in definition.CollectWarnings()) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            DrawDefaultInspector();
        }
    }
}

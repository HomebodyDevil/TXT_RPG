using System.Collections.Generic;
using UnityEditor;
using TxTRPG.Editor.Common.Menu;
using UnityEngine;

namespace TxTRPG.Content.Characters.Editor
{
    public static class CharacterContentProjectValidator
    {
        [MenuItem(
            TxTRPGEditorMenuPaths.CharacterContent + "Validate All",
            false,
            TxTRPGEditorMenuPriorities.Validate)]
        public static void ValidateFromMenu()
        {
            var definitions = LoadAllDefinitions();
            var errors = CharacterContentValidation.CollectErrors(definitions);
            if (errors.Count == 0)
            {
                Debug.Log($"Validated {definitions.Count} character content definitions.");
                EditorUtility.DisplayDialog(
                    "Character Content Validation",
                    $"Validation succeeded for {definitions.Count} definitions.",
                    "OK");
                return;
            }

            foreach (var error in errors)
            {
                Debug.LogError(error);
            }
            EditorUtility.DisplayDialog(
                "Character Content Validation",
                $"Validation failed with {errors.Count} error(s). See the Console.",
                "OK");
        }

        public static IReadOnlyList<CharacterContentDefinition> LoadAllDefinitions()
        {
            var definitions = new List<CharacterContentDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterContentDefinition"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<CharacterContentDefinition>(path);
                if (definition != null)
                {
                    definitions.Add(definition);
                }
            }
            return definitions;
        }
    }
}

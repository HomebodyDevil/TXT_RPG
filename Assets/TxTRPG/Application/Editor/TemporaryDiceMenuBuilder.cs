using System;
using System.Linq;
using TMPro;
using TxTRPG.Application.Dice;
using TxTRPG.Application.Players;
using TxTRPG.UI;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    public static class TemporaryDiceMenuBuilder
    {
        public const string ConfigurationPath = "Assets/TxTRPG/Application/Configuration/TemporaryDiceConfiguration.asset";
        public const string AppRootPrefabPath = "Assets/TxTRPG/SceneTransition/Prefabs/AppRoot.prefab";
        public const string ScenePath = "Assets/Scenes/TMP_MainScene.unity";
        public const string ButtonName = "TemporaryDiceRoll";

        [MenuItem("Tools/TxT RPG/Application/Temporary/Apply Player Dice Roll Menu")]
        public static void Apply()
        {
            var configuration = CreateOrUpdateConfiguration();
            ApplyAppRoot(configuration);
            ApplyMainScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Temporary player dice roll menu was applied. This operation is safe to rerun.");
        }

        public static TemporaryDiceConfiguration CreateOrUpdateConfiguration()
        {
            EnsureFolder("Assets/TxTRPG/Application/Configuration");
            var asset = AssetDatabase.LoadAssetAtPath<TemporaryDiceConfiguration>(ConfigurationPath);
            if (asset == null) { asset = ScriptableObject.CreateInstance<TemporaryDiceConfiguration>(); AssetDatabase.CreateAsset(asset, ConfigurationPath); }
            var definitions = new[] { Create("temporary.d4", "D4", 4), Create("temporary.d6", "D6", 6), Create("temporary.d8", "D8", 8) };
            asset.ConfigureForEditor(true, definitions);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static TemporaryDieDefinition Create(string id, string name, int faces)
        { var result = new TemporaryDieDefinition(); result.ConfigureForEditor(id, name, faces); return result; }

        private static void ApplyAppRoot(TemporaryDiceConfiguration configuration)
        {
            var root = PrefabUtility.LoadPrefabContents(AppRootPrefabPath);
            try
            {
                var host = root.GetComponentInChildren<PlayerSessionHost>(true) ?? throw new InvalidOperationException("AppRoot has no PlayerSessionHost.");
                var serialized = new SerializedObject(host);
                serialized.FindProperty("temporaryDiceConfiguration").objectReferenceValue = configuration;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, AppRootPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ApplyMainScene()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Save or revert unsaved TMP_MainScene changes before applying the temporary dice menu.");
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var menu = roots.SelectMany(root => root.GetComponentsInChildren<GameMenuPanel>(true)).Single();
                var story = roots.SelectMany(root => root.GetComponentsInChildren<StoryTextPanel>(true)).Single();
                var controller = menu.GetComponent<TemporaryDiceRollMenuController>() ?? menu.gameObject.AddComponent<TemporaryDiceRollMenuController>();
                controller.ConfigureForEditor(menu, story);
                menu.SetCommandHandlerForEditor(controller);
                var content = menu.Content ?? throw new InvalidOperationException("GameMenuPanel has no Content reference.");
                var existing = content.Find(ButtonName);
                var view = existing != null ? existing.GetComponent<GameMenuButtonView>() : CreateButton(content);
                var binding = new GameMenuButtonBinding();
                binding.ConfigureCommandForEditor(TemporaryDiceRollMenuController.RollAllCommandId, view.Button, true, view);
                menu.AddOrReplaceCommandForEditor(binding);
                EditorUtility.SetDirty(menu); EditorUtility.SetDirty(controller);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save TMP_MainScene.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static GameMenuButtonView CreateButton(Transform parent)
        {
            var root = CreateRect(ButtonName, parent); root.sizeDelta = new Vector2(150, 48);
            var visual = CreateRect("VisualRoot", root); Stretch(visual);
            var bgRect = CreateRect("Background", visual); Stretch(bgRect); var bg = bgRect.gameObject.AddComponent<Image>(); bg.color = new Color(.2f,.24f,.32f,1);
            var iconRect = CreateRect("Icon", visual); Stretch(iconRect); var icon = iconRect.gameObject.AddComponent<Image>(); icon.raycastTarget = false; icon.gameObject.SetActive(false);
            var labelRect = CreateRect("Label", visual); Stretch(labelRect, 8, 6, 8, 6); var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>(); label.text = "주사위 굴리기 (임시)"; label.fontSize = 16; label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false;
            var borderRect = CreateRect("Border", visual); Stretch(borderRect); var border = borderRect.gameObject.AddComponent<Image>(); border.color = new Color(.65f,.72f,.9f,.35f); border.raycastTarget = false;
            var effect = CreateRect("EffectOverlay", visual); Stretch(effect);
            var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = bg;
            var view = root.gameObject.AddComponent<GameMenuButtonView>(); view.ConfigureForEditor(button, visual, bg, icon, label, border, effect); return view;
        }
        private static RectTransform CreateRect(string name, Transform parent) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        private static void Stretch(RectTransform rect, float l=0,float b=0,float r=0,float t=0) { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=new Vector2(l,b); rect.offsetMax=new Vector2(-r,-t); }
        private static void EnsureFolder(string path) { var parts=path.Split('/'); var current=parts[0]; for(var i=1;i<parts.Length;i++){var next=current+"/"+parts[i]; if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,parts[i]); current=next;} }
    }
}
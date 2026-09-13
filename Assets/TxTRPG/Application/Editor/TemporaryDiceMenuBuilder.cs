using System;
using System.Linq;
using TMPro;
using TxTRPG.Application.Combat;
using TxTRPG.Application.Dice;
using TxTRPG.Application.Exploration;
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
        public const string CombatConfigurationPath = "Assets/TxTRPG/Application/Configuration/TemporaryCombatConfiguration.asset";
        public const string ExplorationConfigurationPath = "Assets/TxTRPG/Application/Configuration/ExplorationRunConfiguration.asset";
        public const string HealthBarPrefabPath = "Assets/TxTRPG/UI/Prefabs/HealthBarPanel.prefab";
        public const string AppRootPrefabPath = "Assets/TxTRPG/SceneTransition/Prefabs/AppRoot.prefab";
        public const string ScenePath = "Assets/Scenes/TMP_MainScene.unity";
        public const string ButtonName = "TemporaryDiceRoll";
        public const string EnemyHealthName = "TemporaryEnemyHealthBar";
        public const string NextActionName = "TemporaryEnemyNextAction";
        public const string ExplorationPanelName = "ExplorationNodePanel";

        [MenuItem("Tools/TxT RPG/Application/Temporary/Apply Player Dice Roll Menu")]
        public static void Apply()
        {
            var diceConfiguration = CreateOrUpdateConfiguration();
            var combatConfiguration = CreateOrUpdateCombatConfiguration();
            var explorationConfiguration = CreateOrUpdateExplorationConfiguration();
            ApplyAppRoot(diceConfiguration);
            ApplyMainScene(combatConfiguration, explorationConfiguration);
            AssetDatabase.SaveAssets();
            Debug.Log("Temporary exploration and action combat were applied. This operation is safe to rerun.");
        }

        public static TemporaryDiceConfiguration CreateOrUpdateConfiguration()
        {
            EnsureFolder("Assets/TxTRPG/Application/Configuration");
            var asset = AssetDatabase.LoadAssetAtPath<TemporaryDiceConfiguration>(ConfigurationPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TemporaryDiceConfiguration>(); AssetDatabase.CreateAsset(asset, ConfigurationPath);
                asset.ConfigureForEditor(true, new[] { Create("temporary.d4", "D4", 4), Create("temporary.d6", "D6", 6), Create("temporary.d8", "D8", 8) }); EditorUtility.SetDirty(asset);
            }
            else if (asset.TryUpgradeKnownDefaultsForEditor()) EditorUtility.SetDirty(asset);
            else if (asset.Dice.Any(definition => definition != null && definition.HasLegacyFaces)) throw new InvalidOperationException("The existing temporary dice configuration contains custom legacy numeric faces. Configure each effect explicitly; no effect was guessed.");
            return asset;
        }

        public static TemporaryCombatConfiguration CreateOrUpdateCombatConfiguration()
        {
            EnsureFolder("Assets/TxTRPG/Application/Configuration");
            var asset = AssetDatabase.LoadAssetAtPath<TemporaryCombatConfiguration>(CombatConfigurationPath);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<TemporaryCombatConfiguration>(); asset.ConfigureForEditor(true, "테스트 적", "demo-slime", "temporary.enemy.1", 40, 5, 3); AssetDatabase.CreateAsset(asset, CombatConfigurationPath); return asset;
        }

        public static ExplorationRunConfiguration CreateOrUpdateExplorationConfiguration()
        {
            EnsureFolder("Assets/TxTRPG/Application/Configuration");
            var asset = AssetDatabase.LoadAssetAtPath<ExplorationRunConfiguration>(ExplorationConfigurationPath);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<ExplorationRunConfiguration>(); asset.ConfigureForEditor(3, 50, 50); AssetDatabase.CreateAsset(asset, ExplorationConfigurationPath); return asset;
        }

        private static TemporaryDieDefinition Create(string id, string name, int faces) { var result = new TemporaryDieDefinition(); result.ConfigureForEditor(id, name, faces); return result; }

        private static void ApplyAppRoot(TemporaryDiceConfiguration configuration)
        {
            var root = PrefabUtility.LoadPrefabContents(AppRootPrefabPath);
            try { var host = root.GetComponentInChildren<PlayerSessionHost>(true) ?? throw new InvalidOperationException("AppRoot has no PlayerSessionHost."); var serialized = new SerializedObject(host); serialized.FindProperty("temporaryDiceConfiguration").objectReferenceValue = configuration; serialized.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, AppRootPrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ApplyMainScene(TemporaryCombatConfiguration combatConfiguration, ExplorationRunConfiguration explorationConfiguration)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath); var opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Save or revert unsaved TMP_MainScene changes before applying temporary exploration.");
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
                var menu = components.OfType<GameMenuPanel>().Single(); var story = components.OfType<StoryTextPanel>().Single(); var enemyPanel = components.OfType<EnemyDisplayPanel>().Single();
                var playerHealth = components.OfType<HealthBarPanel>().First(panel => !panel.transform.IsChildOf(enemyPanel.transform)); DisableEnemyDemoLoader(enemyPanel);
                var enemyHealth = EnsureEnemyHealthBar(enemyPanel); var nextAction = EnsureNextActionText(enemyPanel);
                var combat = menu.GetComponent<TemporaryDiceRollMenuController>() ?? menu.gameObject.AddComponent<TemporaryDiceRollMenuController>();
                combat.ConfigureForEditor(menu, story, combatConfiguration, enemyPanel, playerHealth, enemyHealth, nextAction); menu.SetCommandHandlerForEditor(combat);
                EnsureActionButton(menu);
                var ui = EnsureExplorationUi(menu.GetComponentInParent<Canvas>().transform);
                var exploration = menu.GetComponent<ExplorationRunController>() ?? menu.gameObject.AddComponent<ExplorationRunController>();
                exploration.ConfigureForEditor(explorationConfiguration, combat, story, ui.Root, ui.Status, ui.Buttons, ui.Labels, ui.Continue);
                EditorUtility.SetDirty(menu); EditorUtility.SetDirty(combat); EditorUtility.SetDirty(exploration); EditorUtility.SetDirty(enemyPanel);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save TMP_MainScene.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void EnsureActionButton(GameMenuPanel menu)
        {
            var content = menu.Content ?? throw new InvalidOperationException("GameMenuPanel has no Content reference."); var existing = content.Find(ButtonName);
            var view = existing != null ? existing.GetComponent<GameMenuButtonView>() : CreateMenuButton(content);
            if (view == null) throw new InvalidOperationException("Temporary action button has no GameMenuButtonView.");
            var label = view.GetComponentInChildren<TMP_Text>(true); if (label != null) label.text = "행동";
            var binding = new GameMenuButtonBinding(); binding.ConfigureCommandForEditor(TemporaryDiceRollMenuController.RollAllCommandId, view.Button, true, view); menu.AddOrReplaceCommandForEditor(binding);
        }

        private sealed class ExplorationUi { public GameObject Root; public TMP_Text Status; public Button[] Buttons; public TMP_Text[] Labels; public Button Continue; }

        private static ExplorationUi EnsureExplorationUi(Transform parent)
        {
            var existing = parent.Find(ExplorationPanelName);
            if (existing != null)
            {
                existing.SetParent(parent, false);
                var existingRect = (RectTransform)existing;
                existingRect.anchorMin = existingRect.anchorMax = new Vector2(.5f, .5f);
                existingRect.pivot = new Vector2(.5f, .5f);
                existingRect.anchoredPosition = new Vector2(0, 80);
                existingRect.sizeDelta = new Vector2(760, 250);
                return new ExplorationUi { Root = existing.gameObject, Status = existing.Find("Status").GetComponent<TMP_Text>(), Buttons = Enumerable.Range(1, 3).Select(i => existing.Find($"Choices/Choice{i}").GetComponent<Button>()).ToArray(), Labels = Enumerable.Range(1, 3).Select(i => existing.Find($"Choices/Choice{i}/Label").GetComponent<TMP_Text>()).ToArray(), Continue = existing.Find("Continue").GetComponent<Button>() };
            }
            var root = CreateRect(ExplorationPanelName, parent); root.anchorMin = root.anchorMax = new Vector2(.5f,.5f); root.pivot = new Vector2(.5f,.5f); root.anchoredPosition = new Vector2(0,80); root.sizeDelta = new Vector2(760,250);
            var background = root.gameObject.AddComponent<Image>(); background.color = new Color(.07f,.09f,.14f,.96f);
            var statusRect = CreateRect("Status", root); statusRect.anchorMin = new Vector2(0,1); statusRect.anchorMax = Vector2.one; statusRect.offsetMin = new Vector2(24,-72); statusRect.offsetMax = new Vector2(-24,-18); var status = AddText(statusRect,"다음 노드 선택",24);
            var choices = CreateRect("Choices", root); choices.anchorMin = new Vector2(0,0); choices.anchorMax = Vector2.one; choices.offsetMin = new Vector2(24,64); choices.offsetMax = new Vector2(-24,-82); var layout = choices.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 14; layout.childAlignment = TextAnchor.MiddleCenter; layout.childControlWidth = true; layout.childForceExpandWidth = true;
            var buttons = new Button[3]; var labels = new TMP_Text[3]; for(var i=0;i<3;i++){ buttons[i]=CreateSimpleButton($"Choice{i+1}",choices,$"{i+1}. 노드",out labels[i]); }
            var continueRect = CreateRect("Continue", root); continueRect.anchorMin = continueRect.anchorMax = new Vector2(.5f,0); continueRect.pivot = new Vector2(.5f,0); continueRect.anchoredPosition = new Vector2(0,16); continueRect.sizeDelta = new Vector2(180,44); var continueButton = AddButtonVisual(continueRect,"계속",out _); continueRect.gameObject.SetActive(false);
            return new ExplorationUi { Root=root.gameObject, Status=status, Buttons=buttons, Labels=labels, Continue=continueButton };
        }

        private static void DisableEnemyDemoLoader(EnemyDisplayPanel enemyPanel)
        { var startup=enemyPanel.GetComponentInParent<PanelStartupController>(true); if(startup==null)return; var serialized=new SerializedObject(startup); var loader=serialized.FindProperty("initialDataLoader"); if(loader!=null && loader.objectReferenceValue is EnemyDisplayPanelDemoLoader){loader.objectReferenceValue=null; serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(startup);} }

        private static HealthBarPanel EnsureEnemyHealthBar(EnemyDisplayPanel enemyPanel)
        { var existing=enemyPanel.transform.Find(EnemyHealthName); if(existing!=null)return existing.GetComponent<HealthBarPanel>(); var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HealthBarPrefabPath) ?? throw new InvalidOperationException("HealthBarPanel prefab is missing."); var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,enemyPanel.transform); instance.name=EnemyHealthName; var rect=(RectTransform)instance.transform; rect.anchorMin=rect.anchorMax=new Vector2(.5f,1); rect.pivot=new Vector2(.5f,1); rect.anchoredPosition=new Vector2(0,-12); rect.sizeDelta=new Vector2(360,56); return instance.GetComponent<HealthBarPanel>(); }
        private static TMP_Text EnsureNextActionText(EnemyDisplayPanel enemyPanel)
        { var existing=enemyPanel.transform.Find(NextActionName); if(existing!=null)return existing.GetComponent<TMP_Text>(); var rect=CreateRect(NextActionName,enemyPanel.transform); rect.anchorMin=rect.anchorMax=new Vector2(.5f,1); rect.pivot=new Vector2(.5f,1); rect.anchoredPosition=new Vector2(0,-72); rect.sizeDelta=new Vector2(360,34); return AddText(rect,"전투 노드를 선택하세요",20); }
        private static GameMenuButtonView CreateMenuButton(Transform parent)
        { var root=CreateRect(ButtonName,parent); root.sizeDelta=new Vector2(150,48); var visual=CreateRect("VisualRoot",root); Stretch(visual); var bgRect=CreateRect("Background",visual); Stretch(bgRect); var bg=bgRect.gameObject.AddComponent<Image>(); bg.color=new Color(.2f,.24f,.32f,1); var iconRect=CreateRect("Icon",visual); Stretch(iconRect); var icon=iconRect.gameObject.AddComponent<Image>(); icon.raycastTarget=false; icon.gameObject.SetActive(false); var labelRect=CreateRect("Label",visual); Stretch(labelRect,8,6,8,6); var label=AddText(labelRect,"행동",16); label.textWrappingMode=TextWrappingModes.NoWrap; label.overflowMode=TextOverflowModes.Ellipsis; var borderRect=CreateRect("Border",visual); Stretch(borderRect); var border=borderRect.gameObject.AddComponent<Image>(); border.color=new Color(.65f,.72f,.9f,.35f); border.raycastTarget=false; var effect=CreateRect("EffectOverlay",visual); Stretch(effect); var button=root.gameObject.AddComponent<Button>(); button.targetGraphic=bg; var view=root.gameObject.AddComponent<GameMenuButtonView>(); view.ConfigureForEditor(button,visual,bg,icon,label,border,effect); return view; }
        private static Button CreateSimpleButton(string name,Transform parent,string text,out TMP_Text label){var rect=CreateRect(name,parent); rect.sizeDelta=new Vector2(200,64); return AddButtonVisual(rect,text,out label);}
        private static Button AddButtonVisual(RectTransform rect,string value,out TMP_Text label){var image=rect.gameObject.AddComponent<Image>(); image.color=new Color(.18f,.24f,.34f,1); var labelRect=CreateRect("Label",rect); Stretch(labelRect,10,6,10,6); label=AddText(labelRect,value,18); var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image; return button;}
        private static TMP_Text AddText(RectTransform rect,string value,float size){var text=rect.gameObject.AddComponent<TextMeshProUGUI>(); text.text=value; text.fontSize=size; text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false; return text;}
        private static RectTransform CreateRect(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); return (RectTransform)go.transform;}
        private static void Stretch(RectTransform rect,float left=0,float bottom=0,float right=0,float top=0){rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=new Vector2(left,bottom); rect.offsetMax=new Vector2(-right,-top);}
        private static void EnsureFolder(string path){var parts=path.Split('/'); var current=parts[0]; for(var i=1;i<parts.Length;i++){var next=current+"/"+parts[i]; if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]); current=next;}}
    }
}
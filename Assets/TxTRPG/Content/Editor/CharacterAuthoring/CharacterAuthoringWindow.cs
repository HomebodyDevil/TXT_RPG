using System;
using System.Collections.Generic;
using TxTRPG.UI;
using TxTRPG.Editor.Common.Menu;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Content.Characters.Editor
{
    public sealed class CharacterAuthoringWindow : EditorWindow
    {
        private string definitionId = "character.default";
        private string assetName = "DefaultCharacter";
        private string outputFolder =
            "Assets/TxTRPG/Content/Characters/DefaultCharacter";
        private string localizationKey = "characters.default.name";
        private int attackPower = 10;
        private int maxHealth = 100;
        private Sprite defaultSprite;
        private CharacterFramingPreset framingPreset = CharacterFramingPreset.ThighUp;
        private float additionalScale = 1f;
        private Vector2 pixelOffset;
        private readonly List<CharacterVisualVariantInput> visualVariants = new();
        private float injuredHealthRatio = 0.5f;
        private float criticalHealthRatio = 0.2f;
        private float previewHealthRatio = 1f;
        private int previewStateIndex;
        private CharacterContentCatalog catalog;
        private bool registerAddressables = true;
        private string addressablesGroup = "Character_Content";
        private string spriteAddress = "characters/default/appearance/default";
        private Vector2 scrollPosition;
        private string lastResult = string.Empty;

        [MenuItem(
            TxTRPGEditorMenuPaths.CharacterContent + "Open Authoring",
            false,
            TxTRPGEditorMenuPriorities.Authoring)]
        private static void Open()
        {
            GetWindow<CharacterAuthoringWindow>("Character Authoring");
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawIdentity();
            DrawGameplay();
            DrawAppearance();
            DrawVisualStates();
            DrawRegistration();
            var request = BuildRequest();
            var paths = new CharacterContentAssetPaths(
                request.OutputFolder?.Replace('\\', '/').TrimEnd('/') ?? string.Empty,
                request.AssetName?.Trim() ?? string.Empty);
            var errors = CharacterContentCreationValidator.Validate(request, paths);
            DrawValidation(paths, errors);

            using (new EditorGUI.DisabledScope(errors.Count > 0))
            {
                if (GUILayout.Button("Create Character Content", GUILayout.Height(32f)))
                {
                    CreateContent(request);
                }
            }
            if (!string.IsNullOrEmpty(lastResult))
            {
                EditorGUILayout.HelpBox(lastResult, MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawIdentity()
        {
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            definitionId = EditorGUILayout.TextField("Definition ID", definitionId);
            assetName = EditorGUILayout.TextField("Asset Name", assetName);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            localizationKey = EditorGUILayout.TextField("Localization Key", localizationKey);
            EditorGUILayout.Space();
        }

        private void DrawGameplay()
        {
            EditorGUILayout.LabelField("Gameplay", EditorStyles.boldLabel);
            attackPower = EditorGUILayout.IntField("Attack Power", attackPower);
            maxHealth = EditorGUILayout.IntField("Max Health", maxHealth);
            EditorGUILayout.Space();
        }

        private void DrawAppearance()
        {
            EditorGUILayout.LabelField("Default Appearance", EditorStyles.boldLabel);
            defaultSprite = (Sprite)EditorGUILayout.ObjectField(
                "Default Sprite",
                defaultSprite,
                typeof(Sprite),
                false);
            framingPreset = (CharacterFramingPreset)EditorGUILayout.EnumPopup(
                "Framing Preset",
                framingPreset);
            additionalScale = EditorGUILayout.FloatField("Additional Scale", additionalScale);
            pixelOffset = EditorGUILayout.Vector2Field("Pixel Offset", pixelOffset);
            DrawSpritePreview(defaultSprite);
            EditorGUILayout.Space();
        }

        private void DrawVisualStates()
        {
            EditorGUILayout.LabelField("Visual State Policy", EditorStyles.boldLabel);
            criticalHealthRatio = EditorGUILayout.Slider(
                "Critical Health Ratio",
                criticalHealthRatio,
                0f,
                1f);
            injuredHealthRatio = EditorGUILayout.Slider(
                "Injured Health Ratio",
                injuredHealthRatio,
                0f,
                1f);

            EditorGUILayout.LabelField("Visual State Images", EditorStyles.boldLabel);
            var removeIndex = -1;
            for (var index = 0; index < visualVariants.Count; index++)
            {
                var variant = visualVariants[index];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Variant {index + 1}", EditorStyles.boldLabel);
                        if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                        {
                            removeIndex = index;
                        }
                    }
                    variant.VisualStateId = EditorGUILayout.TextField(
                        "Visual State ID",
                        variant.VisualStateId);
                    variant.AppearanceId = EditorGUILayout.TextField(
                        "Appearance ID",
                        variant.AppearanceId);
                    variant.PoseId = EditorGUILayout.TextField("Pose ID", variant.PoseId);
                    variant.ExpressionId = EditorGUILayout.TextField(
                        "Expression ID",
                        variant.ExpressionId);
                    variant.Sprite = (Sprite)EditorGUILayout.ObjectField(
                        "Sprite",
                        variant.Sprite,
                        typeof(Sprite),
                        false);
                    variant.FramingPreset = (CharacterFramingPreset)EditorGUILayout.EnumPopup(
                        "Framing Preset",
                        variant.FramingPreset);
                    variant.AdditionalScale = EditorGUILayout.FloatField(
                        "Additional Scale",
                        variant.AdditionalScale);
                    variant.PixelOffset = EditorGUILayout.Vector2Field(
                        "Pixel Offset",
                        variant.PixelOffset);
                    variant.Address = EditorGUILayout.TextField("Address", variant.Address);
                }
            }
            if (removeIndex >= 0)
            {
                visualVariants.RemoveAt(removeIndex);
                previewStateIndex = 0;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add State Image"))
                {
                    visualVariants.Add(CreateVariant(string.Empty));
                }
                if (GUILayout.Button("Add Standard Health States"))
                {
                    AddStandardHealthStates();
                }
            }

            DrawStatePreview();
            EditorGUILayout.Space();
        }

        private void DrawStatePreview()
        {
            previewHealthRatio = EditorGUILayout.Slider(
                "Health Preview",
                previewHealthRatio,
                0f,
                1f);
            var stateOptions = new string[visualVariants.Count + 1];
            stateOptions[0] = "From Health";
            for (var index = 0; index < visualVariants.Count; index++)
            {
                stateOptions[index + 1] = string.IsNullOrWhiteSpace(
                    visualVariants[index].VisualStateId)
                    ? $"Variant {index + 1}"
                    : visualVariants[index].VisualStateId.Trim();
            }
            previewStateIndex = Mathf.Clamp(previewStateIndex, 0, stateOptions.Length - 1);
            previewStateIndex = EditorGUILayout.Popup(
                "Preview State",
                previewStateIndex,
                stateOptions);

            var stateId = previewStateIndex == 0
                ? ResolvePreviewHealthState()
                : visualVariants[previewStateIndex - 1].VisualStateId?.Trim();
            EditorGUILayout.LabelField("Resolved State", stateId);
            var previewSprite = defaultSprite;
            foreach (var variant in visualVariants)
            {
                if (string.Equals(
                    variant.VisualStateId?.Trim(),
                    stateId,
                    StringComparison.Ordinal))
                {
                    previewSprite = variant.Sprite != null ? variant.Sprite : defaultSprite;
                    break;
                }
            }
            DrawSpritePreview(previewSprite);
        }

        private string ResolvePreviewHealthState()
        {
            if (previewHealthRatio <= 0f)
            {
                return CharacterVisualStatePolicy.DefeatedStateId;
            }
            if (previewHealthRatio <= criticalHealthRatio)
            {
                return CharacterVisualStatePolicy.CriticalStateId;
            }
            if (previewHealthRatio <= injuredHealthRatio)
            {
                return CharacterVisualStatePolicy.InjuredStateId;
            }
            return CharacterVisualStatePolicy.NormalStateId;
        }

        private void AddStandardHealthStates()
        {
            AddStandardHealthState(CharacterVisualStatePolicy.NormalStateId);
            AddStandardHealthState(CharacterVisualStatePolicy.InjuredStateId);
            AddStandardHealthState(CharacterVisualStatePolicy.CriticalStateId);
            AddStandardHealthState(CharacterVisualStatePolicy.DefeatedStateId);
        }

        private void AddStandardHealthState(string stateId)
        {
            if (visualVariants.Exists(variant => string.Equals(
                    variant.VisualStateId?.Trim(),
                    stateId,
                    StringComparison.Ordinal)))
            {
                return;
            }
            visualVariants.Add(CreateVariant(stateId));
        }

        private CharacterVisualVariantInput CreateVariant(string stateId)
        {
            var safeId = string.IsNullOrWhiteSpace(definitionId)
                ? "character"
                : definitionId.Trim().Replace("character.", string.Empty);
            return new CharacterVisualVariantInput
            {
                VisualStateId = stateId,
                FramingPreset = framingPreset,
                AdditionalScale = additionalScale,
                PixelOffset = pixelOffset,
                Address = $"characters/{safeId}/artwork/{stateId}"
            };
        }

        private static void DrawSpritePreview(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }
            var preview = AssetPreview.GetAssetPreview(sprite) ??
                AssetPreview.GetMiniThumbnail(sprite);
            if (preview == null)
            {
                return;
            }
            var rect = GUILayoutUtility.GetAspectRect(1.6f, GUILayout.MaxHeight(180f));
            GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit, true);
        }

        private void DrawRegistration()
        {
            EditorGUILayout.LabelField("Registration", EditorStyles.boldLabel);
            catalog = (CharacterContentCatalog)EditorGUILayout.ObjectField(
                "Character Catalog",
                catalog,
                typeof(CharacterContentCatalog),
                false);
            registerAddressables = EditorGUILayout.Toggle(
                "Register Addressables",
                registerAddressables);
            using (new EditorGUI.DisabledScope(!registerAddressables))
            {
                addressablesGroup = EditorGUILayout.TextField(
                    "Addressables Group",
                    addressablesGroup);
            }
            spriteAddress = EditorGUILayout.TextField("Sprite Address", spriteAddress);
            EditorGUILayout.Space();
        }

        private static void DrawValidation(
            CharacterContentAssetPaths paths,
            IReadOnlyList<string> errors)
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Gameplay", paths.Gameplay, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Appearance", paths.Appearance, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Content", paths.Content, EditorStyles.wordWrappedLabel);
            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("All inputs are valid.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
            }
        }

        private CharacterContentCreationRequest BuildRequest()
        {
            return new CharacterContentCreationRequest
            {
                DefinitionId = definitionId,
                AssetName = assetName,
                OutputFolder = outputFolder,
                DisplayNameLocalizationKey = localizationKey,
                AttackPower = attackPower,
                MaxHealth = maxHealth,
                DefaultSprite = defaultSprite,
                FramingPreset = framingPreset,
                AdditionalScale = additionalScale,
                PixelOffset = pixelOffset,
                Catalog = catalog,
                RegisterSpriteWithAddressables = registerAddressables,
                AddressablesGroup = addressablesGroup,
                SpriteAddress = spriteAddress,
                VisualVariants = new List<CharacterVisualVariantInput>(visualVariants),
                InjuredHealthRatio = injuredHealthRatio,
                CriticalHealthRatio = criticalHealthRatio
            };
        }

        private void CreateContent(CharacterContentCreationRequest request)
        {
            try
            {
                var result = new CharacterContentAssetFactory().Create(request);
                Selection.activeObject = result.ContentDefinition;
                EditorGUIUtility.PingObject(result.ContentDefinition);
                lastResult = $"Created character content at {result.ContentAssetPath}.";
            }
            catch (Exception exception)
            {
                lastResult = string.Empty;
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Character Content Creation Failed",
                    exception.Message,
                    "OK");
            }
        }
    }
}

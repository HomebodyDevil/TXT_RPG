using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TxTRPG.Gameplay;
using TxTRPG.Gameplay.Encounters;
using TxTRPG.UI.Encounters;
using TxTRPG.UI.Windows;
using UnityEngine;

namespace TxTRPG.Application.Encounters
{
    // Scene-lifetime owner of the encounter element selection and the running roll settings. It fills the toggle bar
    // from the catalog, keeps the selection (the bar only displays it), rolls with EncounterRoller, shows an occurred
    // result in the result modal and raises RollCompleted for every roll. The debug panel is optional (it is disabled in
    // Release players) and talks to this only through the public API. Missing catalog, configuration or bar stops this
    // component with one error and never blocks the rest of the scene. Selection and settings are never saved.
    [DisallowMultipleComponent]
    public sealed class EncounterRollController : MonoBehaviour
    {
        public const string ResultPageId = "encounter-result";
        public const string ResultTitle = "이벤트 발생";

        [SerializeField] private EncounterElementCatalog catalog;
        [SerializeField] private EncounterRollConfiguration configuration;
        [SerializeField] private EncounterElementToggleBar toggleBar;
        [SerializeField] private GameWindowService resultWindow;
        [SerializeField] private MessageGameWindowPage resultPage;
        [SerializeField] private EncounterDebugPanel debugPanel;

        private readonly EncounterElementSelection selection = new();
        private IReadOnlyList<EncounterElementToggleEntry> entries = Array.Empty<EncounterElementToggleEntry>();
        private IRandomIndexProvider random;
        private EncounterRoller roller;
        private EncounterRollSettings settings = EncounterRollSettings.Default;
        private bool bound;
        private bool reportedMissing;

        public event Action<EncounterRollResult> RollCompleted;

        public EncounterElementSelection Selection => selection;
        public EncounterRollSettings CurrentSettings => settings;
        public IReadOnlyList<EncounterElementToggleEntry> Entries => entries;
        public bool IsReady { get; private set; }
        public EncounterElementCatalog Catalog => catalog;
        public EncounterRollConfiguration Configuration => configuration;
        public EncounterElementToggleBar ToggleBar => toggleBar;
        public GameWindowService ResultWindow => resultWindow;
        public MessageGameWindowPage ResultPage => resultPage;
        public EncounterDebugPanel DebugPanel => debugPanel;
        public bool HasResultModal => resultWindow != null && resultPage != null && resultWindow.HasPage(ResultPageId);

        private void Awake() => Initialize();
        private void OnDestroy() => Unbind();

        // Called from Awake. Rebuilds the buttons from the catalog, clears the selection and loads the configured settings.
        public bool Initialize()
        {
            Unbind();
            IsReady = false;
            if (catalog == null || configuration == null || toggleBar == null)
            {
                if (!reportedMissing)
                    Debug.LogError("EncounterRollController is missing its catalog, roll configuration or toggle bar; encounter rolls are disabled.", this);
                reportedMissing = true;
                return false;
            }
            if (!catalog.Validate(out var problems))
                Debug.LogWarning($"EncounterElementCatalog '{catalog.name}' has invalid entries that get no button: {string.Join("; ", problems)}", catalog);
            random ??= new SystemRandomIndexProvider();
            roller = new EncounterRoller(random);
            entries = catalog.CreateToggleEntries();
            settings = configuration.CreateSettings();
            selection.Clear();
            toggleBar.SetEntries(entries);
            Bind();
            SyncToggleBar();
            PushDebugValues();
            if (debugPanel != null) debugPanel.ShowResult(string.Empty);
            if (!HasResultModal)
                Debug.LogWarning($"EncounterRollController has no result modal with page '{ResultPageId}'; results go to the log and the debug panel.", this);
            IsReady = true;
            return true;
        }

        public void SetSettings(EncounterRollSettings value)
        {
            settings = value ?? EncounterRollSettings.Default;
            PushDebugValues();
        }

        public void ResetSettingsToConfiguration() =>
            SetSettings(configuration != null ? configuration.CreateSettings() : EncounterRollSettings.Default);

        // Selected ids in catalog (button) order.
        public IReadOnlyList<string> SelectedElementIds()
        {
            var result = new List<string>(selection.Count);
            foreach (var entry in entries)
                if (selection.IsSelected(entry.Id)) result.Add(entry.Id);
            return result;
        }

        // Rolls now. Occurred opens the result modal (focus returns to returnFocus when it closes). Every roll raises
        // RollCompleted. Returns null without rolling while the result modal is open or when this component is disabled.
        public EncounterRollResult RollNow(GameObject returnFocus = null)
        {
            if (!IsReady)
            {
                Debug.LogWarning("EncounterRollController is not ready; the roll was skipped.", this);
                return null;
            }
            if (resultWindow != null && resultWindow.IsOpen)
            {
                Debug.LogWarning("The encounter result modal is open; close it before rolling again.", this);
                return null;
            }
            var result = roller.Roll(SelectedElementIds(), settings);
            var message = EncounterResultFormatter.Format(result, catalog);
            if (debugPanel != null) debugPanel.ShowResult(message);
            if (result.Occurred)
            {
                if (HasResultModal) _ = OpenResultAsync(message, returnFocus);
                else Debug.LogWarning($"Encounter: {message} (no result modal)", this);
            }
            RollCompleted?.Invoke(result);
            return result;
        }

        private async Task OpenResultAsync(string message, GameObject returnFocus)
        {
            try
            {
                resultPage.SetMessage(message);
                if (!await resultWindow.OpenAsync(ResultPageId, returnFocus))
                    Debug.LogWarning($"Encounter result modal did not open. Result: {message}", this);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Encounter result modal failed ({exception.Message}). Result: {message}", this);
            }
        }

        private void Bind()
        {
            if (bound) return;
            toggleBar.SelectionChanged += OnToggleChanged;
            selection.Changed += SyncToggleBar;
            if (debugPanel != null)
            {
                debugPanel.ValuesChanged += OnDebugValuesChanged;
                debugPanel.RollRequested += OnDebugRollRequested;
                debugPanel.ResetRequested += ResetSettingsToConfiguration;
            }
            bound = true;
        }

        private void Unbind()
        {
            if (!bound) return;
            if (toggleBar != null) toggleBar.SelectionChanged -= OnToggleChanged;
            selection.Changed -= SyncToggleBar;
            if (debugPanel != null)
            {
                debugPanel.ValuesChanged -= OnDebugValuesChanged;
                debugPanel.RollRequested -= OnDebugRollRequested;
                debugPanel.ResetRequested -= ResetSettingsToConfiguration;
            }
            bound = false;
        }

        private void OnToggleChanged(string id, bool isOn)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            selection.SetSelected(id, isOn);
        }

        private void SyncToggleBar()
        {
            if (toggleBar == null) return;
            foreach (var entry in entries) toggleBar.SetSelected(entry.Id, selection.IsSelected(entry.Id));
        }

        private void OnDebugValuesChanged(EncounterDebugValues values) =>
            settings = new EncounterRollSettings(values.OccurrenceChance, values.InclusionChance, values.CombatWeight, values.EventWeight, values.RestWeight);

        private void OnDebugRollRequested() =>
            RollNow(debugPanel != null && debugPanel.RollButton != null ? debugPanel.RollButton.gameObject : null);

        private void PushDebugValues()
        {
            if (debugPanel == null) return;
            debugPanel.SetValues(new EncounterDebugValues(settings.OccurrenceChance, settings.ElementInclusionChance,
                settings.CombatWeight, settings.EventWeight, settings.RestWeight));
        }

#if UNITY_EDITOR
        // Editor/test setup. Call Initialize afterwards (Awake does not run in Edit Mode). randomProvider null keeps System.Random.
        public void ConfigureForEditor(EncounterElementCatalog elementCatalog, EncounterRollConfiguration rollConfiguration,
            EncounterElementToggleBar bar, GameWindowService window, MessageGameWindowPage page, EncounterDebugPanel panel,
            IRandomIndexProvider randomProvider = null)
        {
            Unbind();
            catalog = elementCatalog; configuration = rollConfiguration; toggleBar = bar;
            resultWindow = window; resultPage = page; debugPanel = panel;
            random = randomProvider;
            reportedMissing = false;
        }
#endif
    }
}

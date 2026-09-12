using TxTRPG.UI;
using UnityEngine;

namespace TxTRPG.Application.Presentation
{
    public enum MainScenePreviewScenario
    {
        Normal,
        MissingConfiguration,
        ReadyEmpty,
        PartialAssetFailure,
        DelayedRecovery,
        InitializationFailure
    }

    [CreateAssetMenu(menuName = "TxT RPG/Application/Main Scene Preview Profile", fileName = "MainScenePreviewProfile")]
    public sealed class MainScenePreviewProfile : ScriptableObject
    {
        [SerializeField] private MainScenePreviewScenario scenario;
        [SerializeField] private StoryTextPanelDemoData storyData;
        [SerializeField] private EnemyDisplayPanelDemoData enemyData;
        [SerializeField, Min(0f)] private float simulatedDelay = 0.5f;

        public MainScenePreviewScenario Scenario => scenario;
        public StoryTextPanelDemoData StoryData => storyData;
        public EnemyDisplayPanelDemoData EnemyData => enemyData;
        public float SimulatedDelay => Mathf.Max(0f, simulatedDelay);

#if UNITY_EDITOR
        public void ConfigureForEditor(MainScenePreviewScenario value,
            StoryTextPanelDemoData story, EnemyDisplayPanelDemoData enemy, float delay)
        {
            scenario = value;
            storyData = story;
            enemyData = enemy;
            simulatedDelay = Mathf.Max(0f, delay);
        }
#endif
    }
}

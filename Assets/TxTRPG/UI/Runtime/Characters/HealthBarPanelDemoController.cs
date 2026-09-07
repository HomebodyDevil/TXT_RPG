using System.Collections;
using UnityEngine;

namespace TxTRPG.UI
{
    [DisallowMultipleComponent]
    public sealed class HealthBarPanelDemoController : MonoBehaviour
    {
        [SerializeField] private HealthBarPanel panel;
        [SerializeField] private HealthBarLayoutController layout;
        [SerializeField, Min(0.1f)] private float stepDuration = 1.25f;
        private Coroutine routine;

        public void Configure(HealthBarPanel targetPanel, HealthBarLayoutController targetLayout)
        {
            panel = targetPanel;
            layout = targetLayout;
        }

        private void OnEnable()
        {
            if (Application.isPlaying && panel != null && layout != null)
                routine = StartCoroutine(PlayDemo());
        }

        private void OnDisable()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
        }

        private IEnumerator PlayDemo()
        {
            var alignments = new[]
            {
                (HealthBarHorizontalAlignment.Left, HealthBarVerticalAlignment.Bottom),
                (HealthBarHorizontalAlignment.Center, HealthBarVerticalAlignment.Middle),
                (HealthBarHorizontalAlignment.Right, HealthBarVerticalAlignment.Top)
            };
            var health = 100;
            panel.Apply(CreatePresentation(health));
            var index = 0;
            while (true)
            {
                yield return new WaitForSecondsRealtime(stepDuration);
                var alignment = alignments[index++ % alignments.Length];
                layout.SetAlignment(alignment.Item1, alignment.Item2);
                health = health <= 25 ? 100 : health - 25;
                panel.Apply(CreatePresentation(health));
            }
        }

        private static CharacterStatusPresentation CreatePresentation(int health) =>
            new(
                new CharacterNamePresentation(string.Empty),
                new HealthPresentation(health, 100, "HP", $"{health} / 100"));
    }
}

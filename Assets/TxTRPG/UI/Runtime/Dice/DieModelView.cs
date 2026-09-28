using System;
using System.Collections.Generic;
using TMPro;
using TxTRPG.Gameplay.Dice;
using UnityEngine;

namespace TxTRPG.UI.Dice
{
    public sealed class DieModelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text[] faceLabels = Array.Empty<TMP_Text>();
        public int LabelCount => faceLabels?.Length ?? 0;
        public void SetFaces(IReadOnlyList<DiceFace> faces)
        {
            if (faces == null || faces.Count != LabelCount)
                throw new ArgumentException("The model label count must match the die face count.", nameof(faces));
            for (var i = 0; i < faces.Count; i++)
                faceLabels[i].text = faces[i].EffectKind switch
                {
                    DiceEffectKind.Attack => $"공{faces[i].Amount}",
                    DiceEffectKind.Heal => $"회{faces[i].Amount}",
                    _ => $"? {faces[i].Amount}"
                };
        }
#if UNITY_EDITOR
        public void ConfigureForEditor(TMP_Text[] labels) => faceLabels = labels;
#endif
    }
}

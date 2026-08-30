using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    [CreateAssetMenu(menuName = "TxT RPG/UI/Scrollbar Style", fileName = "ScrollbarStyle")]
    public sealed class ScrollbarStyle : ScriptableObject
    {
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Color backgroundColor = new(1f, 1f, 1f, 0.08f);
        [SerializeField] private Material backgroundMaterial;
        [SerializeField] private bool showBackground = true;
        [SerializeField] private Sprite handleSprite;
        [SerializeField] private Color handleColor = new(0.84f, 0.54f, 0.29f, 0.9f);
        [SerializeField] private Material handleMaterial;

        public void Apply(Image background, Image handle)
        {
            if (background != null)
            {
                background.sprite = backgroundSprite;
                background.color = backgroundColor;
                background.material = backgroundMaterial;
                background.enabled = showBackground;
            }
            if (handle != null)
            {
                handle.sprite = handleSprite;
                handle.color = handleColor;
                handle.material = handleMaterial;
            }
        }
    }
}

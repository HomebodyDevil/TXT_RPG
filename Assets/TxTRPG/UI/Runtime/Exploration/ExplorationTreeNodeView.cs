using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TxTRPG.UI.Exploration
{
    public sealed class ExplorationTreeNodeView : MonoBehaviour
    {
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private CanvasGroup visualAlpha;
        [SerializeField] private Image icon, background, effectOverlay;
        [SerializeField] private TMP_Text title, status;
        public string NodeId { get; private set; }
        public string RunId { get; private set; }
        public int Generation { get; private set; }
        public RectTransform VisualRoot=>visualRoot;
        public Image Icon=>icon;
        public TMP_Text Title=>title;
        public void Bind(string runId,ExplorationTreePlacement placement,ExplorationTreeStyle style)
        {
            if(RunId!=runId||NodeId!=placement.Node.Id){ResetEffects();Generation++;}
            RunId=runId;NodeId=placement.Node.Id;
            var rect=(RectTransform)transform;rect.anchoredPosition=placement.Center;rect.sizeDelta=placement.Size;
            style.Resolve(placement.Node.TypeId,out var name,out var sprite);
            title.text=placement.Node.IsRoot?"시작":name; status.text=placement.Node.IsRoot?string.Empty:ExplorationTreeStyle.Label(placement.Node.Status);
            icon.sprite=sprite;icon.preserveAspect=true;icon.gameObject.SetActive(!placement.Node.IsRoot);
            status.gameObject.SetActive(!placement.Node.IsRoot); background.color=style.ColorFor(placement.Node.Status);
            title.rectTransform.anchorMin=Vector2.zero;title.rectTransform.anchorMax=Vector2.one;
            title.rectTransform.offsetMin=new Vector2(3,placement.Node.IsRoot?0:28);title.rectTransform.offsetMax=new Vector2(-3,placement.Node.IsRoot?0:-64);
            icon.rectTransform.anchorMin=new Vector2(.2f,1);icon.rectTransform.anchorMax=new Vector2(.8f,1);icon.rectTransform.pivot=new Vector2(.5f,1);icon.rectTransform.sizeDelta=new Vector2(0,52);icon.rectTransform.anchoredPosition=new Vector2(0,-4);
            status.rectTransform.anchorMin=Vector2.zero;status.rectTransform.anchorMax=new Vector2(1,0);status.rectTransform.pivot=new Vector2(.5f,0);status.rectTransform.sizeDelta=new Vector2(-4,26);status.rectTransform.anchoredPosition=new Vector2(0,1);
        }
        public void ResetEffects()
        {
            StopAllCoroutines();
            if(visualRoot!=null){visualRoot.localScale=Vector3.one;visualRoot.localRotation=Quaternion.identity;visualRoot.anchoredPosition=Vector2.zero;}
            if(visualAlpha!=null)visualAlpha.alpha=1;
            if(effectOverlay!=null)effectOverlay.color=Color.clear;
            if(icon!=null)icon.color=Color.white;
        }
        private void OnDisable(){Generation++;ResetEffects();}
#if UNITY_EDITOR
        public void Configure(RectTransform visual,CanvasGroup alpha,Image image,Image bg,Image overlay,TMP_Text name,TMP_Text badge)
        {visualRoot=visual;visualAlpha=alpha;icon=image;background=bg;effectOverlay=overlay;title=name;status=badge;}
#endif
    }
}

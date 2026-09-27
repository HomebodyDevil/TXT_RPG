using UnityEngine;
using UnityEngine.UI;
namespace TxTRPG.UI.Exploration
{
    public sealed class ExplorationTreeEdgeView : MonoBehaviour
    {
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private Image line, effectOverlay;
        public string RunId { get; private set; }
        public string ParentId { get; private set; }
        public string NodeId { get; private set; }
        public int Generation { get; private set; }
        public RectTransform VisualRoot=>visualRoot;
        public Image Line=>line;
        private Color boundColor=Color.white;
        public void Bind(string runId,string parentId,string nodeId,Vector2 from,Vector2 to,bool visited,ExplorationTreeStyle style)
        {
            if(RunId!=runId||ParentId!=parentId||NodeId!=nodeId){ResetEffects();Generation++;}
            RunId=runId;ParentId=parentId;NodeId=nodeId;
            var delta=to-from; var rect=(RectTransform)transform;
            rect.anchoredPosition=(from+to)*.5f;rect.sizeDelta=new Vector2(delta.magnitude,Mathf.Max(1,visited?style.visitedLineWidth:style.lineWidth));
            rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            line.sprite=style.lineSprite;line.type=Image.Type.Simple;line.preserveAspect=false;boundColor=visited?style.visitedLineColor:style.lineColor;line.color=boundColor;
        }
        public void ResetEffects(){StopAllCoroutines();if(line!=null)line.color=boundColor;if(visualRoot!=null){visualRoot.localScale=Vector3.one;visualRoot.localRotation=Quaternion.identity;visualRoot.anchoredPosition=Vector2.zero;}if(effectOverlay!=null)effectOverlay.color=Color.clear;}
        private void OnDisable(){Generation++;ResetEffects();}
#if UNITY_EDITOR
        public void Configure(RectTransform visual,Image image,Image overlay){visualRoot=visual;line=image;effectOverlay=overlay;}
#endif
    }
}

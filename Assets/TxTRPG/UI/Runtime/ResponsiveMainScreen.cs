using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TxTRPG.UI.Exploration;
using TxTRPG.UI.Windows;
namespace TxTRPG.UI
{
    /// <summary>Preserves authored wide layout; stacks the same panels inside a page ScrollRect on narrow screens.</summary>
    public sealed class ResponsiveMainScreen : MonoBehaviour
    {
        [SerializeField] private ScrollRect pageScroll;
        [SerializeField] private FlexibleLayoutPanel mainPanel;
        [SerializeField] private FlexibleLayoutItem[] panels=Array.Empty<FlexibleLayoutItem>();
        [SerializeField] private float[] narrowHeights={360,520,640};
        [SerializeField] private float breakpoint=720;
        [SerializeField] private ExplorationNodeChoiceCardList choices;
        [SerializeField] private ScrollRect choiceScroll;
        [SerializeField] private GameMenuPanel menu;
        [SerializeField] private FlexibleLayoutItem menuAllocation;
        private FlexibleLayoutSizeRequest authoredMenu;
        private FlexibleLayoutSizeRequest[] authored;
        private Vector2 anchorMin,anchorMax,pivot,position,size,previousSize;
        private FlexibleLayoutAxis wideAxis;
        private Rect previousSafeArea;private int screenWidth,screenHeight;
        private bool ready,narrow;private GameObject lastFocus;
        private float cardWidth;private readonly Vector3[] corners=new Vector3[4];
        public ScrollRect PageScroll=>pageScroll;
        public bool IsNarrow=>narrow;
        private void Awake()
        {
            if(pageScroll==null||mainPanel==null||panels.Length!=narrowHeights.Length){Debug.LogError("Responsive screen references are incomplete.",this);enabled=false;return;}
            var rect=(RectTransform)mainPanel.transform;anchorMin=rect.anchorMin;anchorMax=rect.anchorMax;pivot=rect.pivot;position=rect.anchoredPosition;size=rect.sizeDelta;
            authored=new FlexibleLayoutSizeRequest[panels.Length];for(var i=0;i<panels.Length;i++)authored[i]=new FlexibleLayoutSizeRequest(panels[i].SizeMode,panels[i].Weight,panels[i].FixedSize,panels[i].MinimumSize,panels[i].MaximumSize);
            if(menuAllocation!=null)authoredMenu=new FlexibleLayoutSizeRequest(menuAllocation.SizeMode,menuAllocation.Weight,menuAllocation.FixedSize,menuAllocation.MinimumSize,menuAllocation.MaximumSize);
            wideAxis=mainPanel.CurrentAxis;cardWidth=choices.Layout.CardWidth;ready=true;Apply();
        }
        private void LateUpdate()
        {
            if(!ready)return;
            var safe=Screen.safeArea;
            if(safe!=previousSafeArea||Screen.width!=screenWidth||Screen.height!=screenHeight)
            {
                previousSafeArea=safe;screenWidth=Screen.width;screenHeight=Screen.height;
                if(screenWidth>0&&screenHeight>0){var r=pageScroll.viewport;r.anchorMin=new Vector2(safe.xMin/screenWidth,safe.yMin/screenHeight);r.anchorMax=new Vector2(safe.xMax/screenWidth,safe.yMax/screenHeight);}
            }
            if(pageScroll.viewport.rect.size!=previousSize)Apply();
            if(narrow&&choiceScroll!=null)
            {
                var width=Mathf.Clamp(choiceScroll.viewport.rect.width-choices.Layout.padding.horizontal,120,cardWidth);
                if(!Mathf.Approximately(width,choices.Layout.CardWidth))choices.Layout.SetCardSize(new Vector2(width,choices.Layout.CardHeight));
            }
            if(narrow&&menu!=null&&menuAllocation!=null)
            {
                var required=Mathf.Max(authoredMenu.FixedSize,menu.CurrentLayout.RequiredHeight+((RectTransform)menu.transform).rect.height-menu.Viewport.rect.height);
                if(!Mathf.Approximately(required,menuAllocation.FixedSize)){menuAllocation.Configure(FlexibleLayoutSizeMode.Fixed,authoredMenu.Weight,required,required,required);mainPanel.Rebuild();UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild((RectTransform)menuAllocation.transform.parent);}
            }
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            if(selected!=lastFocus){lastFocus=selected;if(narrow&&selected!=null&&selected.transform.IsChildOf(mainPanel.transform)&&selected.transform is RectTransform target)Reveal(target);}
        }
        private void Apply()
        {
            previousSize=pageScroll.viewport.rect.size;var next=previousSize.x<breakpoint;var changed=next!=narrow;narrow=next;
            var rect=(RectTransform)mainPanel.transform;pageScroll.StopMovement();pageScroll.vertical=narrow;pageScroll.horizontal=false;
            if(narrow)
            {
                mainPanel.SetAxis(FlexibleLayoutAxis.Vertical);var height=mainPanel.ContentPadding.vertical+mainPanel.ContentLayout.Spacing*Mathf.Max(0,panels.Length-1);
                for(var i=0;i<panels.Length;i++){var h=Mathf.Max(160,narrowHeights[i]);height+=h;panels[i].Configure(FlexibleLayoutSizeMode.Fixed,authored[i].Weight,h,h,h);}
                var content=mainPanel.ContentRoot;height+=content.offsetMin.y-content.offsetMax.y;
                rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(size.x,Mathf.Max(previousSize.y-20,height));rect.anchoredPosition=new Vector2(position.x,-10);
                pageScroll.verticalNormalizedPosition=1;
            }
            else
            {
                mainPanel.SetAxis(wideAxis);for(var i=0;i<panels.Length;i++){var a=authored[i];panels[i].Configure(a.Mode,a.Weight,a.FixedSize,a.MinimumSize,a.MaximumSize);}
                rect.anchorMin=anchorMin;rect.anchorMax=anchorMax;rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=position;
                if(menuAllocation!=null){var a=authoredMenu;menuAllocation.Configure(a.Mode,a.Weight,a.FixedSize,a.MinimumSize,a.MaximumSize);}
                if(choices!=null)choices.Layout.SetCardSize(new Vector2(cardWidth,choices.Layout.CardHeight));
            }
            mainPanel.Rebuild();if(changed)lastFocus=null;
        }
        private void Reveal(RectTransform target)
        {
            target.GetWorldCorners(corners);var viewport=pageScroll.viewport;
            var bottom=viewport.InverseTransformPoint(corners[0]).y;var top=viewport.InverseTransformPoint(corners[1]).y;
            var delta=top>viewport.rect.yMax?viewport.rect.yMax-top:bottom<viewport.rect.yMin?viewport.rect.yMin-bottom:0;
            if(delta!=0){pageScroll.StopMovement();pageScroll.content.position+=viewport.TransformVector(new Vector3(0,delta));}
        }
#if UNITY_EDITOR
        public void Configure(ScrollRect scroll,FlexibleLayoutPanel main,FlexibleLayoutItem[] items,ExplorationNodeChoiceCardList cards,ScrollRect cardScroll,GameMenuPanel targetMenu,FlexibleLayoutItem menuItem){pageScroll=scroll;mainPanel=main;panels=items;choices=cards;choiceScroll=cardScroll;menu=targetMenu;menuAllocation=menuItem;}
#endif
    }
}

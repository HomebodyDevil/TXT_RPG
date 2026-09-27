using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace TxTRPG.UI
{
    /// <summary>Hands vertical gestures to the containing page only when the inner scroll has reached its boundary.</summary>
    public sealed class NestedScrollRectBridge : MonoBehaviour, IBeginDragHandler, IScrollHandler
    {
        [SerializeField] private ScrollRect inner,outer;
        private bool wasTop,wasBottom;
        private void LateUpdate(){if(inner!=null){wasTop=inner.verticalNormalizedPosition>=.999f;wasBottom=inner.verticalNormalizedPosition<=.001f;}}
        private bool CanForward(float direction)
        {
            if(outer==null||!outer.vertical||!outer.isActiveAndEnabled)return false;
            if(!inner.vertical||inner.content.rect.height<=inner.viewport.rect.height+1)return true;
            return direction<0?inner.verticalNormalizedPosition>=.999f:inner.verticalNormalizedPosition<=.001f;
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if(Mathf.Abs(data.delta.y)<Mathf.Abs(data.delta.x)||!CanForward(data.delta.y))return;
            inner.OnEndDrag(data);data.pointerDrag=outer.gameObject;outer.OnInitializePotentialDrag(data);outer.OnBeginDrag(data);
        }
        public void OnScroll(PointerEventData data)
        {
            if(outer==null||!outer.vertical||!outer.isActiveAndEnabled)return;
            var fits=!inner.vertical||inner.content.rect.height<=inner.viewport.rect.height+1;
            if(fits||data.scrollDelta.y>0&&wasTop||data.scrollDelta.y<0&&wasBottom)outer.OnScroll(data);
        }
#if UNITY_EDITOR
        public void Configure(ScrollRect child,ScrollRect parent){inner=child;outer=parent;}
#endif
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TxTRPG.UI.Windows;
namespace TxTRPG.UI.Exploration
{
    public sealed class ExplorationTreeNavigation : Selectable, ISubmitHandler, ICancelHandler
    {
        [SerializeField] private ExplorationNodeTreePanel panel;
        [SerializeField] private Button returnButton;
        [SerializeField] private GameWindowService windows;
        public override void OnMove(AxisEventData data)
        {
            if(windows!=null&&windows.BlocksGameplayInput)return;
            panel.ScrollBy(new Vector2(-data.moveVector.x,-data.moveVector.y)*100f);data.Use();
        }
        public void OnSubmit(BaseEventData data){if(windows!=null&&windows.BlocksGameplayInput)return;panel.GoToLatest();data.Use();}
        public void OnCancel(BaseEventData data){if(windows!=null&&windows.BlocksGameplayInput)return;if(EventSystem.current!=null&&returnButton!=null)EventSystem.current.SetSelectedGameObject(returnButton.gameObject);data.Use();}
#if UNITY_EDITOR
        public void Configure(ExplorationNodeTreePanel target,Button back,GameWindowService service){panel=target;returnButton=back;windows=service;}
#endif
    }
}

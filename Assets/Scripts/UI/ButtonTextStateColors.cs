using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class ButtonTextStateColors : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField] private TMP_Text[] labels;
        [Tooltip("Optional shared palette for specialized buttons that keep their own prefab and sprites.")]
        [SerializeField] private ButtonTextStateColors styleSource;
        [Header("Text colors")]
        [SerializeField] private Color normal = Color.white;
        [SerializeField] private Color highlighted = Color.white;
        [SerializeField] private Color pressed = new Color(.8f,.8f,.8f,1);
        [SerializeField] private Color selected = new Color(1,.85f,.55f,1);
        [SerializeField] private Color disabled = new Color(.5f,.5f,.5f,.6f);
        [SerializeField, Min(0)] private float fadeDuration = .1f;
        private Button button;
        private bool hovering, holding, focused;
        private float submitUntil;
        private Color from, target;
        private float elapsed;
        private bool initialized;
        public void UseStyle(ButtonTextStateColors source){styleSource=source==this?null:source;}

        private void Awake() => Cache();
        private void Cache()
        {
            if(!button)button=GetComponent<Button>();
            if(labels==null || labels.Length==0)labels=GetComponentsInChildren<TMP_Text>(true);
        }
        private void OnEnable()
        {
            Cache(); hovering=holding=focused=false; submitUntil=0;
            RefreshVisuals(true);
        }
        private void OnDisable()
        {
            hovering=holding=focused=false;submitUntil=0;initialized=false;
            if(labels!=null)foreach(var label in labels)if(label)label.color=styleSource?styleSource.normal:normal;
        }
        private void LateUpdate() => RefreshVisuals();
        public void RefreshVisuals(bool instant=false)
        {
            Cache();
            var style=styleSource?styleSource:this;
            bool hasFocus=EventSystem.current?EventSystem.current.currentSelectedGameObject==gameObject:focused;
            Color next=!button.enabled || !button.IsInteractable()?style.disabled:
                holding || Time.unscaledTime<submitUntil?style.pressed:hasFocus?style.selected:hovering?style.highlighted:style.normal;
            float duration=style.fadeDuration;
            if(!initialized || instant)
            {from=target=next;elapsed=duration;initialized=true;}
            else if(next!=target)
            {
                from=Color.Lerp(from,target,duration<=0?1:Mathf.Clamp01(elapsed/duration));
                target=next;elapsed=0;
            }
            elapsed+=Time.unscaledDeltaTime;
            Color value=Color.Lerp(from,target,duration<=0?1:Mathf.Clamp01(elapsed/duration));
            foreach(var label in labels)if(label && label.GetComponentInParent<Button>()==button)label.color=value;
        }
        public void OnPointerEnter(PointerEventData data){hovering=true;}
        public void OnPointerExit(PointerEventData data){hovering=false;}
        public void OnPointerDown(PointerEventData data){if(data.button==PointerEventData.InputButton.Left && button.IsInteractable())holding=true;}
        public void OnPointerUp(PointerEventData data){if(data.button==PointerEventData.InputButton.Left)holding=false;}
        public void OnSelect(BaseEventData data){focused=true;}
        public void OnDeselect(BaseEventData data){focused=false;}
        public void OnSubmit(BaseEventData data){if(button.IsInteractable())submitUntil=Time.unscaledTime+button.colors.fadeDuration;}
    }
}

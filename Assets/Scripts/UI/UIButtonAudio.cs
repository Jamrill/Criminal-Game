using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    [DisallowMultipleComponent,RequireComponent(typeof(Button))]
    public sealed class UIButtonAudio : MonoBehaviour,IPointerEnterHandler
    {
        public bool playAppearance=true,playHover=true,playClick=true;
        public bool playDisappearance=true;
        Button button;
        Canvas canvas;
        CanvasGroup[] groups;
        bool wasVisible,hierarchyPending;
        void Awake(){button=GetComponent<Button>();button.onClick.AddListener(Clicked);CacheHierarchy();}
        void CacheHierarchy(){canvas=GetComponentInParent<Canvas>();groups=GetComponentsInParent<CanvasGroup>(true);}
        void OnTransformParentChanged()=>CacheHierarchy();
        void OnEnable(){wasVisible=false;hierarchyPending=true;CacheHierarchy();}
        void LateUpdate()
        {
            if(hierarchyPending){CacheHierarchy();hierarchyPending=false;}
            // CanvasGroup transitions can show/hide controls without disabling
            // them. Observe actual visibility, not just OnEnable/OnDisable.
            bool visible=IsVisible();
            if(visible && !wasVisible && playAppearance)UIButtonSoundPlayer.Play(UIButtonSoundPlayer.Cue.Appear);
            if(!visible && wasVisible && playDisappearance)UIButtonSoundPlayer.Play(UIButtonSoundPlayer.Cue.Disappear);
            wasVisible=visible;
        }
        void OnDisable(){if(wasVisible && playDisappearance)UIButtonSoundPlayer.Play(UIButtonSoundPlayer.Cue.Disappear);wasVisible=false;}
        void OnApplicationQuit()=>UIButtonSoundPlayer.ShuttingDown=true;
        void OnDestroy(){if(button)button.onClick.RemoveListener(Clicked);}
        public void OnPointerEnter(PointerEventData data)
        {
            if(playHover && button && button.IsActive() && button.IsInteractable() && IsVisible())
                UIButtonSoundPlayer.Play(UIButtonSoundPlayer.Cue.Hover);
        }
        void Clicked()
        {
            // Button.onClick already gates input against disabled controls and
            // handles both pointer clicks and keyboard submit. A successful
            // click can close its panel before this listener runs: still play it.
            if(playClick)UIButtonSoundPlayer.Play(UIButtonSoundPlayer.Cue.Click);
        }
        bool IsVisible()
        {
            if(!isActiveAndEnabled)return false;
            if(groups!=null)foreach(var group in groups)if(group && group.alpha<=.01f)return false;
            if(!canvas || !canvas.isActiveAndEnabled)return false;
            var rect=(RectTransform)transform;var camera=canvas.rootCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.rootCanvas.worldCamera;
            var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
            return point.x>=0 && point.y>=0 && point.x<=Screen.width && point.y<=Screen.height;
        }
        public static void Ensure(Button target)
        {if(target && !target.TryGetComponent<UIButtonAudio>(out _))target.gameObject.AddComponent<UIButtonAudio>();}
    }

    // One persistent 2D source, not an AudioSource on every button. This also
    // keeps confirmation sounds alive when clicking destroys/closes the UI.
    public static class UIButtonSoundPlayer
    {
        public enum Cue { Appear,Hover,Click,Disappear }
        internal static bool ShuttingDown;
        static UIButtonSoundSettings settings;
        static AudioSource source;
        static float lastAppear=float.NegativeInfinity,lastHover=float.NegativeInfinity,lastDisappear=float.NegativeInfinity;
        public static UIButtonSoundSettings Settings=>settings?settings:settings=Resources.Load<UIButtonSoundSettings>("UIButtonSounds");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){settings=null;source=null;ShuttingDown=false;lastAppear=lastHover=lastDisappear=float.NegativeInfinity;}
        public static void Play(Cue cue)
        {
            if(!Application.isPlaying || ShuttingDown)return;
            var s=Settings;if(!s || !s.soundEnabled || s.masterVolume<=0)return;
            float now=Time.unscaledTime;
            if(cue==Cue.Appear && now-lastAppear<s.appearCooldown || cue==Cue.Hover && now-lastHover<s.hoverCooldown)return;
            if(cue==Cue.Disappear && now-lastDisappear<s.appearCooldown)return;
            var clip=cue==Cue.Disappear?(s.disappear?s.disappear:s.appear):cue==Cue.Appear?s.appear:cue==Cue.Hover?s.hover:s.click;
            float level=cue==Cue.Disappear?s.disappearVolume:cue==Cue.Appear?s.appearVolume:cue==Cue.Hover?s.hoverVolume:s.clickVolume;
            if(!clip || level<=0)return;
            if(!source)
            {
                var go=new GameObject("UI button audio");Object.DontDestroyOnLoad(go);
                source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.ignoreListenerPause=true;
            }
            source.outputAudioMixerGroup=s.mixerGroup;
            if(cue==Cue.Appear)lastAppear=now;if(cue==Cue.Hover)lastHover=now;
            if(cue==Cue.Disappear)lastDisappear=now;
            source.PlayOneShot(clip,s.masterVolume*level);
        }
    }
}

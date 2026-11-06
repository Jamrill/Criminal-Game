using JuegoCriminal.Core;
using JuegoCriminal.Player;
using UnityEngine;

namespace JuegoCriminal.Audio
{
    // One director on @App. Clips are not looped: each cue is followed by a
    // fresh random silence, and activity interrupts the track with a fade.
    public sealed class AmbientMusicDirector : MonoBehaviour
    {
        public enum Context { Silent,Menu,CalmWorld }
        public AmbientMusicSettings settings;
        AudioSource source;
        GameStateMachine state;
        LocalPlayerMarker player;
        PlayerLocomotionAnimation animationDriver;
        float nextLookup,remaining,calmAge,blockedUntil;
        int lastTrack=-1;
        bool playing,fadingOut;
        Context context;
        public bool IsPlaying=>source && source.isPlaying;
        public float SecondsUntilNextCue=>remaining;
        public AudioClip CurrentTrack=>source?source.clip:null;
        void Awake()
        {
            if(!settings)settings=Resources.Load<AmbientMusicSettings>("AmbientMusic");
            state=GetComponent<GameStateMachine>();
            var go=new GameObject("Ambient music source");go.transform.SetParent(transform,false);
            source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.volume=0;source.ignoreListenerPause=true;
        }
        void Update()=>Advance(Time.unscaledDeltaTime,ResolveContext());
        Context ResolveContext()
        {
            if(!Application.isFocused || !state || Time.unscaledTime<blockedUntil)return Context.Silent;
            if(state.CurrentState==GameState.Menu)return Context.Menu;
            if(state.CurrentState!=GameState.World || Time.timeScale<=0)return Context.Silent;
            if(Time.unscaledTime>=nextLookup)
            {
                nextLookup=Time.unscaledTime+1;
                player=FindAnyObjectByType<LocalPlayerMarker>();animationDriver=player?player.GetComponent<PlayerLocomotionAnimation>():null;
            }
            if(!player)return Context.Silent;
            if(animationDriver && (animationDriver.IsJumping || animationDriver.Phase==PlayerLocomotionAnimation.ActionPhase.Aiming ||
                animationDriver.Phase==PlayerLocomotionAnimation.ActionPhase.Drawing))return Context.Silent;
            if(GameInput.SprintHeld && GameInput.Move.sqrMagnitude>.01f)return Context.Silent;
            return Context.CalmWorld;
        }
        // Future combat, police chases or mission scripts can postpone ambient
        // music without knowing about audio sources or random scheduling.
        public void SuppressFor(float seconds)=>blockedUntil=Mathf.Max(blockedUntil,Time.unscaledTime+Mathf.Max(0,seconds));
        static float Delay(Vector2 range)=>Random.Range(Mathf.Max(0,Mathf.Min(range.x,range.y)),Mathf.Max(0,Mathf.Max(range.x,range.y)));
        void Advance(float delta,Context requested)
        {
            if(!settings || !settings.musicEnabled || settings.volume<=0)requested=Context.Silent;
            if(requested!=context)
            {
                context=requested;calmAge=0;fadingOut=playing;
                if(settings && context!=Context.Silent)remaining=Delay(context==Context.Menu?settings.menuFirstDelay:settings.worldFirstDelay);
            }
            if(!source || !settings)return;
            source.outputAudioMixerGroup=settings.mixerGroup;
            if(playing)
            {
                float target=fadingOut || context==Context.Silent?0:settings.volume;
                source.volume=Mathf.MoveTowards(source.volume,target,delta/Mathf.Max(.1f,settings.fadeSeconds)*Mathf.Max(.01f,settings.volume));
                if(!source.isPlaying || (fadingOut && source.volume<=.0001f))
                {
                    source.Stop();playing=false;
                    if(!fadingOut)remaining=Delay(context==Context.Menu?settings.menuSilence:settings.worldSilence);
                    fadingOut=false;
                }
                return;
            }
            if(context==Context.Silent)return;
            calmAge+=delta;
            if(context==Context.CalmWorld && calmAge<settings.calmSecondsBeforeMusic)return;
            remaining-=delta;if(remaining>0)return;
            var tracks=settings.tracks;
            if(tracks==null || tracks.Length==0){remaining=30;return;}
            int first=Random.Range(0,tracks.Length);
            for(int i=0;i<tracks.Length;i++)
            {
                int index=(first+i)%tracks.Length;if(!tracks[index] || (tracks.Length>1 && index==lastTrack && i<tracks.Length-1))continue;
                lastTrack=index;source.clip=tracks[index];source.volume=0;source.Play();playing=true;return;
            }
            remaining=30;
        }
        void OnDisable(){if(source)source.Stop();playing=fadingOut=false;context=Context.Silent;}
        void OnDestroy(){if(source)Destroy(source.gameObject);}
    }
}

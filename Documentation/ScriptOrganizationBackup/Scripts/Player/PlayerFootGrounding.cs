using System;
using UnityEngine;

namespace JuegoCriminal.Player
{
    [DisallowMultipleComponent,RequireComponent(typeof(Animator))]
    public sealed class PlayerFootGrounding : MonoBehaviour
    {
        [Header("Foot sensors (triggers; body collision stays on CharacterController)")]
        public BoxCollider leftFootSensor;
        public BoxCollider rightFootSensor;
        [Header("Ground detection (world units)")]
        public LayerMask groundLayers=Physics.DefaultRaycastLayers;
        [Min(.01f)] public float probeRadius=.055f;
        [Min(.05f)] public float probeAbove=.65f;
        [Min(.05f)] public float probeBelow=.65f;
        [Min(0)] public float maximumLift=.45f;
        [Min(0)] public float maximumDrop=.55f;
        [Range(0,80)] public float maximumGroundAngle=50;
        [Min(0)] public float soleClearance=.01f;
        [Tooltip("Altura de las suelas en reposo respecto al origen del Player, calibrada para este modelo.")]
        public float restSoleHeight;
        [Header("Animation and smoothing")]
        [Range(0,1)] public float positionWeight=1;
        [Range(0,1)] public float rotationWeight=.8f;
        [Range(0,60)] public float maximumFootTilt=35;
        [Min(.001f)] public float swingFadeStart=.08f;
        [Min(.01f)] public float swingFadeEnd=.3f;
        [Min(1)] public float footResponse=22;
        [Min(1)] public float weightResponse=12;
        [Min(0)] public float maximumPelvisDrop=.3f;
        [Min(1)] public float pelvisResponse=12;
        Animator animator;
        ThirdPersonController movement;
        CharacterController capsule;
        PlayerLocomotionAnimation locomotion;
        Transform playerRoot;
        readonly Foot left=new(),right=new();
        float pelvisOffset;
        bool pelvisReady;
        public int SolverPasses {get;private set;}
        public float LeftWeight=>left.weight;
        public float RightWeight=>right.weight;
        sealed class Foot
        {
            public bool ready,supported;
            public float offset,weight,supportHeight;
            public Vector3 normal=Vector3.up,position;
            public Quaternion rotation;
        }
        void Awake()=>Initialize();
        void Initialize()
        {
            if(!animator)animator=GetComponent<Animator>();
            if(!movement)movement=GetComponentInParent<ThirdPersonController>();
            if(!capsule && movement)capsule=movement.GetComponent<CharacterController>();
            if(!locomotion)locomotion=GetComponentInParent<PlayerLocomotionAnimation>();
            playerRoot=movement?movement.transform:transform;
        }
        void OnDisable(){Reset(left);Reset(right);pelvisOffset=0;pelvisReady=false;}
        static void Reset(Foot foot){foot.ready=false;foot.supported=false;foot.weight=0;foot.offset=0;foot.normal=Vector3.up;}
        bool Usable(Collider collider)=>collider && !collider.isTrigger && !collider.transform.IsChildOf(playerRoot);
        public bool Probe(Vector3 sole,out RaycastHit support)
            =>Probe(sole,probeRadius,out support);
        bool Probe(Vector3 sole,float radius,out RaycastHit support)
        {
            if(!playerRoot)Initialize();
            var hits=Physics.SphereCastAll(sole+Vector3.up*probeAbove,radius,Vector3.down,probeAbove+probeBelow,groundLayers,QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(!Usable(hit.collider) || Vector3.Angle(hit.normal,Vector3.up)>maximumGroundAngle)continue;
                float difference=hit.point.y-sole.y;
                if(difference>maximumLift || difference < -maximumDrop)continue;
                support=hit;return true;
            }
            support=default;return false;
        }
        bool RootSupported()
        {
            if(capsule && capsule.isGrounded)return true;
            float bottom=capsule?capsule.bounds.min.y:playerRoot.position.y+restSoleHeight;
            var origin=new Vector3(playerRoot.position.x,bottom+.1f,playerRoot.position.z);
            foreach(var hit in Physics.RaycastAll(origin,Vector3.down,.1f+(capsule?capsule.skinWidth:0)+.15f,groundLayers,QueryTriggerInteraction.Ignore))
                if(Usable(hit.collider) && Vector3.Angle(hit.normal,Vector3.up)<=maximumGroundAngle)return true;
            return false;
        }
        void OnAnimatorIK(int layerIndex)
        {
            Initialize();
            if(layerIndex!=0 || !animator || !animator.isHuman || !animator.isInitialized)return;
            if(Application.isPlaying && Time.timeScale<=0)return;
            SolverPasses++;
            bool active=(!locomotion || !locomotion.IsJumping) && RootSupported();
            if(!active)
            {
                Reset(left);Reset(right);pelvisOffset=0;pelvisReady=false;
                SetWeights(AvatarIKGoal.LeftFoot,0);SetWeights(AvatarIKGoal.RightFoot,0);return;
            }
            // Manual Animator previews in the editor have no game delta time.
            float dt=Application.isPlaying?Time.deltaTime:1f/60;
            Evaluate(left,AvatarIKGoal.LeftFoot,HumanBodyBones.LeftFoot,leftFootSensor,animator.leftFeetBottomHeight,dt);
            Evaluate(right,AvatarIKGoal.RightFoot,HumanBodyBones.RightFoot,rightFootSensor,animator.rightFeetBottomHeight,dt);
            float baseline=playerRoot.position.y+restSoleHeight;
            float drop=Mathf.Min(0,left.supported?(left.supportHeight-baseline)*left.weight:0,
                right.supported?(right.supportHeight-baseline)*right.weight:0);
            drop=Mathf.Clamp(drop,-maximumPelvisDrop,0);
            pelvisOffset=pelvisReady?Mathf.Lerp(pelvisOffset,drop,1-Mathf.Exp(-pelvisResponse*dt)):drop;pelvisReady=true;
            if(Mathf.Abs(pelvisOffset)>.0001f)animator.bodyPosition+=Vector3.up*pelvisOffset;
            Apply(left,AvatarIKGoal.LeftFoot);
            Apply(right,AvatarIKGoal.RightFoot);
        }
        void Evaluate(Foot foot,AvatarIKGoal goal,HumanBodyBones bone,BoxCollider sensor,float bottomHeight,float dt)
        {
            // Humanoid IK goals are not necessarily the retargeted FK ankle
            // positions. Using them as animation samples narrowed this rig's
            // stance. Read this frame's animated bones before solving either leg.
            var animated=animator.GetBoneTransform(bone).position;
            var rotation=animator.GetIKRotation(goal);
            // Rigify's ankle axes are not the sole's up axis. Keep the animated
            // ankle orientation on flat ground; only add the terrain's tilt.
            var sole=animated-Vector3.up*bottomHeight;
            // Probe below the current animation goal, never using a previously
            // solved sensor offset: that creates feedback near step edges.
            float radius=sensor?Mathf.Min(probeRadius,Mathf.Max(.01f,Mathf.Min(sensor.bounds.extents.x,sensor.bounds.extents.z)*.65f)):probeRadius;
            if(!Probe(sole,radius,out var hit)){Reset(foot);return;}
            foot.supported=true;foot.supportHeight=hit.point.y;
            float lift=Mathf.Max(0,sole.y-(playerRoot.position.y+restSoleHeight));
            float planted=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(swingFadeStart,Mathf.Max(swingFadeStart+.001f,swingFadeEnd),lift));
            if(hit.point.y+soleClearance>sole.y)planted=1; // Don't swing through a step.
            foot.weight=foot.ready?Mathf.MoveTowards(foot.weight,planted,weightResponse*dt):planted;
            var normal=foot.ready?Vector3.Slerp(foot.normal,hit.normal,1-Mathf.Exp(-footResponse*dt)):hit.normal;
            foot.normal=normal;
            var aligned=Quaternion.FromToRotation(Vector3.up,normal)*rotation;
            foot.rotation=Quaternion.RotateTowards(rotation,aligned,maximumFootTilt);
            // Add only the terrain's elevation; do not pin the swing foot to a
            // fixed ground height or erase the original animation's vertical arc.
            float elevation=hit.point.y-(playerRoot.position.y+restSoleHeight);
            float desired=Mathf.Abs(elevation)>.001f?elevation+soleClearance:0;
            foot.offset=foot.ready?Mathf.Lerp(foot.offset,desired,1-Mathf.Exp(-footResponse*dt)):desired;
            foot.position=animated+Vector3.up*foot.offset;foot.ready=true;
        }
        void SetWeights(AvatarIKGoal goal,float weight,float tiltWeight=0)
        {
            animator.SetIKPositionWeight(goal,weight*positionWeight);
            animator.SetIKRotationWeight(goal,weight*rotationWeight*tiltWeight);
        }
        void Apply(Foot foot,AvatarIKGoal goal)
        {
            float adjustment=Mathf.Clamp01(Mathf.Abs(foot.offset)/.01f);
            float tilt=Mathf.Clamp01(Vector3.Angle(foot.normal,Vector3.up)/3);
            // Leave flat-ground animation completely untouched, including the
            // avatar's authored foot rotations and toe lift.
            SetWeights(goal,foot.weight*Mathf.Max(adjustment,tilt),tilt);
            if(!foot.supported)return;
            // Humanoid IK handles unreachable targets. Clamping this vector in
            // 3D ourselves also pulled its X/Z coordinates toward the hip.
            animator.SetIKPosition(goal,foot.position);animator.SetIKRotation(goal,foot.rotation);
        }
    }
}

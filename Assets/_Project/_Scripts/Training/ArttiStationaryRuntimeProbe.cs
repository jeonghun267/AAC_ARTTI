#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Artti.CharacterKit;
using UnityEngine;

namespace Artti.Training
{
    // Editor-only evidence collector. Never saved into the production scene.
    [DefaultExecutionOrder(11000)]
    public sealed class ArttiStationaryRuntimeProbe : MonoBehaviour
    {
        [Serializable] public sealed class Frame { public float time, audioTime, mouthMaximum, ankleWidth; public string phoneme; public bool wave; }
        [Serializable] public sealed class Result
        {
            public bool passed, rootStayedInactive=true, cardChannelStayedSilent=true, sameWiredSource,
                sawWaveWithSpeech, returnedToIdle, sourcePlayed;
            public int callbacks, activeMouthFrames, captures;
            public float duration, observedAudioTime, peakMouthWeight, finalMouthWeight,
                minimumAnkleWidth=float.PositiveInfinity, maximumFootDrift, maximumFootStep;
            public string sourcePath, failure;
            public List<Frame> samples=new List<Frame>();
        }
        public static Action<ArttiStationaryRuntimeProbe,int> CaptureRequested;
        public static Action<ArttiStationaryRuntimeProbe> Completed;
        public Result Evidence { get; private set; }=new Result();

        private readonly float[] captureTimes={.5f,1.4f,3.5f,6.2f,8.5f};
        private readonly int[] shapeIndices=new int[6];
        private ArttiAudioLipSync lip;
        private ClerkView clerk;
        private Animator animator;
        private SkinnedMeshRenderer body;
        private AudioSource verifiedSource,cardSource;
        private GameObject disabledRoot;
        private Transform leftHip,rightHip,leftFoot,rightFoot;
        private Vector3 initialLeft,initialRight,previousLeft,previousRight;
        private float readyAt,start,nextSample;
        private int captureIndex;
        private bool ready,started,waved,sawWave,finished;

        public void Begin(ClerkView view,ArttiAudioLipSync audioLip,AudioSource wiredSource,
                          GameObject inactiveRoot,AudioSource cardChannel,SkinnedMeshRenderer bodyRenderer,string sourcePath)
        {
            clerk=view;lip=audioLip;verifiedSource=wiredSource;disabledRoot=inactiveRoot;cardSource=cardChannel;body=bodyRenderer;
            animator=GetComponent<Animator>();
            leftHip=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);rightHip=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
            string[] shapes={"A","E","I","O","U","MouthClose"};
            for(int i=0;i<shapes.Length;i++)shapeIndices[i]=body.sharedMesh.GetBlendShapeIndex(shapes[i]);
            Evidence.sourcePath=sourcePath;Evidence.sameWiredSource=lip.speechSource==wiredSource;
            if(!Evidence.sameWiredSource || inactiveRoot.activeInHierarchy || !lip.demoSpeech)
                throw new InvalidOperationException("Runtime QA source wiring/root isolation/local audio precondition failed");
            // Uses the scene's existing source; no replacement or cloud service.
            lip.Connect();readyAt=Time.realtimeSinceStartup+1f;ready=true;
        }

        private void LateUpdate()
        {
            if(!ready || finished)return;
            try
            {
                float now=Time.realtimeSinceStartup;
                if(!started)
                {
                    if(now<readyAt)return;
                    initialLeft=previousLeft=leftFoot.position;initialRight=previousRight=rightFoot.position;
                    lip.PlayDemo();Evidence.duration=lip.demoSpeech.length;start=now;started=true;
                }
                float elapsed=now-start;
                Evidence.rootStayedInactive &= !disabledRoot.activeInHierarchy;
                Evidence.cardChannelStayedSilent &= !cardSource || !cardSource.isPlaying;
                Evidence.sameWiredSource &= lip.speechSource==verifiedSource;
                Evidence.sourcePlayed |= verifiedSource.isPlaying;
                Evidence.observedAudioTime=Mathf.Max(Evidence.observedAudioTime,verifiedSource.time);
                float maximum=0;
                for(int i=0;i<shapeIndices.Length;i++)maximum=Mathf.Max(maximum,body.GetBlendShapeWeight(shapeIndices[i]));
                Evidence.peakMouthWeight=Mathf.Max(Evidence.peakMouthWeight,maximum);Evidence.finalMouthWeight=maximum;
                if(maximum>5)Evidence.activeMouthFrames++;
                if(elapsed>=1&&!waved){clerk.PlayGreeting();waved=true;}
                bool wave=animator.GetCurrentAnimatorStateInfo(0).IsName("Wave");sawWave|=wave;
                Evidence.sawWaveWithSpeech |= wave&&maximum>5&&verifiedSource.isPlaying;
                if(sawWave&&animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion")&&!animator.IsInTransition(0))Evidence.returnedToIdle=true;
                Vector3 lateral=rightHip.position-leftHip.position;lateral.y=0;lateral.Normalize();
                float width=Vector3.Dot(rightFoot.position-leftFoot.position,lateral);
                Evidence.minimumAnkleWidth=Mathf.Min(Evidence.minimumAnkleWidth,width);
                Evidence.maximumFootDrift=Mathf.Max(Evidence.maximumFootDrift,Vector3.Distance(initialLeft,leftFoot.position),Vector3.Distance(initialRight,rightFoot.position));
                Evidence.maximumFootStep=Mathf.Max(Evidence.maximumFootStep,Vector3.Distance(previousLeft,leftFoot.position),Vector3.Distance(previousRight,rightFoot.position));
                previousLeft=leftFoot.position;previousRight=rightFoot.position;
                if(elapsed>=nextSample)
                {
                    nextSample=elapsed+.02f;
                    Evidence.samples.Add(new Frame{time=elapsed,audioTime=verifiedSource.time,mouthMaximum=maximum,ankleWidth=width,phoneme=lip.CurrentPhoneme,wave=wave});
                }
                while(captureIndex<captureTimes.Length&&elapsed>=captureTimes[captureIndex])
                {
                    CaptureRequested?.Invoke(this,captureIndex);captureIndex++;Evidence.captures=captureIndex;
                }
                if(elapsed<Evidence.duration+.8f)return;
                Evidence.callbacks=lip.AnalysisCallbacks;
                bool pass=Evidence.rootStayedInactive&&Evidence.cardChannelStayedSilent&&Evidence.sameWiredSource&&Evidence.sourcePlayed
                    &&Evidence.callbacks>30&&Evidence.peakMouthWeight>20&&Evidence.activeMouthFrames>15
                    &&Evidence.observedAudioTime>Evidence.duration*.8f&&maximum<1&&Evidence.sawWaveWithSpeech&&Evidence.returnedToIdle
                    &&Evidence.minimumAnkleWidth>.05f&&Evidence.maximumFootDrift<.005f&&captureIndex==captureTimes.Length;
                Finish(pass,pass?null:"One or more runtime integration criteria failed; inspect recorded metrics");
            }
            catch(Exception error){Finish(false,error.ToString());}
        }

        private void Finish(bool passed,string failure)
        {
            if(finished)return;finished=true;Evidence.passed=passed;Evidence.failure=failure;
            if(lip)lip.Stop();Completed?.Invoke(this);
        }
    }
}
#endif

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Artti.CharacterKit;
using Artti.Training;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Artti.EditorTools
{
    // Opens and verifies the existing scene. Never invokes a builder or saves a scene.
    [InitializeOnLoad]
    public static class ArttiStationaryIntegrationCheck
    {
        const string ScenePath="Assets/_Project/Scenes/TrainingConvenienceScene.unity";
        const string RunKey="Artti.StationaryIntegration.RuntimePending";
        const string PassKey="Artti.StationaryIntegration.RuntimePassed";
        static string Output=>Environment.GetEnvironmentVariable("ARTTI_INTEGRATION_QA_DIR")??Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/ArttiStationaryIntegrationCheck"));
        sealed class Context
        {
            public GameObject actor,stage;public TrainingSceneRoot root;public ClerkView clerk;public ConvenienceHudView hud;
            public ArttiAudioLipSync lip;public ArttiFaceDriver face;public Animator animator;public AudioSource source,cardSource;
            public Camera clerkCamera;public RawImage raw;public SkinnedMeshRenderer body;public SkinnedMeshRenderer[] skins;
        }
        sealed class CanvasState
        {
            public Canvas canvas;public RenderMode mode;public Camera camera;public float plane,scale;
            public CanvasScaler scaler;public bool scalerEnabled;
        }
        [Serializable] sealed class FramingEvidence
        {
            public int samples,upperBodyVertices;
            public float minimumX=float.PositiveInfinity,maximumX=float.NegativeInfinity,maximumY=float.NegativeInfinity;
            public int minimumXFrame=-1,maximumXFrame=-1,maximumYFrame=-1;
            public float minimumXTime,maximumXTime,maximumYTime;
            public float fieldOfView,cameraAspect,minimumWorldHeight=1f;
            public bool passed;
        }
        static ArttiStationaryIntegrationCheck(){EditorApplication.playModeStateChanged+=PlayModeChanged;}

        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static Object Reference(Object target,string field)
        {
            Require(target,"Missing serialized component for "+field);
            var property=new SerializedObject(target).FindProperty(field);
            Require(property!=null,"Missing serialized field "+target.GetType().Name+"."+field);
            return property.objectReferenceValue;
        }
        static string Hierarchy(Transform t)
        {
            string path=t.name;while(t.parent){t=t.parent;path=t.name+"/"+path;}return path;
        }
        static Context Inspect(StringBuilder report)
        {
            var c=new Context();c.stage=GameObject.Find("[ClerkStage]");Require(c.stage,"Missing [ClerkStage]");
            var actor=c.stage.transform.Find("ARTTI_Stationary_v4");Require(actor&&actor.gameObject.activeInHierarchy,"Missing active ARTTI_Stationary_v4 actor");c.actor=actor.gameObject;
            c.animator=c.actor.GetComponent<Animator>();c.clerk=c.actor.GetComponent<ClerkView>();c.lip=c.actor.GetComponent<ArttiAudioLipSync>();c.face=c.actor.GetComponent<ArttiFaceDriver>();
            Require(c.animator&&c.clerk&&c.lip&&c.face,"Actor is missing required animation/lip-sync/view components");
            Require(c.stage.GetComponentsInChildren<Animator>(true).Count(a=>a.gameObject.activeInHierarchy)==1,"Expected exactly one active actor on the stage");
            Require(c.actor.GetComponentsInChildren<ArttiLipSyncController>(true).Length==0,"Legacy ArttiLipSyncController must not compete with v4");
            Require(c.animator.avatar&&c.animator.avatar.isValid&&c.animator.avatar.isHuman&&!c.animator.applyRootMotion,"Invalid Humanoid avatar or enabled root motion");
            c.root=Object.FindFirstObjectByType<TrainingSceneRoot>(FindObjectsInactive.Include);Require(c.root,"TrainingSceneRoot missing");
            Require(Reference(c.root,"clerkView")==c.clerk,"TrainingSceneRoot.clerkView points to another actor");
            c.source=Reference(c.root,"npcSpeechSource") as AudioSource;Require(c.source,"NPC speech source is not serialized");
            Require(c.source==c.lip.speechSource&&c.source.transform.IsChildOf(c.actor.transform),"NPC speech source does not feed this actor's lip analyzer");
            c.cardSource=c.root.GetComponent<AudioSource>();Require(c.cardSource!=c.source,"NPC and card audio channels are not separated");
            c.hud=Reference(c.root,"hud") as ConvenienceHudView;Require(c.hud,"Convenience HUD not wired");
            Require(Reference(c.hud,"ttsSource")==c.source,"HUD speaker indicator is bound to a different source");
            Require(c.lip.voiceProfile&&c.lip.voiceProfile.mfccs.Count>=5&&c.lip.voiceProfile.mfccs.All(m=>m.mfccCalibrationDataList.Count>0),"Korean lip-sync calibration data missing");
            Require(c.lip.demoSpeech&&c.lip.demoSpeech.length>1,"Local demonstration speech missing");
            Require(GraphicsSettings.currentRenderPipeline&&GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal"),"Expected the project's URP pipeline");
            c.skins=c.actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);c.body=c.skins.Single(s=>s.name=="Artti_Body");
            foreach(var skin in c.skins)
            {
                Require(skin.sharedMesh&&skin.sharedMesh.blendShapeCount==10,"Expected ten morph targets on "+skin.name);
                foreach(var material in skin.sharedMaterials)Require(material&&material.shader&&material.shader.name.StartsWith("Universal Render Pipeline/"),"Non-URP character material on "+skin.name);
            }
            c.clerkCamera=c.stage.GetComponentsInChildren<Camera>(true).Single(cam=>cam.name=="ClerkCamera");
            Require(c.clerkCamera.targetTexture,"Clerk camera render target missing");
            c.raw=Object.FindObjectsByType<RawImage>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(r=>r.name=="Clerk3D");
            Require(c.raw.gameObject.activeInHierarchy&&c.raw.texture==c.clerkCamera.targetTexture,"Dashboard Clerk3D slot is not showing the actor camera");
            var raster=c.raw.transform.parent.Find("Clerk");Require(!raster||!raster.gameObject.activeInHierarchy,"Old raster clerk is still visible");
            var clips=c.animator.runtimeAnimatorController.animationClips.Distinct().ToArray();Require(clips.Length==2&&clips.Any(x=>x.name=="Artti_Idle")&&clips.Any(x=>x.name=="Artti_Wave"),"Expected only Idle and Wave clips");
            report.AppendLine("Scene: "+ScenePath);report.AppendLine("Actor: "+Hierarchy(c.actor.transform));
            report.AppendLine("One active actor; Humanoid; root motion off; ten morphs; URP materials: PASS");
            report.AppendLine("TrainingSceneRoot.clerkView -> "+Hierarchy(c.clerk.transform));
            report.AppendLine("TrainingSceneRoot.npcSpeechSource = lip.speechSource = HUD.ttsSource -> "+Hierarchy(c.source.transform));
            report.AppendLine("Calibration profile: "+AssetDatabase.GetAssetPath(c.lip.voiceProfile)+"; phonemes="+c.lip.voiceProfile.mfccs.Count);
            report.AppendLine("Local audio: "+AssetDatabase.GetAssetPath(c.lip.demoSpeech));
            return c;
        }

        [MenuItem("Artti/Verify Stationary v4 Scene Integration")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);var report=new StringBuilder();bool pass=false;
            try
            {
                Require(!EditorApplication.isPlaying,"Stop Play Mode before editor integration QA");
                EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var c=Inspect(report);
                var a=c.animator;a.Rebind();a.Play("Locomotion",0,0);a.Update(0);
                var left=a.GetBoneTransform(HumanBodyBones.LeftFoot);var right=a.GetBoneTransform(HumanBodyBones.RightFoot);
                var lh=a.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var rh=a.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                var hand=a.GetBoneTransform(HumanBodyBones.RightHand);float restHand=hand.position.y;
                Vector3 initialLeft=left.position,initialRight=right.position;float minWidth=float.PositiveInfinity,maxDrift=0,maxLift=0;bool sawWave=false,returned=false;
                var framing=new FramingEvidence{fieldOfView=c.clerkCamera.fieldOfView,cameraAspect=c.clerkCamera.aspect};
                var framingMesh=new Mesh();var framingVertices=new List<Vector3>();
                try
                {
                    for(int frame=0;frame<=660;frame++)
                    {
                        if(frame==60)c.clerk.PlayGreeting();a.Update(1f/60);
                        bool wave=a.GetCurrentAnimatorStateInfo(0).IsName("Wave");sawWave|=wave;
                        if(sawWave&&a.GetCurrentAnimatorStateInfo(0).IsName("Locomotion")&&!a.IsInTransition(0))returned=true;
                        Vector3 lateral=rh.position-lh.position;lateral.y=0;lateral.Normalize();minWidth=Mathf.Min(minWidth,Vector3.Dot(right.position-left.position,lateral));
                        maxDrift=Mathf.Max(maxDrift,Vector3.Distance(initialLeft,left.position),Vector3.Distance(initialRight,right.position));maxLift=Mathf.Max(maxLift,hand.position.y-restHand);
                        if(frame%12==0)CheckUpperBodyFraming(c,frame,framing,framingMesh,framingVertices);
                        if(frame==30||frame==210||frame==480)CaptureDashboard(c,Path.Combine(Output,$"dashboard_editor_{frame:000}.png"));
                    }
                }
                finally {Object.DestroyImmediate(framingMesh);}
                framing.passed=framing.samples>0&&framing.upperBodyVertices>0&&framing.minimumX>=.01f&&framing.maximumX<=.99f&&framing.maximumY<=.98f;
                File.WriteAllText(Path.Combine(Output,"framing_report.json"),JsonUtility.ToJson(framing,true));
                report.AppendLine($"Upper-body framing: min viewport x={framing.minimumX:R} at frame {framing.minimumXFrame} ({framing.minimumXTime:F3}s); max x={framing.maximumX:R} at frame {framing.maximumXFrame} ({framing.maximumXTime:F3}s); max y={framing.maximumY:R} at frame {framing.maximumYFrame} ({framing.maximumYTime:F3}s); samples={framing.samples}; FOV={framing.fieldOfView}; PASS={framing.passed}");
                Require(sawWave&&returned&&maxLift>.4f,"ClerkView.PlayGreeting did not enter Wave and return to Idle");
                Require(minWidth>.05f&&maxDrift<.005f,"Integrated actor legs crossed or feet moved more than 5 mm");
                Require(framing.passed,"Upper body/hand enters the camera safety margin; inspect framing_report.json");
                report.AppendLine($"ClerkView.PlayGreeting -> Wave -> Locomotion: PASS; maximum hand lift={maxLift:R} m");
                report.AppendLine($"Minimum ankle width={minWidth:R} m; maximum foot drift={maxDrift:R} m");
                c.face.Bind();c.face.SetVisemes(80,0,0,0,0);c.face.ApplyImmediate();
                Require(c.body.GetBlendShapeWeight(c.body.sharedMesh.GetBlendShapeIndex("A"))>79,"Integrated FaceDriver cannot drive visemes");
                CaptureDashboard(c,Path.Combine(Output,"dashboard_editor_speaking.png"));c.face.Clear();pass=true;
            }
            catch(Exception error){report.AppendLine(error.ToString());Debug.LogException(error);}
            finally
            {
                report.AppendLine("RESULT: "+(pass?"PASS":"FAIL"));File.WriteAllText(Path.Combine(Output,"editor_integration_report.txt"),report.ToString());
                EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            }
            if(!pass&&Application.isBatchMode)EditorApplication.Exit(1);
        }

        [MenuItem("Artti/Verify Stationary v4 Local Speech Runtime")]
        public static void RunRuntime()
        {
            Directory.CreateDirectory(Output);Require(!EditorApplication.isPlaying,"Runtime QA is already playing");
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var report=new StringBuilder();var c=Inspect(report);
            Require(!c.actor.transform.IsChildOf(c.root.transform),"Actor cannot be a child of the disabled dialogue root");
            c.root.gameObject.SetActive(false);
            foreach(var bootstrap in Object.FindObjectsByType<Artti.Common.AppBootstrap>(FindObjectsInactive.Include,FindObjectsSortMode.None))bootstrap.gameObject.SetActive(false);
            c.actor.AddComponent<ArttiStationaryRuntimeProbe>();
            report.AppendLine("Runtime isolation: TrainingSceneRoot and scene AppBootstrap GameObjects disabled before entering Play Mode.");
            report.AppendLine("No scene save; local demo WAV will play through the prewired NPC AudioSource. Cloud dialogue/TTS and card audio are not invoked.");
            File.WriteAllText(Path.Combine(Output,"runtime_preflight.txt"),report.ToString());
            SessionState.SetBool(RunKey,true);SessionState.SetBool(PassKey,false);EditorApplication.EnterPlaymode();
        }
        static void PlayModeChanged(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(RunKey,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                try
                {
                    var c=Inspect(new StringBuilder());Require(!c.root.gameObject.activeInHierarchy,"Dialogue root became active before runtime QA");
                    var probe=c.actor.GetComponent<ArttiStationaryRuntimeProbe>();Require(probe,"Temporary runtime probe was not retained on entering Play Mode");
                    ArttiStationaryRuntimeProbe.CaptureRequested=(p,index)=>CaptureDashboard(c,Path.Combine(Output,$"dashboard_runtime_{index:00}.png"));
                    ArttiStationaryRuntimeProbe.Completed=p=>{
                        File.WriteAllText(Path.Combine(Output,"runtime_integration_report.json"),JsonUtility.ToJson(p.Evidence,true));
                        SessionState.SetBool(PassKey,p.Evidence.passed);EditorApplication.isPlaying=false;
                    };
                    probe.Begin(c.clerk,c.lip,c.source,c.root.gameObject,c.cardSource,c.body,Hierarchy(c.source.transform));
                }
                catch(Exception error)
                {
                    File.WriteAllText(Path.Combine(Output,"runtime_failure.txt"),error.ToString());SessionState.SetBool(PassKey,false);EditorApplication.isPlaying=false;
                }
            }
            else if(state==PlayModeStateChange.EnteredEditMode)
            {
                bool passed=SessionState.GetBool(PassKey,false);SessionState.EraseBool(RunKey);SessionState.EraseBool(PassKey);
                ArttiStationaryRuntimeProbe.CaptureRequested=null;ArttiStationaryRuntimeProbe.Completed=null;
                EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
                Debug.Log("Stationary scene runtime QA: "+(passed?"PASS":"FAIL"));if(Application.isBatchMode)EditorApplication.Exit(passed?0:1);
            }
        }

        static void RenderNow(Camera camera,RenderTexture target)
        {
            var request=new RenderPipeline.StandardRequest{destination=target};
            if(RenderPipeline.SupportsRenderRequest(camera,request))RenderPipeline.SubmitRenderRequest(camera,request);else camera.Render();
        }
        static void CheckUpperBodyFraming(Context c,int frame,FramingEvidence evidence,Mesh baked,List<Vector3> vertices)
        {
            c.body.BakeMesh(baked,false);baked.GetVertices(vertices);
            // Match the actual CPU capture's independent scale-one pose object.
            Matrix4x4 world=Matrix4x4.TRS(c.body.transform.position,c.body.transform.rotation,Vector3.one);
            float time=(frame+1)/60f;int tested=0;
            foreach(var vertex in vertices)
            {
                Vector3 position=world.MultiplyPoint3x4(vertex);
                if(position.y<evidence.minimumWorldHeight)continue;
                Vector3 viewport=c.clerkCamera.WorldToViewportPoint(position);
                Require(viewport.z>c.clerkCamera.nearClipPlane,"Upper-body vertex is behind/inside the camera near plane");
                tested++;
                if(viewport.x<evidence.minimumX){evidence.minimumX=viewport.x;evidence.minimumXFrame=frame;evidence.minimumXTime=time;}
                if(viewport.x>evidence.maximumX){evidence.maximumX=viewport.x;evidence.maximumXFrame=frame;evidence.maximumXTime=time;}
                if(viewport.y>evidence.maximumY){evidence.maximumY=viewport.y;evidence.maximumYFrame=frame;evidence.maximumYTime=time;}
            }
            Require(tested>0,"Framing audit found no body vertices above world height 1 m");
            evidence.samples++;evidence.upperBodyVertices+=tested;
        }
        static void SaveNonblankFrame(RenderTexture target,string path)
        {
            var previous=RenderTexture.active;Texture2D image=null;
            try {
                RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                File.WriteAllBytes(path,image.EncodeToPNG());
                var pixels=image.GetPixels32();Color32 reference=pixels[0];int varied=0,opaque=0;
                foreach(var pixel in pixels) {
                    if(Mathf.Abs(pixel.r-reference.r)+Mathf.Abs(pixel.g-reference.g)+Mathf.Abs(pixel.b-reference.b)>18)varied++;
                    if(pixel.a>16)opaque++;
                }
                File.AppendAllText(Path.Combine(Output,"render_validation.txt"),$"{Path.GetFileName(path)}: {target.width}x{target.height}; variedPixels={varied}; nontransparentPixels={opaque}\n");
                Require(varied>pixels.Length/1000 && opaque>pixels.Length/1000,"Blank or unrendered QA image: "+path);
            }
            finally {RenderTexture.active=previous;if(image)Object.DestroyImmediate(image);}
        }
        static void CaptureDashboard(Context c,string path)
        {
            var baked=new List<GameObject>();var enabled=c.skins.Select(s=>s.enabled).ToArray();var states=new List<CanvasState>();
            Camera uiCamera=Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).FirstOrDefault(cam=>cam!=c.clerkCamera&&cam.targetTexture==null&&cam.CompareTag("MainCamera"));
            Require(uiCamera,"Main dashboard camera missing");
            RenderTexture previousTarget=uiCamera.targetTexture,previousActive=RenderTexture.active;float previousAspect=uiCamera.aspect;int previousMask=uiCamera.cullingMask;
            var referenceScaler=c.raw.canvas.rootCanvas.GetComponent<CanvasScaler>();
            Vector2 referenceSize=referenceScaler?referenceScaler.referenceResolution:new Vector2(1536,1024);
            int width=Mathf.RoundToInt(referenceSize.x),height=Mathf.RoundToInt(referenceSize.y);
            Require(width>0&&height>0,"Invalid dashboard reference resolution");
            RenderTexture target=null;
            try
            {
                for(int i=0;i<c.skins.Length;i++)
                {
                    var skin=c.skins[i];var mesh=new Mesh();skin.BakeMesh(mesh,false);
                    var go=new GameObject("Transient QA pose");go.layer=skin.gameObject.layer;
                    go.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                    skin.enabled=false;baked.Add(go);
                }
                RenderNow(c.clerkCamera,c.clerkCamera.targetTexture);
                SaveNonblankFrame(c.clerkCamera.targetTexture,Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+"_clerk_rt.png"));
                target=new RenderTexture(width,height,24);uiCamera.targetTexture=target;uiCamera.aspect=(float)width/height;
                // The production camera has cullingMask=0 because its UI is
                // ScreenSpaceOverlay. A temporary camera-rendered canvas needs
                // its actual UI/default layers included for this capture only.
                int canvasMask=1<<5;
                foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Where(x=>x.isRootCanvas))
                {
                    var scaler=canvas.GetComponent<CanvasScaler>();states.Add(new CanvasState{canvas=canvas,mode=canvas.renderMode,camera=canvas.worldCamera,plane=canvas.planeDistance,scale=canvas.scaleFactor,scaler=scaler,scalerEnabled=scaler&&scaler.enabled});
                    if(scaler)scaler.enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=uiCamera;canvas.planeDistance=Mathf.Max(uiCamera.nearClipPlane+.1f,1f);canvas.scaleFactor=1;
                    canvasMask|=1<<canvas.gameObject.layer;
                    foreach(var graphic in canvas.GetComponentsInChildren<Graphic>(false))canvasMask|=1<<graphic.gameObject.layer;
                    var rect=canvas.transform as RectTransform;if(rect){rect.ForceUpdateRectTransforms();LayoutRebuilder.ForceRebuildLayoutImmediate(rect);}
                }
                uiCamera.cullingMask=previousMask|canvasMask;
                Canvas.ForceUpdateCanvases();
                File.AppendAllText(Path.Combine(Output,"render_validation.txt"),$"UI capture mask: original={previousMask}; temporary={uiCamera.cullingMask}; canvases={states.Count}\n");
                RenderNow(uiCamera,target);SaveNonblankFrame(target,path);
            }
            finally
            {
                RenderTexture.active=previousActive;uiCamera.targetTexture=previousTarget;uiCamera.aspect=previousAspect;uiCamera.cullingMask=previousMask;
                foreach(var saved in states){saved.canvas.renderMode=saved.mode;saved.canvas.worldCamera=saved.camera;saved.canvas.planeDistance=saved.plane;saved.canvas.scaleFactor=saved.scale;if(saved.scaler)saved.scaler.enabled=saved.scalerEnabled;}
                foreach(var go in baked){Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);Object.DestroyImmediate(go);}
                for(int i=0;i<c.skins.Length;i++)c.skins[i].enabled=enabled[i];
                if(target){target.Release();Object.DestroyImmediate(target);}Canvas.ForceUpdateCanvases();
            }
        }
    }
}

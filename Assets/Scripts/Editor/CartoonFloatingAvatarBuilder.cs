using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BiteOrBetray.Art3D;
using BiteOrBetray.Table3D;
using BiteOrBetray.VisualPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BiteOrBetray.EditorTools
{
    [InitializeOnLoad]
    public static class CartoonFloatingAvatarBuilder
    {
        const string Folder = "Assets/BiteOrBetray/VisualPrototype";
        const string Output = @"C:\Users\acer\Documents\Codex\2026-10-04\buk\outputs\BiteOrBetray_VisualPrototype";
        const string Prefab = Folder+"/CartoonFloatingHero.prefab";
        static double lastCheck;
        static CartoonFloatingAvatarBuilder() => EditorApplication.update += Poll;
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup-lastCheck<1 || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            lastCheck=EditorApplication.timeSinceStartup;
            const string request="Temp/CartoonFloatingAvatar.request";
            const string bodyRequest="Temp/CartoonBodyAvatar.request";
            bool withBody=File.Exists(bodyRequest);
            if (!withBody && !File.Exists(request)) return;
            File.Delete(withBody?bodyRequest:request);
            string errorPath=Output+(withBody?"/Body_Unity_Error.txt":"/Floating_Unity_Error.txt");
            try
            {
                Build(withBody);
                if (File.Exists(errorPath)) File.Delete(errorPath);
            }
            catch (Exception e) { File.WriteAllText(errorPath,e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Bite or Betray/Prototype/Bangun Floating Hero (Terpisah)")]
        public static void Build() => Build(false);

        [MenuItem("Bite or Betray/Prototype/Bangun Hero Dengan Badan (Terpisah)")]
        public static void BuildBody() => Build(true);

        static void Build(bool withBody)
        {
            string prefabPath=withBody?Folder+"/CartoonBodyHero.prefab":Prefab;
            string label=withBody?"WithBody":"Floating";
            var original=EditorSceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                string modelPath=Folder+"/Cartoon_Hero_"+(withBody?"WithBody":"Floating")+"_Prototype.fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
                importer.animationType=ModelImporterAnimationType.Generic;
                importer.importAnimation=false;
                importer.isReadable=true;
                importer.SaveAndReimport();
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (source==null) throw new FileNotFoundException(modelPath);
                if (withBody && AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/Cartoon_Trouser.mat")==null)
                {
                    var trousers=source.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).First(m=>m.name=="Cartoon_Trouser");
                    var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/Cartoon_Green.mat"));
                    material.name="Cartoon_Trouser";material.SetColor("_BaseColor",trousers.color);
                    AssetDatabase.CreateAsset(material,Folder+"/Materials/Cartoon_Trouser.mat");
                }
                var root=new GameObject(withBody?"Cartoon Hero With Body":"Cartoon Floating Hero");
                var model=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);
                model.transform.SetParent(root.transform,false);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/"+m.name+".mat") ?? throw new InvalidOperationException("Missing material: "+m.name)).ToArray();
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        skin.updateWhenOffscreen=true;
                        skin.quality=SkinQuality.Bone4;
                        skin.localBounds=new Bounds(new Vector3(0,0,1),Vector3.one*4);
                    }
                }
                var avatar=root.AddComponent<SeatedAvatar>();
                avatar.seat=root.transform;
                avatar.head=Part(model.transform,"Head");
                var eye=new GameObject("Avatar Eye Reference").transform;
                eye.SetParent(avatar.head,false);
                eye.position=(Part(model.transform,"EyeWhite_L").position+Part(model.transform,"EyeWhite_R").position)*.5f+root.transform.forward*.005f;
                eye.rotation=root.transform.rotation;
                avatar.eyeReference=eye;
                bool namedLeftIsLeft=Part(model.transform,"Hand_Left").position.x<0;
                avatar.left=Hand(model.transform,namedLeftIsLeft?"Left":"Right",root.transform,-1,withBody);
                avatar.right=Hand(model.transform,namedLeftIsLeft?"Right":"Left",root.transform,1,withBody);
                avatar.headRenderers=model.GetComponentsInChildren<Renderer>().Where(r=>withBody?
                    r.transform.IsChildOf(avatar.head) || r.name=="HeadSkin": !r.name.StartsWith("Glove_")).ToArray();
                CartoonAvatarBody body=null;
                if (withBody)
                {
                    body=root.AddComponent<CartoonAvatarBody>();
                    body.left=BodyArm(model.transform,namedLeftIsLeft?"Left":"Right");
                    body.right=BodyArm(model.transform,namedLeftIsLeft?"Right":"Left");
                    body.Initialize();
                }
                root.AddComponent<RemoteAvatarRepresentation>().avatar=avatar;
                var reactions=root.AddComponent<AvatarReactionController>();
                var face=root.AddComponent<CartoonAvatarFace>();
                face.reactions=reactions;
                face.mouth=Part(model.transform,"Mouth").GetComponent<SkinnedMeshRenderer>();
                face.leftLid=Part(model.transform,"ExpressionLid_L").GetComponent<SkinnedMeshRenderer>();
                face.rightLid=Part(model.transform,"ExpressionLid_R").GetComponent<SkinnedMeshRenderer>();
                var reference=withBody?ReferencePose(avatar):PoseAt(avatar,0);
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
                var report=Validate(avatar,face,reactions,body,reference,prefabPath);

                RenderSettings.skybox=null;
                RenderSettings.ambientMode=AmbientMode.Flat;
                RenderSettings.ambientLight=new Color(.5f,.53f,.56f);
                AddLight("Warm key",new Vector3(40,-28,0),1.2f,new Color(1,.89f,.74f));
                AddLight("Soft fill",new Vector3(34,143,0),.5f,new Color(.76f,.88f,1));
                var camera=new GameObject(label+" avatar proof camera").AddComponent<Camera>();
                camera.transform.position=withBody?new Vector3(.6f,1.55f,3.2f):new Vector3(.34f,1.72f,2.3f);
                camera.transform.rotation=Quaternion.LookRotation((withBody?new Vector3(0,1.02f,0):new Vector3(0,1.34f,0))-camera.transform.position);
                camera.fieldOfView=withBody?50:33;
                camera.nearClipPlane=.02f;
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.22f,.29f,.31f);
                camera.cullingMask=1<<29;
                camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
                foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer=29;
                avatar.SetPose(reference);
                for (int i=0;i<60;i++) { avatar.ApplyPose(1f/60);body?.ApplyArms(); }
                reactions.ResetState(); face.Step(0);
                EditorSceneManager.SaveScene(scene,Folder+(withBody?"/CartoonBodyAvatarProof.unity":"/CartoonFloatingAvatarProof.unity"));
                var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var mood in new[] { "Neutral", "Good", "Bad", "Grip", "Blink" })
                {
                    reactions.ResetState(); face.Step(0);
                    var pose=reference;
                    if (mood=="Grip") { pose.leftGrip=pose.rightGrip=1; pose.leftIndex=pose.rightIndex=withBody?1:0; }
                    avatar.SetPose(pose);
                    for (int i=0;i<60;i++) { avatar.ApplyPose(1f/60);body?.ApplyArms(); }
                    if (mood=="Good") reactions.Play(CharacterReaction.Yummy);
                    if (mood=="Bad") reactions.Play(CharacterReaction.Yuck);
                    face.Step(.1f); face.Step(.1f);
                    if (mood=="Blink")
                        for (int i=0;i<600 && Weight(face.leftLid,"Blink")<99;i++) face.Step(1f/120);
                    CartoonVisualPrototypeBuilder.Capture(camera,Output+"/BiteOrBetray_"+label+"_Unity_"+mood+".png",1000,withBody?1200:1000,skins);
                }
                if (withBody)
                {
                    reactions.ResetState();face.Step(.1f);face.Step(.1f);avatar.SetPose(reference);
                    for (int i=0;i<60;i++) { avatar.ApplyPose(1f/60);body.ApplyArms(); }
                    root.transform.position=new Vector3(0,0,-1.52f);
                    foreach (string name in new[] { "Room","Table","Dispenser","Inventory","SeatBlockouts","PlayerHands" })
                    {
                        var environment=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Cartoon_"+name+"_Prototype.fbx"),scene);
                        foreach(var renderer in environment.GetComponentsInChildren<Renderer>())
                            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/"+m.name+".mat")).ToArray();
                        foreach(var transform in environment.GetComponentsInChildren<Transform>()) transform.gameObject.layer=29;
                    }
                    camera.transform.position=new Vector3(0,1.48f,1.83f);
                    camera.transform.rotation=Quaternion.LookRotation(new Vector3(0,1.19f,-.45f)-camera.transform.position);
                    camera.fieldOfView=47.5f;
                    EditorSceneManager.SaveScene(scene,Folder+"/CartoonBodyTableProof.unity");
                    CartoonVisualPrototypeBuilder.Capture(camera,Output+"/BiteOrBetray_WithBody_Unity_Table.png",1920,1080,skins);
                }
                File.WriteAllText(Output+(withBody?"/Body_Unity_Checks.txt":"/Floating_Unity_Checks.txt"),string.Join("\n",report));
                Debug.Log("[Bite or Betray] "+label+" prototype prefab and simulated tracking checks passed.");
            }
            finally { if (original.IsValid()) EditorSceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene,true); }
        }

        static List<string> Validate(SeatedAvatar avatar,CartoonAvatarFace face,AvatarReactionController reactions,CartoonAvatarBody body,AvatarPose reference,string prefabPath)
        {
            void Check(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
            Check(avatar.left.fingers.Length==10 && avatar.right.fingers.Length==10,"Finger bindings must be 10 per hand.");
            Check(avatar.headRenderers.Length==29,"Head visibility list incomplete.");
            avatar.Initialize(); face.Step(0);
            avatar.transform.SetPositionAndRotation(new Vector3(.3f,0,.2f),Quaternion.Euler(0,60,0));
            float maxEye=0,maxHand=0;
            for (int i=0;i<360;i++)
            {
                var pose=body==null?PoseAt(avatar,i/60f):reference;
                if(body!=null)
                {
                    pose.head.position+=new Vector3(Mathf.Sin(i/60f)*.02f,0,0);
                    pose.leftHand.position+=new Vector3(Mathf.Sin(i/60f)*.025f,Mathf.Sin(i/70f)*.02f,0);
                    pose.rightHand.position+=new Vector3(Mathf.Sin(i/60f)*.03f,Mathf.Sin(i/70f)*.02f,0);
                }
                Check(avatar.SetPose(pose),"Valid simulated pose rejected.");
                avatar.ApplyPose(1f/60);
                body?.ApplyArms();
                maxEye=Mathf.Max(maxEye,Vector3.Distance(avatar.eyeReference.position,avatar.seat.TransformPoint(pose.head.position)));
                if(i>15) maxHand=Mathf.Max(maxHand,Mathf.Max(Vector3.Distance(avatar.left.hand.position,avatar.seat.TransformPoint(pose.leftHand.position)),
                    Vector3.Distance(avatar.right.hand.position,avatar.seat.TransformPoint(pose.rightHand.position))));
            }
            Check(maxEye<.001f && maxHand<.001f,"Tracked anchors drift after seat rotation.");
            var camera=new GameObject("Visibility test").AddComponent<Camera>();
            avatar.SetLocalHeadHidden(camera,true);
            Check(avatar.headRenderers.All(r=>r.gameObject.layer==30) && (camera.cullingMask&(1<<30))==0,"Local head not hidden.");
            Check(avatar.GetComponentsInChildren<Renderer>().Except(avatar.headRenderers).All(r=>r.gameObject.layer!=30),"Body or hands hidden with head.");
            avatar.SetLocalHeadHidden(camera,false);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            string handSuffix=avatar.left.hand.name.EndsWith("_Left")?"L":"R";
            var grip=avatar.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name==((body!=null?"BodyHand_":"Glove_")+handSuffix));
            var open=new Mesh(); var closed=new Mesh();
            var handPose=reference;handPose.leftGrip=handPose.leftIndex=0;
            avatar.SetPose(handPose); for(int i=0;i<60;i++) { avatar.ApplyPose(1f/60);body?.ApplyArms(); }
            grip.BakeMesh(open);
            handPose.leftGrip=handPose.leftIndex=1;
            avatar.SetPose(handPose); for(int i=0;i<60;i++) { avatar.ApplyPose(1f/60);body?.ApplyArms(); }
            grip.BakeMesh(closed);
            float deformation=open.vertices.Zip(closed.vertices,Vector3.Distance).Average();
            Check(deformation>.003f,"Finger mesh did not deform on grip.");
            UnityEngine.Object.DestroyImmediate(open); UnityEngine.Object.DestroyImmediate(closed);
            var held=avatar.left.hand.position;
            handPose.leftTracked=handPose.rightTracked=false;
            avatar.SetPose(handPose); for(int i=0;i<12;i++) { avatar.ApplyPose(1f/60);body?.ApplyArms(); }
            Check(Vector3.Distance(held,avatar.left.hand.position)<.001f,"Tracking loss hold failed.");
            for(int i=0;i<360;i++) { avatar.ApplyPose(1f/60);body?.ApplyArms(); }
            Check(Vector3.Distance(avatar.left.hand.position,avatar.seat.TransformPoint(avatar.left.restPosition))<.001f,"Rest fallback failed.");
            var remote=avatar.GetComponent<RemoteAvatarRepresentation>();
            var remotePose=reference; remotePose.sequence=1;
            Check(remote.ReceivePose(remotePose) && !remote.ReceivePose(remotePose),"Remote sequence handling failed.");
            remote.ApplyReceivedPose(Time.unscaledTime);
            remote.ResetStream();
            reactions.Play(CharacterReaction.Yummy); face.Step(.1f); face.Step(.1f);
            Check(Weight(face.mouth,"Grin")>70 && Weight(face.leftLid,"Wide")>70,"Good reaction binding failed.");
            reactions.Play(CharacterReaction.Yuck); face.Step(.1f); face.Step(.1f);
            Check(Weight(face.mouth,"Frown")>95 && Weight(face.rightLid,"Squint")>70,"Bad reaction binding failed.");
            reactions.SetStatus(true,true); reactions.SetSickness(.9f); face.Step(.1f);
            Check(Weight(face.mouth,"Frown")>95,"Defeated status binding failed.");
            reactions.ResetState(); face.Step(0);
            Check(Weight(face.mouth,"Frown")==0 && Weight(face.leftLid,"Wide")==0,"Face reset failed.");
            float blink=0;
            for(int i=0;i<300;i++) { face.Step(1f/60); blink=Mathf.Max(blink,Weight(face.leftLid,"Blink")); }
            Check(blink>95,"Blink shape not driven.");
            if (body!=null)
            {
                var leftAttachment=body.left.wrist.localPosition;
                var rightAttachment=body.right.wrist.localPosition;
                handPose=reference;handPose.leftHand.position=new Vector3(-2,2,2);handPose.rightHand.position=new Vector3(2,2,2);
                avatar.SetPose(handPose);for(int i=0;i<60;i++) { avatar.ApplyPose(1f/60);body.ApplyArms(); }
                Check(body.MaxReachCorrection>1,"Unreachable hand pose was not constrained.");
                Check(Vector3.Distance(body.left.wrist.localPosition,leftAttachment)<.0001f &&
                    Vector3.Distance(body.right.wrist.localPosition,rightAttachment)<.0001f,"Wrist detached from forearm.");
                var posed=new Mesh();
                avatar.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Jacket").BakeMesh(posed);
                Check(posed.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"Body deformation produced invalid vertices.");
                UnityEngine.Object.DestroyImmediate(posed);
                avatar.SetPose(reference);for(int i=0;i<60;i++) { avatar.ApplyPose(1f/60);body.ApplyArms(); }
            }
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<1000;i++) { avatar.ApplyPose(1f/60);body?.ApplyArms(); face.Step(1f/60); }
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(allocated==0,"Steady avatar update allocated "+allocated+" bytes.");
            avatar.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var meshes=avatar.GetComponentsInChildren<MeshFilter>().Select(r=>r.sharedMesh).Concat(avatar.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.sharedMesh));
            return new List<string> { "PASS: isolated "+(body!=null?"body":"floating")+" prefab, simulated Unity editor checks", "Prefab: "+prefabPath,
                "Head renderers: 29; finger joints: 20; mouth shapes: 3; eyelid shapes: 6", "Tracking: 360 frames, seat rotated 60 degrees",
                "Max eye anchor error (m): "+maxEye, "Max wrist error, both hands (m): "+maxHand,"Mean grip mesh deformation (m): "+deformation,
                "PASS: local head hiding with visible hands, loss hold, rest fallback, remote sequence rejection",
                "PASS: Yummy/Yuck events, defeated status, reset, blink", "Steady updates (1000): "+allocated+" allocated bytes",
                "Triangles: "+meshes.Sum(m=>m.triangles.Length/3),body!=null?"PASS: connected arms, reach limits, finite jacket mesh; body and legs remain visible when local head is hidden.":"Floating version",
                "Original scene unchanged; no XR hardware test; no Quest FPS measurement." };
        }

        static AvatarPose PoseAt(SeatedAvatar avatar,float time) => new AvatarPose {
            head=new Pose(new Vector3(Mathf.Sin(time)*.04f,1.634f,.06f),Quaternion.Euler(8*Mathf.Sin(time),18*Mathf.Sin(time*.7f),0)),
            leftHand=new Pose(new Vector3(-.22f,.96f+Mathf.Sin(time)*.03f,.3f),Quaternion.identity),
            rightHand=new Pose(new Vector3(.22f,.96f-Mathf.Sin(time)*.03f,.3f),Quaternion.identity),
            headTracked=true,leftTracked=true,rightTracked=true };
        static Transform Part(Transform root,string name) => root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        static AvatarPose ReferencePose(SeatedAvatar avatar) => new AvatarPose {
            head=avatar.Relative(avatar.eyeReference),
            leftHand=new Pose(avatar.seat.InverseTransformPoint(avatar.left.hand.position),Quaternion.Inverse(avatar.seat.rotation)*avatar.left.hand.rotation*Quaternion.Inverse(avatar.left.handRotationOffset)),
            rightHand=new Pose(avatar.seat.InverseTransformPoint(avatar.right.hand.position),Quaternion.Inverse(avatar.seat.rotation)*avatar.right.hand.rotation*Quaternion.Inverse(avatar.right.handRotationOffset)),
            headTracked=true,leftTracked=true,rightTracked=true,
            leftGrip=avatar.left.hand.position.y>avatar.right.hand.position.y?.875f:.15f,
            leftIndex=avatar.left.hand.position.y>avatar.right.hand.position.y?.875f:.15f,
            rightGrip=avatar.right.hand.position.y>avatar.left.hand.position.y?.875f:.15f,
            rightIndex=avatar.right.hand.position.y>avatar.left.hand.position.y?.875f:.15f };
        static CartoonAvatarBody.Arm BodyArm(Transform model,string suffix) => new CartoonAvatarBody.Arm {
            shoulder=Part(model,"Arm_"+suffix),elbow=Part(model,"Forearm_"+suffix),wrist=Part(model,"Hand_"+suffix) };
        static SeatedAvatar.Hand Hand(Transform model,string suffix,Transform root,float side,bool withBody)
        {
            var hand=Part(model,"Hand_"+suffix);
            var forward=(hand.position-Part(model,"Forearm_"+suffix).position).normalized;
            var fingers=model.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Finger_") && t.name.EndsWith("_"+suffix)).ToArray();
            return new SeatedAvatar.Hand { hand=hand,restPosition=withBody?root.InverseTransformPoint(hand.position):new Vector3(side*.16f,.79f,.4f),
                handRotationOffset=Quaternion.Inverse(Quaternion.LookRotation(forward,root.up))*hand.rotation,fingers=fingers,
                fingerAxes=fingers.Select(t=>t.InverseTransformDirection(Vector3.Cross(forward,-root.up)).normalized).ToArray(),
                indexFingers=fingers.Select(t=>t.name.Contains("_Index_")).ToArray() };
        }
        static float Weight(SkinnedMeshRenderer renderer,string name) => Enumerable.Range(0,renderer.sharedMesh.blendShapeCount)
            .Where(i=>renderer.sharedMesh.GetBlendShapeName(i).EndsWith("."+name) || renderer.sharedMesh.GetBlendShapeName(i)==name).Select(renderer.GetBlendShapeWeight).Single();
        static void AddLight(string name,Vector3 rotation,float intensity,Color color)
        {
            var light=new GameObject(name).AddComponent<Light>(); light.type=LightType.Directional;
            light.transform.rotation=Quaternion.Euler(rotation);light.intensity=intensity;light.color=color;light.cullingMask=1<<29;
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class WorldBootstrap : MonoBehaviour
{
    public static WorldBootstrap Instance;
    CharacterController controller;
    Transform playerRoot, cameraRig, leftDoor, rightDoor, leftThreat, rightThreat;
    Camera gameCamera;
    Light flashlight;
    SimpleTouchStick moveStick;
    Text statusText, cameraText, messageText;
    Image powerFill, monitorShade;
    Button leftDoorButton, rightDoorButton, monitorButton, lightButton;
    AudioSource ambienceSource, sfxSource, leftThreatAudio, rightThreatAudio;
    AudioClip ambienceClip, monsterGrowlClip, zombieGroanClip, doorSlamClip;
    Material wallMat, floorMat, metalMat, trimMat, eyeMat;
    bool leftClosed, rightClosed, monitorOpen, lightOn, ended;
    float yaw, pitch = 2f, power = 100f, nightTimer, aiTimer, scareTime;
    int cameraIndex = 0;
    const float NightLength = 240f;
    readonly string[] cameraNames = { "CAM 01  |  VESTÍBULO", "CAM 02  |  PASILLO OESTE", "CAM 03  |  PASILLO ESTE", "CAM 04  |  ALMACÉN" };
    readonly Vector3[] cameraPositions = { new Vector3(0,2.1f,9), new Vector3(-11,2.1f,2), new Vector3(11,2.1f,2), new Vector3(0,2.2f,-10) };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureGameStarts()
    {
        SetLandscape();
        if (FindObjectOfType<WorldBootstrap>() == null) new GameObject("Nightwatch Game").AddComponent<WorldBootstrap>();
    }
    static void SetLandscape()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.autorotateToPortrait = false; Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true; Screen.autorotateToLandscapeRight = true;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; SetLandscape();
    }
    void Start()
    {
        Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        QualitySettings.shadows = ShadowQuality.HardOnly; QualitySettings.shadowDistance = 22;
        BuildMaterials(); BuildOffice(); BuildPlayer(); BuildThreats(); BuildAudio(); BuildHUD(); UpdateHUD();
    }

    void BuildMaterials()
    {
        wallMat = MakeMat(new Color(.17f,.19f,.20f), .08f, .12f);
        floorMat = MakeMat(new Color(.11f,.12f,.13f), .12f, .22f);
        metalMat = MakeMat(new Color(.23f,.25f,.26f), .72f, .42f);
        trimMat = MakeMat(new Color(.055f,.065f,.07f), .45f, .3f);
        eyeMat = MakeMat(new Color(1f,.18f,.025f), .1f, .2f, true);
    }
    Material MakeMat(Color color, float metallic=0, float smooth=.25f, bool emission=false)
    {
        Shader sh = Shader.Find("Standard"); if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh); m.color = color;
        if(m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if(m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if(emission && m.HasProperty("_EmissionColor")){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.7f);}
        return m;
    }
    GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, bool collider=true, Transform parent=null)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.transform.position=pos; o.transform.localScale=size;
        if(parent)o.transform.SetParent(parent,true);
        var r=o.GetComponent<Renderer>(); if(mat)r.sharedMaterial=mat;
        if(!collider){var c=o.GetComponent<Collider>();if(c)Destroy(c);} return o;
    }
    void BuildOffice()
    {
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.11f,.13f,.16f); RenderSettings.fog=true; RenderSettings.fogColor=new Color(.045f,.055f,.065f); RenderSettings.fogMode=FogMode.ExponentialSquared; RenderSettings.fogDensity=.018f;
        Box("Office floor",new Vector3(0,-.22f,0),new Vector3(10,.44f,10),floorMat);
        Box("Office ceiling",new Vector3(0,3.5f,0),new Vector3(10,.3f,10),trimMat);
        Box("Back wall",new Vector3(0,1.6f,-5),new Vector3(10,3.4f,.3f),wallMat);
        Box("Front wall left",new Vector3(-3.4f,1.6f,5),new Vector3(3.2f,3.4f,.3f),wallMat);
        Box("Front wall right",new Vector3(3.4f,1.6f,5),new Vector3(3.2f,3.4f,.3f),wallMat);
        Box("Front lintel",new Vector3(0,3.1f,5),new Vector3(3.6f,.4f,.3f),wallMat);
        Box("Left wall rear",new Vector3(-5,1.6f,-2.9f),new Vector3(.3f,3.4f,4.2f),wallMat);
        Box("Left wall front",new Vector3(-5,1.6f,4.1f),new Vector3(.3f,3.4f,1.8f),wallMat);
        Box("Right wall rear",new Vector3(5,1.6f,-2.9f),new Vector3(.3f,3.4f,4.2f),wallMat);
        Box("Right wall front",new Vector3(5,1.6f,4.1f),new Vector3(.3f,3.4f,1.8f),wallMat);
        // Corridors and solid collision boundaries
        foreach(int side in new[]{-1,1})
        {
            Box("Hall floor",new Vector3(side*11,-.22f,1),new Vector3(12,.44f,7),floorMat);
            Box("Hall ceiling",new Vector3(side*11,3.5f,1),new Vector3(12,.3f,7),trimMat);
            Box("Hall outer wall",new Vector3(side*17,1.6f,1),new Vector3(.3f,3.4f,7),wallMat);
            Box("Hall rear wall",new Vector3(side*11,1.6f,-2.5f),new Vector3(12,3.4f,.3f),wallMat);
            Box("Hall front wall",new Vector3(side*11,1.6f,4.5f),new Vector3(12,3.4f,.3f),wallMat);
            Box("Door frame",new Vector3(side*5,1.5f,1),new Vector3(.38f,3.05f,2.2f),metalMat,false);
            var d=Box(side<0?"West security shutter":"East security shutter",new Vector3(side*4.88f,1.4f,1),new Vector3(.22f,2.8f,1.7f),metalMat);
            if(side<0)leftDoor=d.transform;else rightDoor=d.transform;
            for(int j=0;j<3;j++)Box("Shutter rib",new Vector3(side*4.74f,.55f+j*.76f,1),new Vector3(.06f,.07f,1.62f),trimMat,false);
            // Emergency wall lights: simple emissive fixtures, no real-time point lights.
            Box("Hall lamp housing",new Vector3(side*10,2.95f,1),new Vector3(.8f,.12f,.7f),trimMat,false);
            var lamp=Box("Hall lamp",new Vector3(side*10,2.86f,1),new Vector3(.55f,.045f,.42f),MakeMat(new Color(.63f,.55f,.37f),0,.2f,true),false);
        }
        // Office props and silhouette-breaking geometry
        Box("Desk top",new Vector3(0,1.05f,2.15f),new Vector3(4.2f,.22f,1.35f),metalMat);
        Box("Desk front",new Vector3(0,.52f,2.72f),new Vector3(4.2f,.95f,.18f),trimMat);
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box("Desk leg",new Vector3(x*1.8f,.28f,2.15f+z*.48f),new Vector3(.12f,.56f,.12f),metalMat);
        Box("Monitor casing",new Vector3(0,1.85f,2.45f),new Vector3(1.8f,1.12f,.18f),trimMat,false);
        Box("Monitor screen",new Vector3(0,1.86f,2.34f),new Vector3(1.58f,.83f,.035f),MakeMat(new Color(.035f,.10f,.12f),.12f,.18f,true),false);
        for(int i=0;i<5;i++)
        {
            Box("Wall acoustic panel",new Vector3(-3.8f+i*1.9f,2.45f,-4.79f),new Vector3(1.4f,.75f,.1f),i%2==0?trimMat:metalMat,false);
        }
        var lampLight=new GameObject("Office overhead light").AddComponent<Light>(); lampLight.type=LightType.Directional;lampLight.intensity=.42f;lampLight.color=new Color(.70f,.78f,1f);lampLight.transform.rotation=Quaternion.Euler(48,-25,0);lampLight.shadows=LightShadows.Hard;
        // Camera housings at corridor ends (visual navigation cues)
        foreach(int side in new[]{-1,1})
        {var cam=GameObject.CreatePrimitive(PrimitiveType.Cylinder);cam.name="CCTV housing";cam.transform.position=new Vector3(side*15,2.8f,-1.5f);cam.transform.localScale=new Vector3(.24f,.34f,.24f);cam.transform.rotation=Quaternion.Euler(0,0,90);cam.GetComponent<Renderer>().sharedMaterial=metalMat;}
    }

    void BuildPlayer()
    {
        playerRoot=new GameObject("First Person Security Guard").transform;playerRoot.position=new Vector3(0,0,-1.2f);
        controller=playerRoot.gameObject.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.34f;controller.center=new Vector3(0,.9f,0);controller.stepOffset=.25f;controller.slopeLimit=45;controller.skinWidth=.04f;
        cameraRig=new GameObject("First Person Camera").transform;cameraRig.SetParent(playerRoot,false);cameraRig.localPosition=new Vector3(0,1.58f,0);
        gameCamera=cameraRig.gameObject.AddComponent<Camera>();gameCamera.tag="MainCamera";gameCamera.fieldOfView=72;gameCamera.nearClipPlane=.08f;gameCamera.farClipPlane=70;gameCamera.clearFlags=CameraClearFlags.SolidColor;gameCamera.backgroundColor=new Color(.025f,.035f,.05f);
        gameCamera.gameObject.AddComponent<AudioListener>();
        flashlight = gameCamera.gameObject.AddComponent<Light>(); flashlight.type = LightType.Spot; flashlight.range = 14f; flashlight.spotAngle = 58f; flashlight.intensity = 2.3f; flashlight.shadows = LightShadows.None; flashlight.color = new Color(.72f,.82f,1f); flashlight.enabled = false;
    }

    Transform MakeThreat(string name, Color color, Vector3 pos)
    {
        var root=new GameObject(name).transform;root.position=pos;
        var bodyCollider=root.gameObject.AddComponent<CapsuleCollider>();bodyCollider.height=2.7f;bodyCollider.radius=.52f;bodyCollider.center=new Vector3(0,1.35f,0);
        var body=GameObject.CreatePrimitive(PrimitiveType.Capsule);body.name=name+" body";body.transform.SetParent(root,false);body.transform.localPosition=new Vector3(0,.95f,0);body.transform.localScale=new Vector3(.8f,1.2f,.7f);body.GetComponent<Renderer>().sharedMaterial=MakeMat(color,.55f,.32f);
        var head=GameObject.CreatePrimitive(PrimitiveType.Sphere);head.name=name+" mask";head.transform.SetParent(root,false);head.transform.localPosition=new Vector3(0,1.85f,.08f);head.transform.localScale=new Vector3(.95f,.8f,.75f);head.GetComponent<Renderer>().sharedMaterial=MakeMat(new Color(.16f,.18f,.19f),.6f,.45f);
        foreach(float x in new[]{-.23f,.23f}){var eye=GameObject.CreatePrimitive(PrimitiveType.Sphere);eye.name="glowing eyes";eye.transform.SetParent(root,false);eye.transform.localPosition=new Vector3(x,1.9f,.43f);eye.transform.localScale=Vector3.one*.13f;eye.GetComponent<Renderer>().sharedMaterial=eyeMat;}
        foreach(var c in root.GetComponentsInChildren<Collider>())Destroy(c);
        return root;
    }
    void BuildThreats(){leftThreat=MakeThreat("The Warden",new Color(.24f,.29f,.31f),new Vector3(-15,0,1));rightThreat=MakeThreat("The Brass Guest",new Color(.30f,.22f,.13f),new Vector3(15,0,1));}
    void BuildAudio()
    {
        ambienceClip=Resources.Load<AudioClip>("Audio/CC0/DarkDrone");
        monsterGrowlClip=Resources.Load<AudioClip>("Audio/CC0/MonsterGrowl");
        zombieGroanClip=Resources.Load<AudioClip>("Audio/CC0/ZombieGroan");
        doorSlamClip=Resources.Load<AudioClip>("Audio/CC0/HauntedDoorSlam");
        var ambienceObject=new GameObject("CC0 horror ambience");ambienceObject.transform.SetParent(transform,false);
        ambienceSource=ambienceObject.AddComponent<AudioSource>();ambienceSource.clip=ambienceClip;ambienceSource.loop=true;ambienceSource.volume=.12f;ambienceSource.spatialBlend=0;
        if(ambienceClip)ambienceSource.Play();
        sfxSource=gameCamera.gameObject.AddComponent<AudioSource>();sfxSource.playOnAwake=false;sfxSource.spatialBlend=0;sfxSource.volume=.72f;
        leftThreatAudio=MakeThreatAudio(leftThreat);rightThreatAudio=MakeThreatAudio(rightThreat);
    }
    AudioSource MakeThreatAudio(Transform threat)
    {
        var source=threat.gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1f;source.minDistance=2f;source.maxDistance=24f;source.rolloffMode=AudioRolloffMode.Linear;source.volume=.8f;return source;
    }
    void PlayThreatSound(AudioSource source)
    {
        if(source&&monsterGrowlClip&&!source.isPlaying)source.PlayOneShot(monsterGrowlClip,Random.Range(.55f,.9f));
    }

    void BuildHUD()
    {
        var eventObj=new GameObject("Touch Event System");eventObj.AddComponent<EventSystem>();eventObj.AddComponent<StandaloneInputModule>();
        var canvasObj=new GameObject("Nightwatch HUD");var canvas=canvasObj.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvasObj.AddComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;canvasObj.GetComponent<CanvasScaler>().referenceResolution=new Vector2(960,540);canvasObj.AddComponent<GraphicRaycaster>();
        statusText=Label(canvas.transform,"12:00 AM\nNIGHT 1",22,TextAnchor.UpperLeft,new Vector2(25,-20),new Vector2(210,70));
        var powerBack=Panel(canvas.transform,new Vector2(1,0),new Vector2(-25,24),new Vector2(220,34),new Color(.02f,.03f,.04f,.8f));powerFill=Panel(powerBack.transform,new Vector2(0,0),new Vector2(3,3),new Vector2(210,24),new Color(.3f,.85f,.46f,.95f)).GetComponent<Image>();
        Label(canvas.transform,"ENERGÍA",15,TextAnchor.LowerRight,new Vector2(-25,64),new Vector2(220,26));
        cameraText=Label(canvas.transform,"",18,TextAnchor.UpperCenter,new Vector2(0,-18),new Vector2(520,45));cameraText.gameObject.SetActive(false);
        messageText=Label(canvas.transform,"",26,TextAnchor.MiddleCenter,Vector2.zero,new Vector2(620,180));messageText.gameObject.SetActive(false);
        monitorShade=Panel(canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(960,540),new Color(.02f,.08f,.09f,.28f)).GetComponent<Image>();monitorShade.gameObject.SetActive(false);
        // Look gesture area is created before buttons so controls stay tappable above it.
        var look=Panel(canvas.transform,new Vector2(1,0),new Vector2(-250,100),new Vector2(400,330),new Color(0,0,0,.01f));look.AddComponent<TouchLookArea>();
        // Right-side buttons: doors, light and surveillance tablet.
        leftDoorButton=MakeButton(canvas.transform,"PUERTA OESTE",new Vector2(1,0),new Vector2(-235,165),new Vector2(205,68),ToggleLeftDoor);
        rightDoorButton=MakeButton(canvas.transform,"PUERTA ESTE",new Vector2(1,0),new Vector2(-20,165),new Vector2(205,68),ToggleRightDoor);
        lightButton=MakeButton(canvas.transform,"LUZ",new Vector2(1,0),new Vector2(-235,88),new Vector2(205,62),ToggleLight);
        monitorButton=MakeButton(canvas.transform,"CÁMARAS",new Vector2(.5f,0),new Vector2(0,18),new Vector2(230,70),ToggleMonitor);
        MakeButton(canvas.transform,"SIGUIENTE CAM",new Vector2(.5f,0),new Vector2(0,98),new Vector2(230,58),NextCamera);
        // Joystick left bottom
        var stickObj=Panel(canvas.transform,new Vector2(0,0),new Vector2(38,34),new Vector2(156,156),new Color(.08f,.12f,.16f,.48f));moveStick=stickObj.AddComponent<SimpleTouchStick>();
        var knob=Panel(stickObj.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(62,62),new Color(.7f,.8f,.9f,.55f));moveStick.knob=knob.GetComponent<RectTransform>();
        Label(canvas.transform,"MOVER",14,TextAnchor.LowerLeft,new Vector2(49,195),new Vector2(130,25));
    }
    Text Label(Transform parent,string text,int size,TextAnchor align,Vector2 pos,Vector2 sizeDelta)
    {var o=new GameObject("Text");o.transform.SetParent(parent,false);var t=o.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("Arial.ttf");t.text=text;t.fontSize=size;t.color=Color.white;t.alignment=align;t.raycastTarget=false;var r=t.rectTransform;r.anchorMin=new Vector2(0,1);r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=pos;r.sizeDelta=sizeDelta;return t;}
    GameObject Panel(Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Color color)
    {var o=new GameObject("Panel");o.transform.SetParent(parent,false);var im=o.AddComponent<Image>();im.color=color;im.raycastTarget=(color.a>.01f);var r=o.GetComponent<RectTransform>();r.anchorMin=anchor;r.anchorMax=anchor;r.pivot=anchor;r.anchoredPosition=pos;r.sizeDelta=size;return o;}
    Button MakeButton(Transform parent,string text,Vector2 anchor,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action)
    {var o=Panel(parent,anchor,pos,size,new Color(.055f,.075f,.09f,.88f));var b=o.AddComponent<Button>();b.targetGraphic=o.GetComponent<Image>();b.onClick.AddListener(action);var t=Label(o.transform,text,16,TextAnchor.MiddleCenter,Vector2.zero,size- new Vector2(14,8));var r=t.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=Vector2.zero;return b;}

    void ToggleLeftDoor(){if(!ended){leftClosed=!leftClosed;PlayDoorSound();}}
    void ToggleRightDoor(){if(!ended){rightClosed=!rightClosed;PlayDoorSound();}}
    void PlayDoorSound(){if(sfxSource&&doorSlamClip)sfxSource.PlayOneShot(doorSlamClip,.45f);}
    void ToggleLight(){if(!ended&&power>0){lightOn=!lightOn;if(flashlight)flashlight.enabled=lightOn;}}
    void ToggleMonitor(){if(!ended&&power>0){monitorOpen=!monitorOpen;cameraText.gameObject.SetActive(monitorOpen);monitorShade.gameObject.SetActive(monitorOpen);}}
    void NextCamera(){if(monitorOpen){cameraIndex=(cameraIndex+1)%cameraNames.Length;cameraText.text=cameraNames[cameraIndex];}}

    void Update()
    {
        if(ended){if(scareTime>0){scareTime-=Time.deltaTime;gameCamera.fieldOfView=72+Mathf.Sin(Time.time*35)*4;}return;}
        if(power<=0){power=0;leftClosed=rightClosed=false;monitorOpen=false;lightOn=false;if(flashlight)flashlight.enabled=false;}
        nightTimer+=Time.deltaTime;aiTimer+=Time.deltaTime;
        float drain = .035f + (monitorOpen ? .19f : 0f) + (leftClosed ? .21f : 0f) + (rightClosed ? .21f : 0f) + (lightOn ? .12f : 0f);
        power=Mathf.Max(0,power-drain*Time.deltaTime);
        if(aiTimer>9f){aiTimer=0;AdvanceThreats();}
        if(nightTimer>=NightLength){Win();return;}
        MovePlayer();UpdateDoors();UpdateCamera();UpdateHUD();
    }
    void MovePlayer()
    {
        if(monitorOpen)return;
        float x=moveStick?SimpleTouchStick.Axis.x:0,y=moveStick?SimpleTouchStick.Axis.y:0;
        if(Input.GetKey(KeyCode.A))x=-1;if(Input.GetKey(KeyCode.D))x=1;if(Input.GetKey(KeyCode.W))y=1;if(Input.GetKey(KeyCode.S))y=-1;
        Vector3 move=playerRoot.right*x+playerRoot.forward*y;
        if(move.sqrMagnitude>1)move.Normalize();
        controller.Move(move*2.1f*Time.deltaTime+Vector3.down*2.2f*Time.deltaTime);
        playerRoot.position=new Vector3(Mathf.Clamp(playerRoot.position.x,-3.9f,3.9f),playerRoot.position.y,Mathf.Clamp(playerRoot.position.z,-3.7f,2.75f));
    }
    public void LookDrag(Vector2 delta){if(monitorOpen||ended)return;yaw+=delta.x*.18f;pitch=Mathf.Clamp(pitch-delta.y*.14f,-27,24);}
    void UpdateCamera()
    {
        if(monitorOpen){var p=cameraPositions[cameraIndex];gameCamera.transform.position=p;Vector3 focus=cameraIndex==1?new Vector3(-5,1,1):cameraIndex==2?new Vector3(5,1,1):new Vector3(0,1,0);gameCamera.transform.rotation=Quaternion.LookRotation(focus-p,Vector3.up);gameCamera.fieldOfView=58;}
        else{gameCamera.transform.localPosition=new Vector3(0,1.58f,0);gameCamera.transform.localRotation=Quaternion.Euler(pitch,yaw,0);gameCamera.fieldOfView=72;}
    }
    void UpdateDoors()
    {
        if(leftDoor)leftDoor.position=new Vector3(-4.88f,leftClosed?1.4f:4.5f,1);
        if(rightDoor)rightDoor.position=new Vector3(4.88f,rightClosed?1.4f:4.5f,1);
        leftDoorButton.GetComponentInChildren<Text>().text=leftClosed?"OESTE: CERRADA":"PUERTA OESTE";
        rightDoorButton.GetComponentInChildren<Text>().text=rightClosed?"ESTE: CERRADA":"PUERTA ESTE";
        lightButton.GetComponentInChildren<Text>().text=lightOn?"LUZ: ENCENDIDA":"LUZ";
    }
    void AdvanceThreats()
    {
        float difficulty=Mathf.Clamp01(nightTimer/NightLength);
        if(leftThreat){float x=leftThreat.position.x;if(x<-6){x+=Mathf.Lerp(1.2f,2.1f,difficulty);if(Random.value<.48f)PlayThreatSound(leftThreatAudio);}else if(!leftClosed){TriggerLoss("EL VIGILANTE TE ENCONTRÓ");return;}else{x=-14;if(leftThreatAudio)leftThreatAudio.Stop();}leftThreat.position=new Vector3(x,0,1);}
        if(rightThreat){float x=rightThreat.position.x;if(x>6){if(Random.value<Mathf.Lerp(.45f,.8f,difficulty)){x-=Mathf.Lerp(1.2f,2.2f,difficulty);if(Random.value<.55f)PlayThreatSound(rightThreatAudio);}}else if(!rightClosed){TriggerLoss("LA FIGURA TE ENCONTRÓ");return;}else{x=14;if(rightThreatAudio)rightThreatAudio.Stop();}rightThreat.position=new Vector3(x,0,1);}
        // Face the office when close to the entrance
        if(leftThreat)leftThreat.rotation=Quaternion.Euler(0,90,0);if(rightThreat)rightThreat.rotation=Quaternion.Euler(0,-90,0);
    }
    void UpdateHUD()
    {
        int hour=Mathf.Min(6,Mathf.FloorToInt(nightTimer/NightLength*6));string time=hour==0?"12:00 AM":hour+":00 AM";
        statusText.text=time+"\nNOCHE 1";
        powerFill.rectTransform.anchorMin=new Vector2(0,0);powerFill.rectTransform.anchorMax=new Vector2(power/100f,1);powerFill.rectTransform.offsetMin=new Vector2(3,3);powerFill.rectTransform.offsetMax=new Vector2(-3,-3);
        powerFill.color=power>40?new Color(.3f,.85f,.46f):power>18?new Color(1f,.63f,.12f):new Color(.95f,.16f,.12f);
        if(monitorOpen)cameraText.text=cameraNames[cameraIndex]+"\nToca SIGUIENTE CAM para cambiar";
        if(power<=0&&!monitorOpen){messageText.gameObject.SetActive(true);messageText.text="SIN ENERGÍA\nNo hagas ruido...";}
    }
    void TriggerLoss(string text){ended=true;monitorOpen=false;messageText.gameObject.SetActive(true);messageText.text=text+"\n\nNOCHE TERMINADA";scareTime=2.5f;if(ambienceSource)ambienceSource.Stop();if(sfxSource){if(zombieGroanClip)sfxSource.PlayOneShot(zombieGroanClip,.9f);else if(monsterGrowlClip)sfxSource.PlayOneShot(monsterGrowlClip,1f);}}
    void Win(){ended=true;messageText.gameObject.SetActive(true);messageText.text="6:00 AM\n\nSOBREVIVISTE A LA NOCHE";}
}

public class SimpleTouchStick : MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
{
    public RectTransform knob;public static Vector2 Axis{get;private set;}Vector2 center;
    void Start(){center=GetComponent<RectTransform>().sizeDelta*.5f;}
    public void OnPointerDown(PointerEventData e){OnDrag(e);}
    public void OnDrag(PointerEventData e){Vector2 local;if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,e.position,e.pressEventCamera,out local)){Vector2 d=Vector2.ClampMagnitude(local-center,53);Axis=d/53;if(knob)knob.anchoredPosition=d;}}
    public void OnPointerUp(PointerEventData e){Axis=Vector2.zero;if(knob)knob.anchoredPosition=Vector2.zero;}
}
public class TouchLookArea:MonoBehaviour,IPointerDownHandler,IDragHandler
{
    Vector2 last;bool held;
    public void OnPointerDown(PointerEventData e){last=e.position;held=true;}
    public void OnDrag(PointerEventData e){if(!held)return;Vector2 d=e.position-last;last=e.position;if(WorldBootstrap.Instance)WorldBootstrap.Instance.LookDrag(d);}
}

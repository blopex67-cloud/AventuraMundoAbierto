using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class WorldBootstrap : MonoBehaviour
{
    public static WorldBootstrap Instance;
    CharacterController controller;
    Transform playerRoot, cameraRig, leftThreat, rightThreat;
    Camera gameCamera;
    Light flashlight;
    SimpleTouchStick moveStick;
    Text statusText, cameraText, messageText, missionText;
    Image powerFill, monitorShade;
    Slider realismSlider;
    Text realismValueText;
    Button monitorButton, lightButton, nextCameraButton;
    GameObject startOverlay, gameplayHud;
    Text startTitle, startDescription;
    Button startButton;
    bool gameStarted, objectiveCamera, objectiveLight;
    AudioSource ambienceSource, sfxSource, leftThreatAudio, rightThreatAudio;
    AudioClip ambienceClip, monsterGrowlClip, zombieGroanClip;
    Material wallMat, floorMat, metalMat, trimMat, eyeMat;
    static Material fallbackSourceMaterial;
    static Texture2D tileNoiseTexture;
    bool emergencyGroundCreated;
    float verticalVelocity;
    string startupFailure;
    bool monitorOpen, lightOn, ended;
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
        AddEmergencyGroundCollider();
        EnsureFallbackCamera();
    }
    void Start()
    {
        Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        QualitySettings.shadows = ShadowQuality.HardOnly; QualitySettings.shadowDistance = 22;
        try
        {
            // Create the camera before generating the rest of the world. A failure later
            // can no longer leave Unity displaying only its default grey screen.
            BuildPlayer();
            BuildMaterials(); BuildOffice(); BuildThreats(); BuildAudio(); BuildHUD(); UpdateHUD();
            float savedRealism=PlayerPrefs.GetFloat("Nightwatch.Realism", .35f);ApplyRealismLevel(savedRealism, false);if(realismSlider)realismSlider.SetValueWithoutNotify(savedRealism);if(realismValueText)realismValueText.text="REALISMO · "+Mathf.RoundToInt(savedRealism*100)+"%";
        }
        catch (System.Exception ex)
        {
            startupFailure = ex.GetType().Name + ": " + ex.Message;
            Debug.LogError("GAME STARTUP FAILED: " + ex);
        }
    }
    void EnsureFallbackCamera()
    {
        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            var cameraObject = new GameObject("Emergency Main Camera");
            cameraObject.tag = "MainCamera";
            gameCamera = cameraObject.AddComponent<Camera>();
        }
        gameCamera.enabled = true;
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = new Color(.025f,.035f,.05f);
        gameCamera.fieldOfView = 72f;
        gameCamera.nearClipPlane = .08f;
        gameCamera.farClipPlane = 70f;
        if (gameCamera.GetComponent<AudioListener>() == null)
            gameCamera.gameObject.AddComponent<AudioListener>();
        if (cameraRig == null && playerRoot == null)
            gameCamera.transform.SetPositionAndRotation(new Vector3(0,1.58f,-1.2f), Quaternion.identity);
    }
    void OnGUI()
    {
        if (string.IsNullOrEmpty(startupFailure)) return;
        GUI.color = Color.white;
        GUI.Box(new Rect(12, 12, Screen.width - 24, 100), "ERROR AL INICIAR EL JUEGO\n" + startupFailure + "\nConsulta el log de Unity para el detalle.");
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
        // Prefer serialized Resources anchors: runtime Shader.Find alone lets Unity
        // strip Standard/URP shaders from Android builds, producing bright magenta.
        if (fallbackSourceMaterial == null)
        {
            var anchor = Resources.Load<Material>("RuntimeShaders/StandardAnchor");
            if (anchor == null) anchor = Resources.Load<Material>("RuntimeShaders/UnlitAnchor");
            if (anchor != null && anchor.shader != null) fallbackSourceMaterial = anchor;
        }
        Shader sh = fallbackSourceMaterial != null ? fallbackSourceMaterial.shader : null;
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh == null)
        {
            Debug.LogError("No hay shader de materiales incluido. Mantengo material predeterminado; revisa RuntimeShaders.");
            return null;
        }
        var m = new Material(sh);
        m.color = color;
        if(m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if(m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if(m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        // Small tileable grain prevents the office materials from looking flat.
        if (tileNoiseTexture == null) tileNoiseTexture = CreateTileNoise();
        if (tileNoiseTexture != null)
        {
            if(m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tileNoiseTexture);
            if(m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tileNoiseTexture);
        }
        if(emission && m.HasProperty("_EmissionColor")){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.7f);}
        return m;
    }
    Texture2D CreateTileNoise()
    {
        const int size=64;
        var texture=new Texture2D(size,size,TextureFormat.RGB24,false,true);
        texture.name="Runtime subtle surface grain";
        texture.wrapMode=TextureWrapMode.Repeat;
        texture.filterMode=FilterMode.Bilinear;
        var pixels=new Color[size*size];
        var rng=new System.Random(1741);
        for(int i=0;i<pixels.Length;i++){float v=.84f+(float)rng.NextDouble()*.30f;pixels[i]=new Color(v,v,v,1);}
        texture.SetPixels(pixels);texture.Apply(false,true);
        return texture;
    }
    void AddEmergencyGroundCollider()
    {
        if (emergencyGroundCreated) return;
        emergencyGroundCreated = true;
        // Keep a real collision surface even if constructing the visible office fails.
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Emergency Ground Collision";
        ground.transform.position = new Vector3(0, -.30f, 0);
        ground.transform.localScale = new Vector3(100f, .5f, 100f);
        var renderer = ground.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
        ground.layer = 0;
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
            Box("Hall floor",new Vector3(side*16,-.22f,1),new Vector3(22,.44f,7),floorMat);
            Box("Hall ceiling",new Vector3(side*16,3.5f,1),new Vector3(22,.3f,7),trimMat);
            Box("Hall outer wall",new Vector3(side*27,1.6f,1),new Vector3(.3f,3.4f,7),wallMat);
            Box("Hall rear wall",new Vector3(side*16,1.6f,-2.5f),new Vector3(22,3.4f,.3f),wallMat);
            Box("Hall front wall",new Vector3(side*16,1.6f,4.5f),new Vector3(22,3.4f,.3f),wallMat);
            // Emergency wall lights: simple emissive fixtures, no real-time point lights.
            Box("Hall lamp housing",new Vector3(side*10,2.95f,1),new Vector3(.8f,.12f,.7f),trimMat,true);
            var lamp=Box("Hall lamp",new Vector3(side*10,2.86f,1),new Vector3(.55f,.045f,.42f),MakeMat(new Color(.63f,.55f,.37f),0,.2f,true),true);
        }
        // Office props and silhouette-breaking geometry
        Box("Desk top",new Vector3(0,1.05f,2.15f),new Vector3(4.2f,.22f,1.35f),metalMat);
        Box("Desk front",new Vector3(0,.52f,2.72f),new Vector3(4.2f,.95f,.18f),trimMat);
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box("Desk leg",new Vector3(x*1.8f,.28f,2.15f+z*.48f),new Vector3(.12f,.56f,.12f),metalMat);
        Box("Monitor casing",new Vector3(0,1.85f,2.45f),new Vector3(1.8f,1.12f,.18f),trimMat,true);
        Box("Monitor screen",new Vector3(0,1.86f,2.34f),new Vector3(1.58f,.83f,.035f),MakeMat(new Color(.035f,.10f,.12f),.12f,.18f,true),true);
        for(int i=0;i<5;i++)
        {
            Box("Wall acoustic panel",new Vector3(-3.8f+i*1.9f,2.45f,-4.79f),new Vector3(1.4f,.75f,.1f),i%2==0?trimMat:metalMat,true);
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
        EnsureFallbackCamera();
        cameraRig=gameCamera.transform;cameraRig.SetParent(playerRoot,false);cameraRig.localPosition=new Vector3(0,1.58f,0);cameraRig.localRotation=Quaternion.identity;
        gameCamera.tag="MainCamera";gameCamera.enabled=true;gameCamera.fieldOfView=72;gameCamera.nearClipPlane=.08f;gameCamera.farClipPlane=70;gameCamera.clearFlags=CameraClearFlags.SolidColor;gameCamera.backgroundColor=new Color(.025f,.035f,.05f);
        flashlight = gameCamera.GetComponent<Light>();if(!flashlight)flashlight=gameCamera.gameObject.AddComponent<Light>(); flashlight.type = LightType.Spot; flashlight.range = 14f; flashlight.spotAngle = 58f; flashlight.intensity = 2.3f; flashlight.shadows = LightShadows.None; flashlight.color = new Color(.72f,.82f,1f); flashlight.enabled = false;
    }

    Transform MakeThreat(string name, Color color, Vector3 pos)
    {
        var root=new GameObject(name).transform;root.position=pos;
        var bodyCollider=root.gameObject.AddComponent<CapsuleCollider>();bodyCollider.height=2.7f;bodyCollider.radius=.52f;bodyCollider.center=new Vector3(0,1.35f,0);
        var actor=root.gameObject.AddComponent<MimicActor>();actor.modelFile="the_mimic_dlc.glb";actor.modelScale=1f;
        return root;
    }
    void BuildThreats(){leftThreat=MakeThreat("The Warden",new Color(.24f,.29f,.31f),new Vector3(-23,0,1));rightThreat=MakeThreat("The Brass Guest",new Color(.30f,.22f,.13f),new Vector3(23,0,1));}
    void BuildAudio()
    {
        ambienceClip=Resources.Load<AudioClip>("Audio/CC0/DarkDrone");
        monsterGrowlClip=Resources.Load<AudioClip>("Audio/CC0/MonsterGrowl");
        zombieGroanClip=Resources.Load<AudioClip>("Audio/CC0/ZombieGroan");
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
        var canvasObj=new GameObject("Nightwatch HUD");var canvas=canvasObj.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvasObj.AddComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;canvasObj.GetComponent<CanvasScaler>().referenceResolution=new Vector2(960,540);canvasObj.GetComponent<CanvasScaler>().matchWidthOrHeight=.5f;canvasObj.AddComponent<GraphicRaycaster>();
        var hud=new GameObject("Gameplay Interface",typeof(RectTransform));hud.transform.SetParent(canvas.transform,false);gameplayHud=hud;Transform hudRoot=hud.transform;
        // Critical: child anchors only work if the HUD root stretches over the whole canvas.
        var hudRect=hud.GetComponent<RectTransform>();hudRect.anchorMin=Vector2.zero;hudRect.anchorMax=Vector2.one;hudRect.offsetMin=Vector2.zero;hudRect.offsetMax=Vector2.zero;hudRect.localScale=Vector3.one;
        // Touching open screen space rotates the view; buttons and joystick sit above this layer.
        var look=Panel(hudRoot,new Vector2(.2f,0),Vector2.zero,new Vector2(520,540),new Color(0,0,0,.001f));
        var lookRect=look.GetComponent<RectTransform>();lookRect.anchorMin=new Vector2(.20f,.12f);lookRect.anchorMax=new Vector2(.70f,.88f);lookRect.offsetMin=Vector2.zero;lookRect.offsetMax=Vector2.zero;look.GetComponent<Image>().raycastTarget=true;look.AddComponent<TouchLookArea>();
        var statusCard=Panel(hudRoot,new Vector2(0,1),new Vector2(18,-18),new Vector2(188,64),new Color(.025f,.035f,.05f,.88f));statusCard.GetComponent<Image>().raycastTarget=false;
        statusText=AnchoredLabel(statusCard.transform,"12:00 AM\nNOCHE 1",20,TextAnchor.MiddleLeft,new Vector2(0,1),new Vector2(12,-8),new Vector2(164,48));
        var powerCard=Panel(hudRoot,new Vector2(1,1),new Vector2(-18,-18),new Vector2(228,64),new Color(.025f,.035f,.05f,.88f));powerCard.GetComponent<Image>().raycastTarget=false;
        AnchoredLabel(powerCard.transform,"ENERGÍA",13,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(12,-7),new Vector2(200,18));
        var powerBack=Panel(powerCard.transform,new Vector2(0,0),new Vector2(12,10),new Vector2(204,28),new Color(.02f,.03f,.04f,.9f));powerFill=Panel(powerBack.transform,new Vector2(0,0),new Vector2(3,3),new Vector2(198,22),new Color(.3f,.85f,.46f,.95f)).GetComponent<Image>();
        cameraText=AnchoredLabel(hudRoot,"",18,TextAnchor.UpperCenter,new Vector2(.5f,1),new Vector2(0,-20),new Vector2(520,45));cameraText.gameObject.SetActive(false);
        messageText=AnchoredLabel(hudRoot,"",26,TextAnchor.MiddleCenter,new Vector2(.5f,.5f),Vector2.zero,new Vector2(620,180));messageText.gameObject.SetActive(false);
        monitorShade=Panel(hudRoot,new Vector2(.5f,.5f),Vector2.zero,new Vector2(960,540),new Color(.02f,.08f,.09f,.28f)).GetComponent<Image>();monitorShade.raycastTarget=false;monitorShade.gameObject.SetActive(false);
        // Right-side actions use a fixed dock so they're never under the mission card.
        var actionsCard=Panel(hudRoot,new Vector2(1,0),new Vector2(-18,18),new Vector2(410,84),new Color(.025f,.035f,.05f,.86f));actionsCard.GetComponent<Image>().raycastTarget=false;
        lightButton=MakeButton(actionsCard.transform,"LINTERNA",new Vector2(0,0),new Vector2(10,12),new Vector2(188,58),ToggleLight);
        monitorButton=MakeButton(actionsCard.transform,"CÁMARAS",new Vector2(1,0),new Vector2(-10,12),new Vector2(188,58),ToggleMonitor);
        nextCameraButton=MakeButton(actionsCard.transform,"SIGUIENTE CAM",new Vector2(1,0),new Vector2(-10,82),new Vector2(188,48),NextCamera);nextCameraButton.gameObject.SetActive(false);
        var missionCard=Panel(hudRoot,new Vector2(1,1),new Vector2(-18,-94),new Vector2(264,118),new Color(.025f,.035f,.05f,.84f));missionCard.GetComponent<Image>().raycastTarget=false;
        missionText=AnchoredLabel(missionCard.transform,"OBJETIVOS\n□ Revisa las cámaras\n□ Usa la linterna\n□ Sobrevive hasta las 6 AM",14,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(12,-10),new Vector2(240,100));
        var realismCard=Panel(hudRoot,new Vector2(0,1),new Vector2(18,-92),new Vector2(188,62),new Color(.025f,.035f,.05f,.86f));realismCard.GetComponent<Image>().raycastTarget=false;
        realismValueText=AnchoredLabel(realismCard.transform,"REALISMO · 35%",12,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(10,-6),new Vector2(168,18));
        realismSlider=CreateRealismSlider(realismCard.transform);
        // Movement stick stays in its own lower-left zone, away from camera look and action buttons.
        var stickObj=Panel(hudRoot,new Vector2(0,0),new Vector2(22,22),new Vector2(142,142),new Color(.04f,.07f,.10f,.62f));moveStick=stickObj.AddComponent<SimpleTouchStick>();
        var knob=Panel(stickObj.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(58,58),new Color(.7f,.8f,.9f,.75f));moveStick.knob=knob.GetComponent<RectTransform>();
        AnchoredLabel(hudRoot,"MOVER",13,TextAnchor.LowerLeft,new Vector2(0,0),new Vector2(27,172),new Vector2(132,22));
        hud.SetActive(false);
        startOverlay=Panel(canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(960,540),new Color(.015f,.025f,.04f,.98f));
        var full=startOverlay.GetComponent<RectTransform>();full.anchorMin=Vector2.zero;full.anchorMax=Vector2.one;full.offsetMin=Vector2.zero;full.offsetMax=Vector2.zero;
        startTitle=AnchoredLabel(startOverlay.transform,"NOCHE DE VIGILANCIA",36,TextAnchor.MiddleCenter,new Vector2(.5f,.5f),new Vector2(0,-30),new Vector2(800,65));
        startDescription=AnchoredLabel(startOverlay.transform,"Sobrevive hasta las 6 AM.\nRevisa las cámaras, usa la linterna y vigila los pasillos.\nArrastra la pantalla para mirar; usa el control izquierdo para moverte.",20,TextAnchor.MiddleCenter,new Vector2(.5f,.5f),new Vector2(0,-115),new Vector2(780,110));
        startButton=MakeButton(startOverlay.transform,"INICIAR MISIÓN",new Vector2(.5f,.5f),new Vector2(0,95),new Vector2(270,72),StartMission);
        UpdateStartScreen();
    }
    Text AnchoredLabel(Transform parent,string text,int size,TextAnchor align,Vector2 anchor,Vector2 pos,Vector2 sizeDelta)
    {var t=Label(parent,text,size,align,pos,sizeDelta);var r=t.rectTransform;r.anchorMin=anchor;r.anchorMax=anchor;r.pivot=anchor;r.anchoredPosition=pos;r.sizeDelta=sizeDelta;return t;}
    Text Label(Transform parent,string text,int size,TextAnchor align,Vector2 pos,Vector2 sizeDelta)
    {var o=new GameObject("Text");o.transform.SetParent(parent,false);var t=o.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("Arial.ttf");t.text=text;t.fontSize=size;t.color=Color.white;t.alignment=align;t.raycastTarget=false;var r=t.rectTransform;r.anchorMin=new Vector2(0,1);r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=pos;r.sizeDelta=sizeDelta;return t;}
    GameObject Panel(Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Color color)
    {var o=new GameObject("Panel");o.transform.SetParent(parent,false);var im=o.AddComponent<Image>();im.color=color;im.raycastTarget=(color.a>.01f);var r=o.GetComponent<RectTransform>();r.anchorMin=anchor;r.anchorMax=anchor;r.pivot=anchor;r.anchoredPosition=pos;r.sizeDelta=size;return o;}
    Button MakeButton(Transform parent,string text,Vector2 anchor,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action)
    {var o=Panel(parent,anchor,pos,size,new Color(.055f,.075f,.09f,.96f));var image=o.GetComponent<Image>();image.raycastTarget=true;var b=o.AddComponent<Button>();b.targetGraphic=image;b.transition=Selectable.Transition.ColorTint;b.colors=new ColorBlock{normalColor=Color.white,highlightedColor=new Color(.76f,.88f,1f),pressedColor=new Color(.35f,.65f,.9f),selectedColor=Color.white,disabledColor=new Color(.4f,.4f,.4f),colorMultiplier=1f,fadeDuration=.08f};b.onClick.AddListener(action);var t=AnchoredLabel(o.transform,text,15,TextAnchor.MiddleCenter,new Vector2(.5f,.5f),Vector2.zero,size-new Vector2(14,8));return b;}

    Slider CreateRealismSlider(Transform parent)
    {
        var go=new GameObject("Realism power",typeof(RectTransform),typeof(Slider));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(.5f,0);rect.offsetMin=new Vector2(10,8);rect.offsetMax=new Vector2(-10,30);
        var track=new GameObject("Track",typeof(RectTransform),typeof(Image));track.transform.SetParent(go.transform,false);var tr=track.GetComponent<RectTransform>();tr.anchorMin=new Vector2(0,.36f);tr.anchorMax=new Vector2(1,.64f);tr.offsetMin=Vector2.zero;tr.offsetMax=Vector2.zero;track.GetComponent<Image>().color=new Color(.18f,.23f,.3f,1);
        var fill=new GameObject("Fill",typeof(RectTransform),typeof(Image));fill.transform.SetParent(track.transform,false);var fr=fill.GetComponent<RectTransform>();fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=Vector2.zero;fr.offsetMax=Vector2.zero;fill.GetComponent<Image>().color=new Color(.2f,.67f,1f,1);
        var knob=new GameObject("Handle",typeof(RectTransform),typeof(Image));knob.transform.SetParent(go.transform,false);var kr=knob.GetComponent<RectTransform>();kr.sizeDelta=new Vector2(20,24);knob.GetComponent<Image>().color=Color.white;
        var slider=go.GetComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.value=.35f;slider.fillRect=fr;slider.handleRect=kr;slider.targetGraphic=knob.GetComponent<Image>();slider.onValueChanged.AddListener(v=>{ApplyRealismLevel(v,true);if(realismValueText)realismValueText.text="REALISMO · "+Mathf.RoundToInt(v*100)+"%";});return slider;
    }
    void ApplyRealismLevel(float level,bool save)
    {
        level=Mathf.Clamp01(level);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=Color.Lerp(new Color(.20f,.22f,.27f),new Color(.48f,.52f,.58f),level);
        RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.12f,.13f,.16f),new Color(.28f,.30f,.34f),level);
        RenderSettings.ambientGroundColor=Color.Lerp(new Color(.045f,.05f,.06f),new Color(.12f,.12f,.13f),level);
        QualitySettings.shadows=level>.58f?ShadowQuality.HardOnly:ShadowQuality.Disable;
        QualitySettings.shadowDistance=Mathf.Lerp(8f,26f,level);
        foreach(var light in FindObjectsOfType<Light>())
        {light.shadows=level>.58f?LightShadows.Hard:LightShadows.None;if(light.type==LightType.Directional)light.intensity=Mathf.Lerp(.7f,1.25f,level);}
        if(save){PlayerPrefs.SetFloat("Nightwatch.Realism",level);PlayerPrefs.Save();}
    }

    void StartMission()
    {
        if(ended){RestartMission();return;}
        gameStarted=true;
        if(startOverlay)startOverlay.SetActive(false);
        if(gameplayHud)gameplayHud.SetActive(true);
        UpdateMissions();
    }
    void RestartMission()
    {
        nightTimer=0;aiTimer=0;power=100;ended=false;monitorOpen=false;lightOn=false;objectiveCamera=false;objectiveLight=false;verticalVelocity=0;
        if(leftThreat)leftThreat.position=new Vector3(-23,0,1);if(rightThreat)rightThreat.position=new Vector3(23,0,1);
        if(ambienceSource&&ambienceClip&&!ambienceSource.isPlaying)ambienceSource.Play();
        if(flashlight)flashlight.enabled=false;
        if(messageText)messageText.gameObject.SetActive(false);
        if(startOverlay)startOverlay.SetActive(false);
        if(gameplayHud)gameplayHud.SetActive(true);
        gameStarted=true;UpdateHUD();
    }
    void UpdateStartScreen(){if(startButton)startButton.GetComponentInChildren<Text>().text=ended?"VOLVER A JUGAR":"INICIAR MISIÓN";if(startTitle)startTitle.text=ended?"TURNO TERMINADO":"NOCHE DE VIGILANCIA";if(startDescription)startDescription.text=ended?"Tu objetivo: vigilar, conservar energía y llegar a las 6 AM.":"Sobrevive hasta las 6 AM.\nRevisa las cámaras, usa la linterna y vigila los pasillos.\nArrastra la pantalla para mirar; usa el control izquierdo para moverte.";}
    void ToggleLight(){if(!ended&&gameStarted&&power>0){lightOn=!lightOn;objectiveLight=true;if(flashlight)flashlight.enabled=lightOn;}}
    void ToggleMonitor(){if(!ended&&gameStarted&&power>0){monitorOpen=!monitorOpen;objectiveCamera=true;cameraText.gameObject.SetActive(monitorOpen);monitorShade.gameObject.SetActive(monitorOpen);if(nextCameraButton)nextCameraButton.gameObject.SetActive(monitorOpen);}}
    void NextCamera(){if(monitorOpen){cameraIndex=(cameraIndex+1)%cameraNames.Length;cameraText.text=cameraNames[cameraIndex];}}

    void Update()
    {
        if(gameCamera==null){EnsureFallbackCamera();return;}
        if(startupFailure!=null)return;
        if(ended){if(scareTime>0){scareTime-=Time.deltaTime;gameCamera.fieldOfView=72+Mathf.Sin(Time.time*35)*4;}return;}
        if(!gameStarted)return;
        if(power<=0){power=0;monitorOpen=false;lightOn=false;if(flashlight)flashlight.enabled=false;}
        nightTimer+=Time.deltaTime;aiTimer+=Time.deltaTime;
        float drain = .035f + (monitorOpen ? .19f : 0f) + (lightOn ? .12f : 0f);
        power=Mathf.Max(0,power-drain*Time.deltaTime);
        if(aiTimer>9f){aiTimer=0;AdvanceThreats();}
        if(nightTimer>=NightLength){Win();return;}
        MovePlayer();UpdateCamera();UpdateHUD();
    }
    void MovePlayer()
    {
        if(monitorOpen)return;
        float x=moveStick?SimpleTouchStick.Axis.x:0,y=moveStick?SimpleTouchStick.Axis.y:0;
        if(Input.GetKey(KeyCode.A))x=-1;if(Input.GetKey(KeyCode.D))x=1;if(Input.GetKey(KeyCode.W))y=1;if(Input.GetKey(KeyCode.S))y=-1;
        Vector3 cameraForward = cameraRig ? Vector3.ProjectOnPlane(cameraRig.forward, Vector3.up).normalized : playerRoot.forward;
        Vector3 cameraRight = cameraRig ? Vector3.ProjectOnPlane(cameraRig.right, Vector3.up).normalized : playerRoot.right;
        Vector3 move=cameraRight*x+cameraForward*y;
        if(move.sqrMagnitude>1)move.Normalize();
        if (controller == null) return;
        controller.detectCollisions = true;
        controller.minMoveDistance = 0f;
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += Physics.gravity.y * Time.deltaTime;
        controller.Move((move * 2.1f + Vector3.up * verticalVelocity) * Time.deltaTime);
        if (playerRoot.position.y < -2f)
        {
            Debug.LogError("Player fell through collision surface; restoring player to the office.");
            playerRoot.position = new Vector3(0,0,-1.2f);
            verticalVelocity = 0f;
        }
        playerRoot.position=new Vector3(Mathf.Clamp(playerRoot.position.x,-3.9f,3.9f),playerRoot.position.y,Mathf.Clamp(playerRoot.position.z,-3.7f,2.75f));
    }
    public void LookDrag(Vector2 delta){if(monitorOpen||ended)return;yaw+=delta.x*.20f;pitch=Mathf.Clamp(pitch-delta.y*.16f,-35,35);}
    void UpdateCamera()
    {
        if(monitorOpen){var p=cameraPositions[cameraIndex];gameCamera.transform.position=p;Vector3 focus=cameraIndex==1?new Vector3(-5,1,1):cameraIndex==2?new Vector3(5,1,1):new Vector3(0,1,0);gameCamera.transform.rotation=Quaternion.LookRotation(focus-p,Vector3.up);gameCamera.fieldOfView=58;}
        else{gameCamera.transform.localPosition=new Vector3(0,1.58f,0);gameCamera.transform.localRotation=Quaternion.Euler(pitch,yaw,0);gameCamera.fieldOfView=72;}
    }
    void AdvanceThreats()
    {
        float difficulty=Mathf.Clamp01(nightTimer/NightLength);
        if(leftThreat){float x=leftThreat.position.x;if(x<-6){x+=Mathf.Lerp(1.2f,2.1f,difficulty);var actor=leftThreat.GetComponent<MimicActor>();if(actor)actor.PlayWalk();if(Random.value<.48f)PlayThreatSound(leftThreatAudio);}else{TriggerLoss("EL VIGILANTE TE ENCONTRÓ");return;}leftThreat.position=new Vector3(x,0,1);}
        if(rightThreat){float x=rightThreat.position.x;if(x>6){var actor=rightThreat.GetComponent<MimicActor>();if(actor)actor.PlayWalk();if(Random.value<Mathf.Lerp(.45f,.8f,difficulty)){x-=Mathf.Lerp(1.2f,2.2f,difficulty);if(actor)actor.PlayRun();if(Random.value<.55f)PlayThreatSound(rightThreatAudio);}}else{TriggerLoss("LA FIGURA TE ENCONTRÓ");return;}rightThreat.position=new Vector3(x,0,1);}
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
        UpdateMissions();
    }
    void UpdateMissions()
    {
        if(!missionText)return;
        bool survived=nightTimer>=NightLength;
        missionText.text="OBJETIVOS\n"+(objectiveCamera?"✓":"□")+" Revisa las cámaras\n"+(objectiveLight?"✓":"□")+" Usa la linterna\n"+(survived?"✓":"□")+" Sobrevive hasta las 6 AM";
    }
    void TriggerLoss(string text){ended=true;gameStarted=false;monitorOpen=false;messageText.gameObject.SetActive(true);messageText.text=text+"\n\nNOCHE TERMINADA";scareTime=2.5f;var actor=leftThreat&&leftThreat.position.x>-6?leftThreat.GetComponent<MimicActor>():rightThreat?rightThreat.GetComponent<MimicActor>():null;if(actor)actor.PlayJumpscare();if(ambienceSource)ambienceSource.Stop();if(sfxSource){if(zombieGroanClip)sfxSource.PlayOneShot(zombieGroanClip,.9f);else if(monsterGrowlClip)sfxSource.PlayOneShot(monsterGrowlClip,1f);}if(startOverlay){startOverlay.SetActive(true);UpdateStartScreen();}}
    void Win(){objectiveCamera=true;objectiveLight=true;UpdateMissions();ended=true;gameStarted=false;messageText.gameObject.SetActive(true);messageText.text="6:00 AM\n\nSOBREVIVISTE A LA NOCHE";if(startOverlay){startOverlay.SetActive(true);UpdateStartScreen();}}
}

public class SimpleTouchStick : MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
{
    public RectTransform knob;public static Vector2 Axis{get;private set;}Vector2 center;
    void Start(){center=GetComponent<RectTransform>().sizeDelta*.5f;}
    public void OnPointerDown(PointerEventData e){OnDrag(e);}
    public void OnDrag(PointerEventData e){Vector2 local;if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,e.position,e.pressEventCamera,out local)){Vector2 d=Vector2.ClampMagnitude(local-center,53);Axis=d/53;if(knob)knob.anchoredPosition=d;}}
    public void OnPointerUp(PointerEventData e){Axis=Vector2.zero;if(knob)knob.anchoredPosition=Vector2.zero;}
}
public class TouchLookArea:MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
{
    Vector2 last;int activePointer=int.MinValue;
    public void OnPointerDown(PointerEventData e){activePointer=e.pointerId;last=e.position;}
    public void OnDrag(PointerEventData e){if(e.pointerId!=activePointer)return;Vector2 d=e.position-last;last=e.position;if(WorldBootstrap.Instance)WorldBootstrap.Instance.LookDrag(d);}
    public void OnPointerUp(PointerEventData e){if(e.pointerId==activePointer)activePointer=int.MinValue;}
}

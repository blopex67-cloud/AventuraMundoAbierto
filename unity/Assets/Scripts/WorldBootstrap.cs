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
    Text statusText, messageText, missionText;
    Image powerFill;
    Slider realismSlider;
    Text realismValueText;
    Image filmGrainOverlay;
    Button lightButton, whistleButton;
    GameObject startOverlay, gameplayHud;
    Text startTitle, startDescription, animatronicTurnText, whistleText;
    Button startButton;
    bool gameStarted, objectiveCamera, objectiveLight;
    int activeHunterIndex;
    float turnClock, whistleCooldown, whistleStunTimer, realismLevel=.35f;
    Light[] pizzeriaLights;
    Material neonPinkMat, neonCyanMat, neonGoldMat, stageMat;
    static Sprite circularControlSprite;
    AudioSource ambienceSource, sfxSource, leftThreatAudio, rightThreatAudio;
    AudioClip ambienceClip, monsterGrowlClip, zombieGroanClip;
    Material wallMat, floorMat, metalMat, trimMat, eyeMat;
    static Material fallbackSourceMaterial;
    static Texture2D tileNoiseTexture;
    bool emergencyGroundCreated;
    float verticalVelocity;
    string startupFailure;
    bool lightOn, ended;
    float yaw, pitch = 2f, power = 100f, aiTimer, scareTime;
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
            BuildMaterials(); BuildPizzeria(); BuildThreats(); BuildAudio(); BuildHUD(); UpdateHUD();
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
        neonPinkMat=MakeMat(new Color(1f,.035f,.24f),0,.3f,true);
        neonCyanMat=MakeMat(new Color(.02f,.75f,1f),0,.3f,true);
        neonGoldMat=MakeMat(new Color(1f,.48f,.04f),0,.3f,true);
        stageMat=MakeMat(new Color(.16f,.035f,.055f),.05f,.32f);
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
    void BuildPizzeria()
    {
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.22f,.24f,.30f);
        RenderSettings.ambientEquatorColor=new Color(.16f,.17f,.20f);
        RenderSettings.ambientGroundColor=new Color(.055f,.05f,.055f);
        RenderSettings.fog=true;RenderSettings.fogColor=new Color(.055f,.045f,.065f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.006f;

        // Large continuous walkable dining room with a roof and perimeter collision.
        Box("Pizzeria tiled floor",new Vector3(0,-.22f,0),new Vector3(44,.44f,32),floorMat);
        Box("Pizzeria ceiling",new Vector3(0,5.4f,0),new Vector3(44,.32f,32),trimMat);
        Box("Back perimeter wall",new Vector3(0,2.5f,16),new Vector3(44,5,.35f),wallMat);
        Box("Front perimeter wall",new Vector3(0,2.5f,-16),new Vector3(44,5,.35f),wallMat);
        Box("Left perimeter wall",new Vector3(-22,2.5f,0),new Vector3(.35f,5,32),wallMat);
        Box("Right perimeter wall",new Vector3(22,2.5f,0),new Vector3(.35f,5,32),wallMat);

        // Show stage, with safe route down the center and accessible open sides.
        Box("Animatronic stage",new Vector3(0,.22f,12.2f),new Vector3(13,.44f,5.8f),metalMat);
        Box("Stage back",new Vector3(0,2.55f,15.05f),new Vector3(13,4.7f,.25f),stageMat);
        for(int x=-5;x<=5;x+=2)Box("Red stage curtain",new Vector3(x,2.65f,14.83f),new Vector3(1.35f,4.1f,.18f),stageMat);
        for(int x=-6;x<=6;x+=2)Box("Stage footlight",new Vector3(x,.55f,9.28f),new Vector3(.65f,.13f,.14f),x%4==0?neonCyanMat:neonPinkMat,false);

        // Neon sign, strips and pizzeria wayfinding; emissive surfaces plus a few real spotlights.
        Box("Neon sign backing",new Vector3(0,4.55f,15.0f),new Vector3(8,.85f,.22f),trimMat,false);
        Box("NEON PIZZERIA",new Vector3(0,4.55f,14.84f),new Vector3(7.2f,.14f,.08f),neonPinkMat,false);
        Box("Ceiling LED strip left",new Vector3(-10,5.1f,0),new Vector3(.08f,.08f,27),neonCyanMat,false);
        Box("Ceiling LED strip right",new Vector3(10,5.1f,0),new Vector3(.08f,.08f,27),neonPinkMat,false);
        Box("Counter front",new Vector3(15,.62f,4),new Vector3(5,1.25f,1.3f),stageMat);
        Box("Counter top",new Vector3(15,1.3f,4),new Vector3(5.3f,.18f,1.55f),metalMat);
        Box("Kitchen partition",new Vector3(18,2.3f,10),new Vector3(7,4.6f,.25f),wallMat);
        Box("Kitchen sign",new Vector3(15,3.6f,9.82f),new Vector3(4,.12f,.08f),neonGoldMat,false);

        // Tables, benches and party furniture are solid; broad aisles remain open for roaming.
        for(int row=0;row<3;row++)
        for(int col=0;col<3;col++)
        {
            float x=-13+col*8, z=-7+row*6;
            Box("Party table",new Vector3(x,.82f,z),new Vector3(3.2f,.18f,1.9f),metalMat);
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)
                Box("Table leg",new Vector3(x+sx*1.25f,.39f,z+sz*.68f),new Vector3(.13f,.78f,.13f),trimMat);
            Box("Dining bench",new Vector3(x,.53f,z-1.75f),new Vector3(3.3f,.45f,.42f),stageMat);
            Box("Dining bench",new Vector3(x,.53f,z+1.75f),new Vector3(3.3f,.45f,.42f),stageMat);
        }
        // Arcade cabinets, party decorations and pizza props to make exploration read as a pizzeria.
        for(int i=0;i<4;i++)
        {
            float x=-15+i*4.5f;
            Box("Arcade cabinet",new Vector3(x,.95f,-13.9f),new Vector3(1.25f,1.9f,.72f),trimMat);
            Box("Arcade screen",new Vector3(x,1.35f,-13.48f),new Vector3(.88f,.58f,.035f),i%2==0?neonCyanMat:neonPinkMat,false);
        }
        for(int i=0;i<4;i++)
        {
            float x=-12+i*8;
            Box("Party pennant",new Vector3(x,4.25f,5.5f),new Vector3(.32f,.65f,.10f),i%2==0?neonGoldMat:neonPinkMat,false);
        }
        // Six practical light pools; emissive strips remain visible without heavy post effects.
        var lights=new List<Light>();
        for(int i=0;i<6;i++)
        {
            float x=-15+i*6;
            var fixture=Box("Ceiling LED panel",new Vector3(x,5.12f,0),new Vector3(1.2f,.10f,1.8f),i%2==0?neonCyanMat:neonGoldMat,false);
            var lightObject=new GameObject("Pizzeria practical light");lightObject.transform.position=new Vector3(x,4.85f,0);
            var light=lightObject.AddComponent<Light>();light.type=LightType.Point;light.range=10f;light.intensity=2.2f;light.color=i%2==0?new Color(.22f,.62f,1f):new Color(1f,.40f,.18f);light.shadows=LightShadows.None;lights.Add(light);
        }
        pizzeriaLights=lights.ToArray();
        AddNeonPointLight(new Vector3(-6,3.2f,11),new Color(.15f,.55f,1f),2.3f);
        AddNeonPointLight(new Vector3(6,3.2f,11),new Color(1f,.08f,.28f),2.3f);
        AddNeonPointLight(new Vector3(15,3f,2),new Color(1f,.48f,.12f),1.4f);
    }
    void AddNeonPointLight(Vector3 position,Color color,float intensity)
    {var go=new GameObject("Neon glow light");go.transform.position=position;var l=go.AddComponent<Light>();l.type=LightType.Point;l.range=8f;l.intensity=intensity;l.color=color;l.shadows=LightShadows.None;}

    void BuildPlayer()
    {
        playerRoot=new GameObject("Pizzeria Explorer").transform;playerRoot.position=new Vector3(0,0,-11f);
        playerRoot.gameObject.tag="Player";
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
    void BuildThreats(){leftThreat=MakeThreat("The Warden",new Color(.24f,.29f,.31f),new Vector3(-2.1f,.48f,12.1f));rightThreat=MakeThreat("The Brass Guest",new Color(.30f,.22f,.13f),new Vector3(2.1f,.48f,12.1f));activeHunterIndex=0;}
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
        animatronicTurnText=AnchoredLabel(hudRoot,"DE TURNO: THE WARDEN",14,TextAnchor.UpperCenter,new Vector2(.5f,1),new Vector2(0,-75),new Vector2(360,28));
        statusText=AnchoredLabel(statusCard.transform,"12:00 AM\nNOCHE 1",20,TextAnchor.MiddleLeft,new Vector2(0,1),new Vector2(12,-8),new Vector2(164,48));
        var powerCard=Panel(hudRoot,new Vector2(1,1),new Vector2(-18,-18),new Vector2(228,64),new Color(.025f,.035f,.05f,.88f));powerCard.GetComponent<Image>().raycastTarget=false;
        AnchoredLabel(powerCard.transform,"PELIGRO",13,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(12,-7),new Vector2(200,18));
        var powerBack=Panel(powerCard.transform,new Vector2(0,0),new Vector2(12,10),new Vector2(204,28),new Color(.02f,.03f,.04f,.9f));powerFill=Panel(powerBack.transform,new Vector2(0,0),new Vector2(3,3),new Vector2(198,22),new Color(.3f,.85f,.46f,.95f)).GetComponent<Image>();
        messageText=AnchoredLabel(hudRoot,"",26,TextAnchor.MiddleCenter,new Vector2(.5f,.5f),Vector2.zero,new Vector2(620,180));messageText.gameObject.SetActive(false);
        // Right-side actions use a fixed dock so they're never under the mission card.
        var actionsCard=Panel(hudRoot,new Vector2(1,0),new Vector2(-18,18),new Vector2(410,84),new Color(.025f,.035f,.05f,.86f));actionsCard.GetComponent<Image>().raycastTarget=false;
        lightButton=MakeButton(actionsCard.transform,"LINTERNA",new Vector2(0,0),new Vector2(10,12),new Vector2(188,58),ToggleLight);
        whistleButton=MakeButton(actionsCard.transform,"SILBATO",new Vector2(1,0),new Vector2(-10,12),new Vector2(188,58),UseWhistle);
        whistleText=AnchoredLabel(hudRoot,"",14,TextAnchor.MiddleCenter,new Vector2(.5f,0),new Vector2(0,20),new Vector2(220,24));
        var missionCard=Panel(hudRoot,new Vector2(1,1),new Vector2(-18,-94),new Vector2(264,118),new Color(.025f,.035f,.05f,.84f));missionCard.GetComponent<Image>().raycastTarget=false;
        missionText=AnchoredLabel(missionCard.transform,"OBJETIVOS\n□ Revisa las cámaras\n□ Usa la linterna\n□ Sobrevive hasta las 6 AM",14,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(12,-10),new Vector2(240,100));
        var realismCard=Panel(hudRoot,new Vector2(0,1),new Vector2(18,-92),new Vector2(188,62),new Color(.025f,.035f,.05f,.86f));realismCard.GetComponent<Image>().raycastTarget=false;
        realismValueText=AnchoredLabel(realismCard.transform,"REALISMO · 35%",12,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(10,-6),new Vector2(168,18));
        realismSlider=CreateRealismSlider(realismCard.transform);
        // Movement stick stays in its own lower-left zone, away from camera look and action buttons.
        var stickObj=Panel(hudRoot,new Vector2(0,0),new Vector2(22,22),new Vector2(142,142),new Color(.04f,.07f,.10f,.78f));var stickImage=stickObj.GetComponent<Image>();stickImage.sprite=GetCircularControlSprite();stickImage.type=Image.Type.Simple;stickImage.raycastTarget=true;moveStick=stickObj.AddComponent<SimpleTouchStick>();
        var knob=Panel(stickObj.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(58,58),new Color(.7f,.8f,.9f,.90f));var knobImage=knob.GetComponent<Image>();knobImage.sprite=GetCircularControlSprite();knobImage.type=Image.Type.Simple;knobImage.raycastTarget=false;moveStick.knob=knob.GetComponent<RectTransform>();
        AnchoredLabel(hudRoot,"MOVER",13,TextAnchor.LowerLeft,new Vector2(0,0),new Vector2(27,172),new Vector2(132,22));
        var grain=Panel(hudRoot,new Vector2(.5f,.5f),Vector2.zero,new Vector2(0,0),new Color(1,1,1,.025f));var grainRect=grain.GetComponent<RectTransform>();grainRect.anchorMin=Vector2.zero;grainRect.anchorMax=Vector2.one;grainRect.offsetMin=Vector2.zero;grainRect.offsetMax=Vector2.zero;var grainImage=grain.GetComponent<Image>();grainImage.sprite=GetFilmGrainSprite();grainImage.type=Image.Type.Simple;grainImage.raycastTarget=false;filmGrainOverlay=grainImage;grain.transform.SetAsLastSibling();
        hud.SetActive(false);
        startOverlay=Panel(canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(960,540),new Color(.015f,.025f,.04f,.98f));
        var full=startOverlay.GetComponent<RectTransform>();full.anchorMin=Vector2.zero;full.anchorMax=Vector2.one;full.offsetMin=Vector2.zero;full.offsetMax=Vector2.zero;
        startTitle=AnchoredLabel(startOverlay.transform,"PIZZERÍA · TURNO DE NOCHE",32,TextAnchor.MiddleCenter,new Vector2(.5f,.5f),new Vector2(0,-30),new Vector2(800,65));
        startDescription=AnchoredLabel(startOverlay.transform,"Explora la pizzería en primera persona.\nUn animatrónico te persigue mientras el otro permanece en el escenario; cambian de turno.\nJoystick izquierdo para caminar y arrastra con otro dedo para mirar.",20,TextAnchor.MiddleCenter,new Vector2(.5f,.5f),new Vector2(0,-115),new Vector2(780,110));
        startButton=MakeButton(startOverlay.transform,"ENTRAR A LA PIZZERÍA",new Vector2(.5f,.5f),new Vector2(0,95),new Vector2(270,72),StartMission);
        UpdateStartScreen();
    }
    static Sprite GetCircularControlSprite()
    {
        if(circularControlSprite!=null)return circularControlSprite;
        const int size=96;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false,true);tex.name="Round joystick touch surface";tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;
        var pixels=new Color32[size*size];float center=(size-1)*.5f,radius=size*.47f;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(center,center));byte a=(byte)Mathf.RoundToInt(Mathf.Clamp01(radius+.5f-d)*255);pixels[y*size+x]=new Color32(255,255,255,a);}
        tex.SetPixels32(pixels);tex.Apply(false,true);circularControlSprite=Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);return circularControlSprite;
    }
    static Sprite GetFilmGrainSprite()
    {
        const int size=128;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false,true);tex.name="Subtle static film grain";tex.wrapMode=TextureWrapMode.Repeat;tex.filterMode=FilterMode.Point;var rng=new System.Random(90817);var pixels=new Color32[size*size];
        for(int i=0;i<pixels.Length;i++){byte v=(byte)rng.Next(110,246);pixels[i]=new Color32(v,v,v,255);}tex.SetPixels32(pixels);tex.Apply(false,true);return Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
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
        var slider=go.GetComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.value=PlayerPrefs.GetFloat("Nightwatch.Realism",.35f);slider.fillRect=fr;slider.handleRect=kr;slider.targetGraphic=knob.GetComponent<Image>();slider.onValueChanged.AddListener(v=>{ApplyRealismLevel(v,true);if(realismValueText)realismValueText.text="REALISMO · "+Mathf.RoundToInt(v*100)+"%";});return slider;
    }
    void ApplyRealismLevel(float level,bool save)
    {
        level=Mathf.Clamp01(level);realismLevel=level;if(filmGrainOverlay){var tint=filmGrainOverlay.color;tint.a=Mathf.Lerp(.008f,.045f,level);filmGrainOverlay.color=tint;}RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=Color.Lerp(new Color(.20f,.22f,.27f),new Color(.48f,.52f,.58f),level);
        RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.12f,.13f,.16f),new Color(.28f,.30f,.34f),level);
        RenderSettings.ambientGroundColor=Color.Lerp(new Color(.045f,.05f,.06f),new Color(.12f,.12f,.13f),level);
        QualitySettings.shadows=level>.58f?ShadowQuality.HardOnly:ShadowQuality.Disable;
        QualitySettings.shadowDistance=Mathf.Lerp(8f,26f,level);
        foreach(var light in FindObjectsOfType<Light>())
        {
            // Keep the many practical/LED point lights cheap on Android; only the key light casts shadows.
            light.shadows=light.type==LightType.Directional&&level>.72f?LightShadows.Hard:LightShadows.None;
            if(light.type==LightType.Directional)light.intensity=Mathf.Lerp(.7f,1.25f,level);
            else if(light.type==LightType.Point)light.intensity=Mathf.Lerp(.6f,2.2f,level);
        }
        if(save){PlayerPrefs.SetFloat("Nightwatch.Realism",level);PlayerPrefs.Save();}
    }

    void StartMission()
    {
        if(ended){RestartMission();return;}
        activeHunterIndex=0;turnClock=0;if(leftThreat)leftThreat.position=StagePosition(0);if(rightThreat)rightThreat.position=StagePosition(1);
        gameStarted=true;
        if(startOverlay)startOverlay.SetActive(false);
        if(gameplayHud)gameplayHud.SetActive(true);
        UpdateMissions();
    }
    void RestartMission()
    {
        aiTimer=0;power=100;ended=false;lightOn=false;objectiveCamera=false;objectiveLight=false;verticalVelocity=0;yaw=0;pitch=2f;playerRoot.position=new Vector3(0,0,-11f);
        activeHunterIndex=0;turnClock=0;whistleCooldown=0;whistleStunTimer=0;if(leftThreat)leftThreat.position=StagePosition(0);if(rightThreat)rightThreat.position=StagePosition(1);
        if(ambienceSource&&ambienceClip&&!ambienceSource.isPlaying)ambienceSource.Play();
        if(flashlight)flashlight.enabled=false;
        if(messageText)messageText.gameObject.SetActive(false);
        if(startOverlay)startOverlay.SetActive(false);
        if(gameplayHud)gameplayHud.SetActive(true);
        gameStarted=true;UpdateHUD();
    }
    void UpdateStartScreen(){if(startButton)startButton.GetComponentInChildren<Text>().text=ended?"VOLVER A EXPLORAR":"ENTRAR A LA PIZZERÍA";if(startTitle)startTitle.text=ended?"TE ATRAPARON":"PIZZERÍA · TURNO DE NOCHE";if(startDescription)startDescription.text=ended?"El turno cambió y te encontró. Puedes volver a recorrer la pizzería.":"Explora libremente la pizzería.\nUn animatrónico te persigue y el otro permanece en el escenario; cambian de turno.\nJoystick circular izquierdo para moverte; arrastra con otro dedo para mirar.";}
    void ToggleLight(){if(!ended&&gameStarted){lightOn=!lightOn;objectiveLight=true;if(flashlight)flashlight.enabled=lightOn;}}
    void UseWhistle(){if(!gameStarted||ended||whistleCooldown>0)return;whistleCooldown=9f;whistleStunTimer=2.8f;objectiveCamera=true;if(sfxSource&&monsterGrowlClip)sfxSource.PlayOneShot(monsterGrowlClip,.18f);}

    void Update()
    {
        if(gameCamera==null){EnsureFallbackCamera();return;}
        if(startupFailure!=null)return;
        if(ended){if(scareTime>0){scareTime-=Time.deltaTime;gameCamera.fieldOfView=72+Mathf.Sin(Time.time*35)*4;}return;}
        if(!gameStarted)return;
        aiTimer+=Time.deltaTime;
        power=100f; // Free-roam mode has no night timer or resource lockout.
        UpdateAnimatronicTurns();
        MovePlayer();UpdateCamera();UpdateHUD();
    }
    void MovePlayer()
    {
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
        controller.Move((move * 4.2f + Vector3.up * verticalVelocity) * Time.deltaTime);
        if (playerRoot.position.y < -2f)
        {
            Debug.LogError("Player fell through collision surface; restoring player to the office.");
            playerRoot.position = new Vector3(0,0,-11f);
            verticalVelocity = 0f;
        }
        playerRoot.position=new Vector3(Mathf.Clamp(playerRoot.position.x,-20.8f,20.8f),playerRoot.position.y,Mathf.Clamp(playerRoot.position.z,-14.8f,14.7f));
    }
    public void LookDrag(Vector2 delta){if(ended)return;yaw+=delta.x*.20f;pitch=Mathf.Clamp(pitch-delta.y*.16f,-35,35);}
    void UpdateCamera()
    {
        gameCamera.transform.localPosition=new Vector3(0,1.58f,0);gameCamera.transform.localRotation=Quaternion.Euler(pitch,yaw,0);gameCamera.fieldOfView=72;
    }
    void UpdateAnimatronicTurns()
    {
        turnClock+=Time.deltaTime;whistleCooldown=Mathf.Max(0,whistleCooldown-Time.deltaTime);whistleStunTimer=Mathf.Max(0,whistleStunTimer-Time.deltaTime);
        if(turnClock>=32f){turnClock=0;activeHunterIndex=1-activeHunterIndex;Transform former=activeHunterIndex==0?rightThreat:leftThreat;Transform next=activeHunterIndex==0?leftThreat:rightThreat;if(former){former.position=StagePosition(activeHunterIndex==0?1:0);var resting=former.GetComponent<MimicActor>();if(resting)resting.PlayIdle();}if(next)next.position=StagePosition(activeHunterIndex);}
        Transform hunter=activeHunterIndex==0?leftThreat:rightThreat;
        Transform stageActor=activeHunterIndex==0?rightThreat:leftThreat;
        if(animatronicTurnText)animatronicTurnText.text="DE TURNO: "+(activeHunterIndex==0?"THE WARDEN":"THE BRASS GUEST")+" · "+Mathf.CeilToInt(32f-turnClock)+" s";
        if(whistleText)whistleText.text=whistleCooldown>0?"SILBATO · "+Mathf.CeilToInt(whistleCooldown)+" s":whistleStunTimer>0?"SE DETUVO · "+Mathf.CeilToInt(whistleStunTimer)+" s":"";
        if(stageActor)stageActor.position=StagePosition(activeHunterIndex==0?1:0);
        if(!hunter||!gameStarted)return;
        Vector3 target=playerRoot.position;target.y=hunter.position.y;
        Vector3 toPlayer=target-hunter.position;float distance=toPlayer.magnitude;
        if(distance<1.2f){TriggerLoss("EL ANIMATRÓNICO TE ALCANZÓ");return;}
        if(whistleStunTimer<=0f&&distance>1.1f)
        {
            Vector3 direction=toPlayer.normalized;
            Vector3 origin=hunter.position+Vector3.up*.8f;
            // Keep the pursuer from walking straight through tables and columns.
            if(Physics.Raycast(origin,direction,out RaycastHit hit,1.6f))
            {
                if(hit.transform!=playerRoot&&hit.transform!=hunter&&!hit.transform.IsChildOf(hunter))
                {
                    Vector3 right=Vector3.Cross(Vector3.up,direction).normalized;
                    bool rightFree=!Physics.Raycast(origin,right,1.2f);
                    direction=rightFree?right:-right;
                }
            }
            float speed=1.45f+Mathf.Clamp01(aiTimer/180f)*.65f;
            hunter.position+=direction*speed*Time.deltaTime;
            hunter.position=new Vector3(Mathf.Clamp(hunter.position.x,-20.5f,20.5f),hunter.position.y,Mathf.Clamp(hunter.position.z,-14.8f,14.5f));
            hunter.rotation=Quaternion.Slerp(hunter.rotation,Quaternion.LookRotation(direction,Vector3.up),Time.deltaTime*6f);
            var actor=hunter.GetComponent<MimicActor>();if(actor)actor.PlayRun();
        }
        if(pizzeriaLights!=null)
        {
            for(int i=0;i<pizzeriaLights.Length;i++)if(pizzeriaLights[i])pizzeriaLights[i].intensity=Mathf.Lerp(.65f,2.2f,realismLevel)+Mathf.Sin(Time.time*2.3f+i*1.7f)*.08f;
        }
    }
    Vector3 StagePosition(int index){return new Vector3(index==0?-2.1f:2.1f,.48f,12.1f);}

    void UpdateHUD()
    {
        if(statusText)statusText.text="PIZZERÍA\nEXPLORACIÓN LIBRE";
        Transform hunter=activeHunterIndex==0?leftThreat:rightThreat;
        float danger=hunter?1f-Mathf.Clamp01(Vector3.Distance(playerRoot.position,hunter.position)/18f):0f;
        if(!powerFill||!playerRoot)return;
        powerFill.rectTransform.anchorMin=new Vector2(0,0);powerFill.rectTransform.anchorMax=new Vector2(Mathf.Max(.025f,danger),1);powerFill.rectTransform.offsetMin=new Vector2(3,3);powerFill.rectTransform.offsetMax=new Vector2(-3,-3);
        powerFill.color=danger<.35f?new Color(.3f,.85f,.46f):danger<.7f?new Color(1f,.63f,.12f):new Color(.95f,.16f,.12f);
        UpdateMissions();
    }
    void UpdateMissions()
    {
        if(!missionText)return;
        missionText.text="PIZZERÍA\nExplora el salón\nEvita al que está de turno\nEl otro descansa en el escenario";
        if(animatronicTurnText)animatronicTurnText.text="DE TURNO: "+(activeHunterIndex==0?"THE WARDEN":"THE BRASS GUEST");
    }
    void TriggerLoss(string text){ended=true;gameStarted=false;messageText.gameObject.SetActive(true);messageText.text=text+"\n\nTURNO TERMINADO";scareTime=2.5f;var actor=activeHunterIndex==0?(leftThreat?leftThreat.GetComponent<MimicActor>():null):(rightThreat?rightThreat.GetComponent<MimicActor>():null);if(actor)actor.PlayJumpscare();if(ambienceSource)ambienceSource.Stop();if(sfxSource){if(zombieGroanClip)sfxSource.PlayOneShot(zombieGroanClip,.9f);else if(monsterGrowlClip)sfxSource.PlayOneShot(monsterGrowlClip,1f);}if(startOverlay){startOverlay.SetActive(true);UpdateStartScreen();}}
}

public class SimpleTouchStick : MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
{
    public RectTransform knob;
    public static Vector2 Axis{get;private set;}
    Vector2 center;
    int activePointer=int.MinValue;
    void Start(){center=GetComponent<RectTransform>().rect.center;}
    public void OnPointerDown(PointerEventData e){activePointer=e.pointerId;OnDrag(e);}
    public void OnDrag(PointerEventData e){if(e.pointerId!=activePointer)return;Vector2 local;if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,e.position,e.pressEventCamera,out local)){Vector2 d=Vector2.ClampMagnitude(local-center,53);Axis=d/53;if(knob)knob.anchoredPosition=d;}}
    public void OnPointerUp(PointerEventData e){if(e.pointerId!=activePointer)return;activePointer=int.MinValue;Axis=Vector2.zero;if(knob)knob.anchoredPosition=Vector2.zero;}
}
public class TouchLookArea:MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
{
    Vector2 last;int activePointer=int.MinValue;
    public void OnPointerDown(PointerEventData e){activePointer=e.pointerId;last=e.position;}
    public void OnDrag(PointerEventData e){if(e.pointerId!=activePointer)return;Vector2 d=e.position-last;last=e.position;if(WorldBootstrap.Instance)WorldBootstrap.Instance.LookDrag(d);}
    public void OnPointerUp(PointerEventData e){if(e.pointerId==activePointer)activePointer=int.MinValue;}
}

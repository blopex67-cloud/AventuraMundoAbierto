using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class WorldBootstrap : MonoBehaviour
{
    public static WorldBootstrap Instance;
    public Transform player;
    public Text hud;
    int crystals;
    float timeOfDay;
    readonly List<Transform> pickups = new List<Transform>();
    Material mat;

    void Awake() { Instance = this; }
    void Start()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        BuildWorld();
        BuildPlayer();
        BuildUI();
    }
    Material M(Color c, float metallic = 0f)
    {
        var m = new Material(Shader.Find("Standard")); m.color = c; m.SetFloat("_Metallic", metallic); m.SetFloat("_Glossiness", .25f); return m;
    }
    GameObject Cube(string n, Vector3 p, Vector3 s, Color c, Transform parent = null)
    { var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=n; o.transform.position=p; o.transform.localScale=s; if(parent)o.transform.SetParent(parent); o.GetComponent<Renderer>().material=M(c); return o; }
    void BuildWorld()
    {
        var groundMat=M(new Color(.24f,.48f,.25f));
        for(int x=-4;x<=4;x++) for(int z=-4;z<=4;z++)
        {
            float h=Mathf.PerlinNoise((x+10)*.16f,(z+10)*.16f)*2.2f;
            var t=GameObject.CreatePrimitive(PrimitiveType.Cube); t.name="Island terrain"; t.transform.position=new Vector3(x*12,h-1.1f,z*12); t.transform.localScale=new Vector3(12,2.2f,12); t.GetComponent<Renderer>().sharedMaterial=groundMat;
        }
        var sand=M(new Color(.77f,.68f,.43f));
        for(int i=0;i<55;i++) { float a=i*2.399f, r=40+Mathf.Sin(i*4)*6; Vector3 p=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r); var tree=GameObject.CreatePrimitive(PrimitiveType.Cylinder); tree.transform.position=p+Vector3.up*2; tree.transform.localScale=new Vector3(.55f,2,.55f); tree.GetComponent<Renderer>().material=sand; var crown=GameObject.CreatePrimitive(PrimitiveType.Sphere); crown.transform.position=p+Vector3.up*4.6f; crown.transform.localScale=Vector3.one*3.6f; crown.GetComponent<Renderer>().material=M(i%2==0?new Color(.12f,.36f,.17f):new Color(.2f,.44f,.19f)); }
        var water=GameObject.CreatePrimitive(PrimitiveType.Plane); water.name="Ocean"; water.transform.position=new Vector3(0,-2.4f,0); water.transform.localScale=new Vector3(140,1,140); water.GetComponent<Renderer>().material=M(new Color(.08f,.36f,.55f),.25f);
        for(int i=0;i<10;i++) { float a=i*2.4f; Vector3 p=new Vector3(Mathf.Cos(a)*Random.Range(9,34),2.5f,Mathf.Sin(a)*Random.Range(9,34)); var c=GameObject.CreatePrimitive(PrimitiveType.Sphere); c.name="Cristal"; c.transform.position=p; c.transform.localScale=new Vector3(.8f,1.5f,.8f); c.GetComponent<Renderer>().material=M(new Color(.1f,.9f,1f),.6f); pickups.Add(c.transform); }
        var light=new GameObject("Sun").AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.15f; light.transform.rotation=Quaternion.Euler(48,-35,0); RenderSettings.ambientLight=new Color(.58f,.62f,.67f);
        var sky=GameObject.CreatePrimitive(PrimitiveType.Sphere); sky.name="Sky dome"; sky.transform.position=Vector3.zero; sky.transform.localScale=Vector3.one*600; sky.GetComponent<Renderer>().material=M(new Color(.35f,.65f,.86f)); sky.GetComponent<Collider>().enabled=false; sky.transform.SetAsFirstSibling();
    }
    void BuildPlayer()
    {
        var p=new GameObject("Explorer"); p.transform.position=new Vector3(0,2,0); var body=GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name="Player body"; body.transform.SetParent(p.transform); body.transform.localPosition=Vector3.zero; body.GetComponent<Renderer>().material=M(new Color(.9f,.43f,.13f)); var col=body.GetComponent<CapsuleCollider>(); col.center=Vector3.zero; var rb=p.AddComponent<Rigidbody>(); rb.constraints=RigidbodyConstraints.FreezeRotation; rb.mass=1; p.AddComponent<ExplorerController>(); player=p.transform;
        var cam=new GameObject("Follow Camera").AddComponent<Camera>(); cam.tag="MainCamera"; cam.transform.position=p.transform.position+new Vector3(0,5,-8); cam.transform.LookAt(p.transform.position+Vector3.up*1.2f); cam.gameObject.AddComponent<CameraFollow>();
    }
    void BuildUI()
    {
        var canvasObj=new GameObject("Mobile HUD"); var canvas=canvasObj.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvasObj.AddComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; canvasObj.AddComponent<GraphicRaycaster>(); var ev=new GameObject("EventSystem"); ev.AddComponent<EventSystem>(); ev.AddComponent<StandaloneInputModule>();
        hud=Label(canvas.transform,"CRISTALES  0 / 10\nExplora la isla y encuentra los cristales",24,TextAnchor.UpperLeft,new Vector2(20,-20),new Vector2(560,115));
        Button(canvas.transform,"↑",new Vector2(-100,100),new Vector2(120,120),()=>{ if(player) player.GetComponent<Rigidbody>().AddForce(Vector3.up*6,ForceMode.VelocityChange); });
        Button(canvas.transform,"✦",new Vector2(-235,90),new Vector2(120,120),Collect);
        var hint=Label(canvas.transform,"MOVER: WASD / JOYSTICK\nMIRAR: RATÓN / ARRASTRAR",16,TextAnchor.LowerLeft,new Vector2(18,22),new Vector2(430,70));
        var stickRoot=new GameObject("Move Joystick"); stickRoot.transform.SetParent(canvas.transform,false); var bg=stickRoot.AddComponent<Image>(); bg.color=new Color(.04f,.08f,.12f,.42f); var sr=stickRoot.GetComponent<RectTransform>(); sr.anchorMin=new Vector2(0,0); sr.anchorMax=new Vector2(0,0); sr.pivot=new Vector2(0,0); sr.anchoredPosition=new Vector2(32,110); sr.sizeDelta=new Vector2(170,170); var stick=stickRoot.AddComponent<SimpleTouchStick>();
        var knob=new GameObject("Joystick Knob"); knob.transform.SetParent(stickRoot.transform,false); var ki=knob.AddComponent<Image>(); ki.color=new Color(.8f,.9f,1f,.55f); var kr=knob.GetComponent<RectTransform>(); kr.anchorMin=new Vector2(.5f,.5f); kr.anchorMax=new Vector2(.5f,.5f); kr.pivot=new Vector2(.5f,.5f); kr.sizeDelta=new Vector2(72,72); stick.knob=kr;
    }
    Text Label(Transform root,string text,int size,TextAnchor anchor,Vector2 pos,Vector2 dims)
    { var o=new GameObject("HUD Text"); o.transform.SetParent(root,false); var t=o.AddComponent<Text>(); t.font=Resources.GetBuiltinResource<Font>("Arial.ttf"); t.text=text; t.fontSize=size; t.color=Color.white; t.alignment=anchor; t.raycastTarget=false; var r=o.GetComponent<RectTransform>(); r.anchorMin=new Vector2(0,1); r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1); r.anchoredPosition=pos; r.sizeDelta=dims; var sh=o.AddComponent<Shadow>(); sh.effectColor=Color.black; sh.effectDistance=new Vector2(2,-2); return t; }
    void Button(Transform root,string text,Vector2 anchored,Vector2 size,UnityEngine.Events.UnityAction action)
    { var o=new GameObject("Button "+text); o.transform.SetParent(root,false); var im=o.AddComponent<Image>(); im.color=new Color(.06f,.12f,.16f,.6f); var b=o.AddComponent<Button>(); b.targetGraphic=im; b.onClick.AddListener(action); var r=o.GetComponent<RectTransform>(); r.anchorMin=new Vector2(1,0); r.anchorMax=new Vector2(1,0); r.pivot=new Vector2(1,0); r.anchoredPosition=anchored; r.sizeDelta=size; var tx=new GameObject("Label").AddComponent<Text>(); tx.transform.SetParent(o.transform,false); tx.font=Resources.GetBuiltinResource<Font>("Arial.ttf"); tx.text=text; tx.fontSize=52; tx.color=Color.white; tx.alignment=TextAnchor.MiddleCenter; tx.rectTransform.anchorMin=Vector2.zero; tx.rectTransform.anchorMax=Vector2.one; tx.rectTransform.offsetMin=Vector2.zero; tx.rectTransform.offsetMax=Vector2.zero; }
    void Collect()
    { if(!player)return; Transform nearest=null; float d=4.8f; foreach(var p in pickups) if(p){float n=Vector3.Distance(player.position,p.position);if(n<d){d=n;nearest=p;}} if(nearest){pickups.Remove(nearest);Destroy(nearest.gameObject);crystals++; if(hud)hud.text=$"CRISTALES  {crystals} / 10\n"+(crystals>=10?"¡Misión completada!": "¡Cristal encontrado!");} }
    void Update()
    { timeOfDay+=Time.deltaTime*.015f; var sun=FindObjectOfType<Light>(); if(sun && sun.type==LightType.Directional){sun.transform.rotation=Quaternion.Euler(20+Mathf.Sin(timeOfDay)*45,-35,0); sun.intensity=.65f+(Mathf.Sin(timeOfDay)+1)*.4f;} }
}

public class SimpleTouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform knob; public static Vector2 Axis { get; private set; } Vector2 center;
    void Start(){center=GetComponent<RectTransform>().sizeDelta*.5f;}
    public void OnPointerDown(PointerEventData e){OnDrag(e);}
    public void OnDrag(PointerEventData e){Vector2 local; RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,e.position,e.pressEventCamera,out local); Vector2 delta=Vector2.ClampMagnitude(local-center,62f); Axis=delta/62f; if(knob)knob.anchoredPosition=delta;}
    public void OnPointerUp(PointerEventData e){Axis=Vector2.zero;if(knob)knob.anchoredPosition=Vector2.zero;}
}
public class ExplorerController : MonoBehaviour
{
    Rigidbody rb; float yaw;
    void Start(){rb=GetComponent<Rigidbody>();}
    void Update()
    {
        float x=Input.GetAxisRaw("Horizontal"), z=Input.GetAxisRaw("Vertical"); if(SimpleTouchStick.Axis.sqrMagnitude>.01f){x=SimpleTouchStick.Axis.x;z=SimpleTouchStick.Axis.y;} Vector3 forward=Camera.main?Camera.main.transform.forward:Vector3.forward; forward.y=0; forward.Normalize(); Vector3 right=Camera.main?Camera.main.transform.right:Vector3.right; right.y=0; right.Normalize(); Vector3 move=forward*z+right*x; if(move.sqrMagnitude>.05f){rb.MovePosition(transform.position+move.normalized*5.5f*Time.deltaTime); transform.forward=Vector3.Lerp(transform.forward,move.normalized,Time.deltaTime*10f);} if(Input.GetKeyDown(KeyCode.Space)&&Mathf.Abs(rb.velocity.y)<.1f)rb.AddForce(Vector3.up*6,ForceMode.VelocityChange);
    }
}
public class CameraFollow : MonoBehaviour
{
    Transform target; float yaw; float pitch=20; Vector2 last;
    void LateUpdate()
    { if(!target){var p=GameObject.Find("Explorer");if(p)target=p.transform;else return;} if(Input.GetMouseButton(0)){yaw+=Input.GetAxis("Mouse X")*3;pitch-=Input.GetAxis("Mouse Y")*2;} if(Input.touchCount>0){var t=Input.GetTouch(0); if(t.position.x>Screen.width*.42f){if(t.phase==TouchPhase.Moved){yaw+=t.deltaPosition.x*.12f;pitch-=t.deltaPosition.y*.08f;}}} pitch=Mathf.Clamp(pitch,8,65); Quaternion rot=Quaternion.Euler(pitch,yaw,0); transform.position=target.position+rot*new Vector3(0,2,-7); transform.LookAt(target.position+Vector3.up*1.25f); }
}

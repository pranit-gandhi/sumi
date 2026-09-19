using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Sumi
{
    public enum SumiRunState { Intro,WaveOne,UpgradeOne,WaveTwo,UpgradeTwo,BossIntro,Boss,Victory,Death,Paused }
    public enum SumiEnemyKind { Retainer,Shade,Oni }

    // One owner for hit stop, menus and Golden Silence prevents a late coroutine from
    // accidentally unpausing the game.
    public static class SumiTime
    {
        static float hitUntil,goldenUntil;static bool menu;
        public static bool Golden=>Time.unscaledTime<goldenUntil;
        public static void HitStop(float seconds){hitUntil=Mathf.Max(hitUntil,Time.unscaledTime+seconds);}
        public static void GoldenSilence(float seconds){goldenUntil=Mathf.Max(goldenUntil,Time.unscaledTime+seconds);}
        public static void Menu(bool value){menu=value;Tick();}
        public static void Tick(){Time.timeScale=menu?0:Time.unscaledTime<hitUntil?0:Golden?.46f:1;}
        public static void Reset(){hitUntil=goldenUntil=0;menu=false;Time.timeScale=1;}
    }

    public sealed class SumiRunDirector:MonoBehaviour
    {
        public SumiRunState state=SumiRunState.Intro;public SumiPlayer player;
        public bool CombatActive=>state==SumiRunState.WaveOne||state==SumiRunState.WaveTwo||state==SumiRunState.Boss;
        public bool Paused=>state==SumiRunState.Paused;
        public float redPulse,waveGold;public string banner="THE PAINTED COURT";
        readonly List<SumiEnemy> enemies=new List<SumiEnemy>();readonly List<int> offered=new List<int>();readonly HashSet<int> chosen=new HashSet<int>();
        SumiRunState beforePause;SumiEnemy attacker;float stateAt,nextArrowAt,bannerUntil,nextColorUpdate;int spawned;GUIStyle title,small,card,center;Renderer[] playerRenderers;SumiGoldSurface[] goldSurfaces;MaterialPropertyBlock colorBlock;
        static readonly Vector3[] Gates={new Vector3(0,0,17.5f),new Vector3(17.5f,0,0),new Vector3(0,0,-17.5f),new Vector3(-17.5f,0,0)};
        static readonly string[] UpgradeNames={"GILDED EDGE","THIRD BELL","RED REVERSAL","BRUSH STEP","UNBROKEN LINE","QUIET MOON","FALLING PETAL","INK GUARD"};
        static readonly string[] UpgradeText={"Perfect deflection empowers the next shoulder cut.","Every third perfect deflection restores 12 health.","Critical-health deflections shatter extra posture.","Brush Flash leaves a decoy and gains a longer invulnerable beat.","A shoulder cut releases a narrow ink wave.","Golden Silence restores 18 health.","Execution slows nearby devils.","The first wound in every wave is absorbed by mastery."};

        public void Init(SumiPlayer p){player=p;stateAt=Time.unscaledTime;bannerUntil=stateAt+2.8f;SumiTime.Reset();playerRenderers=p.GetComponentsInChildren<Renderer>(true);goldSurfaces=FindObjectsByType<SumiGoldSurface>(FindObjectsSortMode.None);colorBlock=new MaterialPropertyBlock();}
        void Update()
        {
            SumiTime.Tick();redPulse=Mathf.MoveTowards(redPulse,0,Time.unscaledDeltaTime*1.8f);waveGold=Mathf.MoveTowards(waveGold,0,Time.unscaledDeltaTime*.34f);
            if(Time.unscaledTime>=nextColorUpdate){nextColorUpdate=Time.unscaledTime+.05f;ApplyColor();}
            var k=Keyboard.current;
            if((state==SumiRunState.Death||state==SumiRunState.Victory)&&k!=null&&k.rKey.wasPressedThisFrame){Restart();return;}
            if(state==SumiRunState.Intro&&Time.unscaledTime-stateAt>2.2f)BeginWave(1);
            if(state==SumiRunState.WaveOne||state==SumiRunState.WaveTwo||state==SumiRunState.Boss)
            {
                enemies.RemoveAll(e=>!e);
                if(player.combat.health<=0){EnterEnd(false);return;}
                if((state==SumiRunState.WaveTwo||state==SumiRunState.Boss)&&Time.time>=nextArrowAt){SpawnArrow();nextArrowAt=Time.time+Random.Range(7.5f,10.5f);}
                if(spawned>0&&AliveCount()==0)
                {
                    if(state==SumiRunState.WaveOne)OpenUpgrade(false);
                    else if(state==SumiRunState.WaveTwo)OpenUpgrade(true);
                    else EnterEnd(true);
                }
            }
            if((state==SumiRunState.UpgradeOne||state==SumiRunState.UpgradeTwo)&&k!=null)
            {if(k.digit1Key.wasPressedThisFrame)Choose(0);if(k.digit2Key.wasPressedThisFrame)Choose(1);if(k.digit3Key.wasPressedThisFrame)Choose(2);}
            if(state==SumiRunState.BossIntro&&Time.unscaledTime-stateAt>1.8f)BeginBoss();
        }

        void BeginWave(int wave)
        {
            state=wave==1?SumiRunState.WaveOne:SumiRunState.WaveTwo;spawned=0;attacker=null;player.controllable=true;player.combat.BeginWave();
            Banner(wave==1?"FIRST INK — THE RETAINERS":"SECOND INK — FOUR DIRECTIONS",2.1f);
            if(wave==1){Spawn(SumiEnemyKind.Retainer,0);Spawn(SumiEnemyKind.Retainer,2);}
            else {Spawn(SumiEnemyKind.Shade,1);Spawn(SumiEnemyKind.Retainer,2);Spawn(SumiEnemyKind.Retainer,3);nextArrowAt=Time.time+5.2f;}
        }
        void BeginBoss(){state=SumiRunState.Boss;spawned=0;attacker=null;player.controllable=true;player.combat.BeginWave();Spawn(SumiEnemyKind.Oni,0);nextArrowAt=Time.time+8;Banner("THE PAINTED ONI",2.3f);}
        void Spawn(SumiEnemyKind kind,int gate)
        {
            var go=new GameObject(kind==SumiEnemyKind.Oni?"Painted Oni":kind==SumiEnemyKind.Shade?"Ink Shade":"Ashen Retainer");go.transform.position=Gates[gate%4]+Vector3.up*.02f;
            var e=go.AddComponent<SumiEnemy>();e.Init(player,kind,this);enemies.Add(e);spawned++;
        }
        int AliveCount(){int n=0;foreach(var e in enemies)if(e&&!e.dead)n++;return n;}
        public bool RequestAttack(SumiEnemy enemy){if(!CombatActive||SumiTime.Golden)return false;if(attacker&&attacker!=enemy&&!attacker.dead)return false;attacker=enemy;return true;}
        public void ReleaseAttack(SumiEnemy enemy){if(attacker==enemy)attacker=null;}
        public int OrbitIndex(SumiEnemy e){int i=enemies.IndexOf(e);return i<0?0:i;}
        public void EnemyDied(SumiEnemy enemy){ReleaseAttack(enemy);}
        void SpawnArrow(){if(!CombatActive||!player.controllable)return;new GameObject("Announced ink arrow").AddComponent<SumiArrowStrike>().Init(player,this);}

        void OpenUpgrade(bool second)
        {
            state=second?SumiRunState.UpgradeTwo:SumiRunState.UpgradeOne;player.controllable=false;SumiTime.Menu(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            offered.Clear();while(offered.Count<3){int n=Random.Range(0,UpgradeNames.Length);if(!chosen.Contains(n)&&!offered.Contains(n))offered.Add(n);}Banner("CHOOSE A BRUSH VOW",99);
            player.combat.AddMastery(14);waveGold=1;
        }
        void Choose(int slot)
        {
            if(slot<0||slot>=offered.Count)return;int id=offered[slot];chosen.Add(id);player.combat.ApplyUpgrade(id);SumiTime.Menu(false);Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
            if(state==SumiRunState.UpgradeOne)BeginWave(2);else {state=SumiRunState.BossIntro;stateAt=Time.unscaledTime;player.controllable=false;Banner("A BELL BENEATH THE PAPER",1.8f);}
        }
        void EnterEnd(bool victory)
        {
            state=victory?SumiRunState.Victory:SumiRunState.Death;stateAt=Time.unscaledTime;player.controllable=false;attacker=null;SumiTime.Reset();
            foreach(var e in enemies)if(e)e.enabled=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            Banner(victory?"THE NIGHT REMEMBERS":"THE INK TAKES YOU",99);if(victory){waveGold=1;player.combat.AddMastery(100);}else redPulse=1;
        }
        public void PlayerDamaged(){redPulse=1;if(player.combat.health<=0)EnterEnd(false);}
        void ApplyColor()
        {
            if(!player||player.combat==null)return;float gold=player.combat.mastery/100f,red=Mathf.Max(redPulse,Mathf.Clamp01((45-player.combat.health)/45f));
            foreach(var r in playerRenderers){if(!r)continue;r.GetPropertyBlock(colorBlock);colorBlock.SetFloat("_Gold",gold);colorBlock.SetFloat("_Red",red);r.SetPropertyBlock(colorBlock);}
            foreach(var s in goldSurfaces)if(s)s.SetMastery(player.combat.mastery);
        }
        public void TogglePause()
        {
            if(state==SumiRunState.Death||state==SumiRunState.Victory)return;
            if(state==SumiRunState.Paused){state=beforePause;SumiTime.Menu(state==SumiRunState.UpgradeOne||state==SumiRunState.UpgradeTwo);Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            else {beforePause=state;state=SumiRunState.Paused;SumiTime.Menu(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        void Restart(){SumiTime.Reset();SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);}
        public void DebugChoose(int slot){Choose(slot);}
        public void DebugRestart(){Restart();}
        void Banner(string text,float duration){banner=text;bannerUntil=Time.unscaledTime+duration;}

        void Styles()
        {
            if(title!=null)return;title=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(Screen.height*.052f),fontStyle=FontStyle.Bold};title.normal.textColor=new Color(.06f,.055f,.045f);
            small=new GUIStyle(title){fontSize=Mathf.RoundToInt(Screen.height*.020f),fontStyle=FontStyle.Normal};card=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(Screen.height*.021f),wordWrap=true,fontStyle=FontStyle.Bold};card.normal.textColor=new Color(.06f,.05f,.04f);center=new GUIStyle(small){fontSize=Mathf.RoundToInt(Screen.height*.026f)};
        }
        void OnGUI()
        {
            Styles();float w=Screen.width,h=Screen.height;Color old=GUI.color;
            // Separate red and gold washes preserve their meaning instead of blending to orange.
            float low=player&&player.combat!=null?Mathf.Clamp01((45-player.combat.health)/45f):0;
            if(low>0||redPulse>0){GUI.color=new Color(.48f,.015f,.02f,Mathf.Max(low*.30f,redPulse*.42f));GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);}
            if(waveGold>0||SumiTime.Golden){GUI.color=new Color(.82f,.62f,.18f,(SumiTime.Golden?.20f:waveGold*.12f));GUI.DrawTexture(new Rect(0,0,w,h*.09f),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(0,h*.91f,w,h*.09f),Texture2D.whiteTexture);}
            GUI.color=old;
            if(player&&player.combat!=null)
            {
                DrawBar(new Rect(w*.035f,h*.052f,w*.25f,10),player.combat.health/100f,new Color(.13f,.12f,.105f),"LIFE");
                DrawBar(new Rect(w*.035f,h*.087f,w*.25f,7),player.combat.mastery/100f,new Color(.72f,.52f,.12f),"MASTERY");
                if(player.combat.ExecutionTarget)GUI.Label(new Rect(w*.37f,h*.69f,w*.26f,42),"E  —  DECISIVE CUT",center);
            }
            SumiEnemy focus=null;float nearest=999;foreach(var e in enemies)if(e&&!e.dead){float d=(e.transform.position-player.transform.position).sqrMagnitude;if(e.kind==SumiEnemyKind.Oni){focus=e;break;}if(d<nearest){nearest=d;focus=e;}}
            if(focus){float width=focus.kind==SumiEnemyKind.Oni?w*.42f:w*.25f;float x=(w-width)*.5f;DrawBar(new Rect(x,h*.91f,width,8),focus.health/focus.maxHealth,focus.kind==SumiEnemyKind.Oni?new Color(.35f,.03f,.035f):new Color(.12f,.115f,.10f),focus.kind==SumiEnemyKind.Oni?"PAINTED ONI":"DEVIL");DrawBar(new Rect(x,h*.94f,width,5),focus.posture/focus.maxPosture,new Color(.67f,.48f,.12f),"POSTURE");}
            if(Time.unscaledTime<bannerUntil)GUI.Label(new Rect(w*.15f,h*.13f,w*.70f,h*.10f),banner,title);
            if(state==SumiRunState.Intro){GUI.Label(new Rect(w*.2f,h*.73f,w*.6f,h*.16f),"WASD move   •   LMB three-cut chain: 袈裟 · 逆袈裟 · 回旋   •   RMB hold / perfect deflect\nSPACE Brush Flash   •   Q lock, WHEEL switch foe   •   E execute",small);}
            if(state==SumiRunState.UpgradeOne||state==SumiRunState.UpgradeTwo)
            {
                GUI.color=new Color(.91f,.89f,.82f,.96f);GUI.DrawTexture(new Rect(w*.08f,h*.28f,w*.84f,h*.48f),Texture2D.whiteTexture);GUI.color=old;
                for(int i=0;i<3;i++){Rect r=new Rect(w*(.11f+i*.27f),h*.36f,w*.24f,h*.30f);if(GUI.Button(r,(i+1)+"\n\n"+UpgradeNames[offered[i]]+"\n\n"+UpgradeText[offered[i]],card))Choose(i);}
            }
            if(state==SumiRunState.Death||state==SumiRunState.Victory){GUI.Label(new Rect(w*.2f,h*.58f,w*.6f,50),state==SumiRunState.Victory?"THE COURT IS QUIET":"YOUR GOLD RETURNS TO PAPER",center);if(GUI.Button(new Rect(w*.39f,h*.69f,w*.22f,48),"R  —  PAINT AGAIN",card))Restart();}
            if(state==SumiRunState.Paused){GUI.color=new Color(.88f,.87f,.82f,.93f);GUI.DrawTexture(new Rect(w*.31f,h*.27f,w*.38f,h*.40f),Texture2D.whiteTexture);GUI.color=old;GUI.Label(new Rect(w*.32f,h*.31f,w*.36f,60),"STILLNESS",title);if(GUI.Button(new Rect(w*.40f,h*.46f,w*.20f,45),"RESUME",card))TogglePause();if(GUI.Button(new Rect(w*.40f,h*.55f,w*.20f,45),"RESTART RUN",card))Restart();}
        }
        void DrawBar(Rect r,float value,Color fill,string label){GUI.color=new Color(.8f,.79f,.73f,.8f);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=fill;GUI.DrawTexture(new Rect(r.x,r.y,r.width*Mathf.Clamp01(value),r.height),Texture2D.whiteTexture);GUI.color=Color.white;GUI.Label(new Rect(r.x,r.y-20,r.width,18),label,small);}
    }

    public sealed class SumiArrowStrike:MonoBehaviour
    {
        SumiPlayer player;SumiRunDirector run;Vector3 target;float born;LineRenderer ring,shaft;bool fired;
        public void Init(SumiPlayer p,SumiRunDirector r)
        {
            player=p;run=r;target=p.transform.position+p.velocity*.32f;target.y=.025f;born=Time.unscaledTime;
            ring=gameObject.AddComponent<LineRenderer>();ring.positionCount=33;ring.loop=true;ring.useWorldSpace=true;ring.startWidth=.025f;ring.endWidth=.006f;ring.sharedMaterial=SumiArt.Crimson;
            for(int i=0;i<33;i++){float a=i*Mathf.PI*2/32;ring.SetPosition(i,target+new Vector3(Mathf.Sin(a)*.72f,0,Mathf.Cos(a)*.72f));}
            var s=new GameObject("Falling arrow brush");s.transform.SetParent(transform);shaft=s.AddComponent<LineRenderer>();shaft.positionCount=2;shaft.startWidth=.07f;shaft.endWidth=.012f;shaft.sharedMaterial=SumiArt.Black;shaft.SetPosition(0,target+Vector3.up*9);shaft.SetPosition(1,target+Vector3.up*8);shaft.enabled=false;
        }
        void Update()
        {
            if(!run||!run.CombatActive){Destroy(gameObject);return;}float age=Time.unscaledTime-born;
            if(age>.82f&&!fired){fired=true;shaft.enabled=true;ring.startWidth=.07f;Vector3 d=player.transform.position-target;d.y=0;if(d.magnitude<.85f)player.combat.ReceiveWorldHit(11,target);SumiCombatFeedback.Hit(.035f,.13f,target);}
            if(fired){float y=Mathf.Lerp(9,0,Mathf.Clamp01((age-.82f)/.13f));shaft.SetPosition(0,target+Vector3.up*(y+1.2f));shaft.SetPosition(1,target+Vector3.up*y);}
            if(age>1.25f)Destroy(gameObject);
        }
    }
}

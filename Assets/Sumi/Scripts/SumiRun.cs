using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Sumi
{
    public enum SumiRunState { Intro,Wave,Upgrade,BossIntro,Boss,Victory,Death,Paused }
    public enum SumiEnemyKind { Retainer,Shade,Oni }

    // One owner for hit stop and menus prevents a late effect from unpausing the game.
    public static class SumiTime
    {
        static float hitUntil;static bool menu;
        public static bool MenuPaused=>menu;
        public static void HitStop(float seconds){hitUntil=Mathf.Max(hitUntil,Time.unscaledTime+seconds);}
        public static void Menu(bool value){menu=value;Tick();}
        public static void Tick(){Time.timeScale=menu||Time.unscaledTime<hitUntil?0:1;}
        public static void Reset(){hitUntil=0;menu=false;Time.timeScale=1;}
    }

    public sealed class SumiRunDirector:MonoBehaviour
    {
        public SumiRunState state=SumiRunState.Intro;public SumiPlayer player;
        public bool CombatActive=>state==SumiRunState.Wave||state==SumiRunState.Boss;
        public bool Paused=>state==SumiRunState.Paused;
        public float redPulse;public string banner="THE PAINTED COURT";
        readonly List<SumiEnemy> enemies=new List<SumiEnemy>();readonly List<int> offered=new List<int>();readonly HashSet<int> chosen=new HashSet<int>();
        SumiRunState beforePause;SumiEnemy attacker;float stateAt,nextArrowAt,bannerUntil,nextColorUpdate,shownHealth=1,trailHealth=1,lastHealth=1,trailDelay,healthVelocity,trailVelocity;int spawned,waveIndex;GUIStyle title,small,hud,card,center,wheelName,wheelNote;Renderer[] playerRenderers;MaterialPropertyBlock colorBlock;Texture2D pipTexture,barTexture;Texture2D[] wheelSegments;
        static readonly Vector3[] Gates={new Vector3(0,0,17.5f),new Vector3(17.5f,0,0),new Vector3(0,0,-17.5f),new Vector3(-17.5f,0,0)};

        // The approach to the Oni is a ladder: each wave puts one more body on the court than the
        // last, drawn from the same two kinds the run already uses, so the pressure grows without
        // asking the player to read anything new. The Oni stays the only thing the boss introduces.
        // upgrade marks the waves answered with a spell; the pool holds six, and FillOffers needs
        // three unchosen to fill a wheel, so at most four of these may be true.
        struct Wave { public int retainers,shades;public bool arrows,upgrade;public string banner; }
        static readonly Wave[] Waves=
        {
            new Wave{retainers=2,shades=0,arrows=false,upgrade=false,banner="FIRST INK — THE RETAINERS"},
            new Wave{retainers=3,shades=0,arrows=false,upgrade=true, banner="SECOND INK — THE GATHERING"},
            new Wave{retainers=3,shades=1,arrows=true, upgrade=false,banner="THIRD INK — FOUR DIRECTIONS"},
            new Wave{retainers=4,shades=1,arrows=true, upgrade=true, banner="FOURTH INK — THE CLOSING FAN"},
            new Wave{retainers=4,shades=2,arrows=true, upgrade=false,banner="FIFTH INK — THE DEEP COURT"},
            new Wave{retainers=5,shades=2,arrows=true, upgrade=true, banner="SIXTH INK — THE BLACK RING"},
            new Wave{retainers=5,shades=3,arrows=true, upgrade=false,banner="SEVENTH INK — THE CROWDED GATE"},
            new Wave{retainers=6,shades=3,arrows=true, upgrade=true, banner="EIGHTH INK — BEFORE THE BELL"},
        };
        static readonly string[] UpgradeNames={"RED THREAD","SECOND BREATH","STEADY HEART","SPLIT INK","DEEP INK","QUICK INK"};
        static readonly string[] UpgradeText={"+25 max health","Executions heal 10","Take 20% less damage","Throw two darts","Darts hit harder","Throw more often"};

        public void Init(SumiPlayer p){player=p;stateAt=Time.unscaledTime;bannerUntil=stateAt+2.8f;SumiTime.Reset();playerRenderers=p.GetComponentsInChildren<Renderer>(true);colorBlock=new MaterialPropertyBlock();}
        void Update()
        {
            SumiTime.Tick();redPulse=Mathf.MoveTowards(redPulse,0,Time.unscaledDeltaTime*1.8f);
            if(player&&player.combat!=null){float target=Mathf.Clamp01(player.combat.health/player.combat.maxHealth);float dt=Time.unscaledDeltaTime;if(target<lastHealth-.001f)trailDelay=Time.unscaledTime+.27f;lastHealth=target;if(target<shownHealth)shownHealth=Mathf.SmoothDamp(shownHealth,target,ref healthVelocity,.17f,Mathf.Infinity,dt);else shownHealth=Mathf.MoveTowards(shownHealth,target,dt*.75f);if(trailHealth<target)trailHealth=Mathf.MoveTowards(trailHealth,target,dt*.75f);else if(Time.unscaledTime>trailDelay)trailHealth=Mathf.SmoothDamp(trailHealth,target,ref trailVelocity,.56f,Mathf.Infinity,dt);}
            if(Time.unscaledTime>=nextColorUpdate){nextColorUpdate=Time.unscaledTime+.05f;ApplyColor();}
            var k=Keyboard.current;
            if((state==SumiRunState.Death||state==SumiRunState.Victory)&&k!=null&&k.rKey.wasPressedThisFrame){Restart();return;}
            if(state==SumiRunState.Intro&&Time.unscaledTime-stateAt>2.2f)BeginWave(1);
            if(state==SumiRunState.Wave||state==SumiRunState.Boss)
            {
                enemies.RemoveAll(e=>!e);
                if(player.combat.health<=0){EnterEnd(false);return;}
                // Waves that hold no arrows park nextArrowAt out of reach rather than test the state.
                if(Time.time>=nextArrowAt){SpawnArrow();nextArrowAt=Time.time+Random.Range(7.5f,10.5f);}
                if(spawned>0&&AliveCount()==0)
                {
                    if(state==SumiRunState.Wave)ClearWave();
                    else EnterEnd(true);
                }
            }
            if(state==SumiRunState.BossIntro&&Time.unscaledTime-stateAt>1.8f)BeginBoss();
        }

        void BeginWave(int wave)
        {
            waveIndex=Mathf.Clamp(wave,1,Waves.Length);var plan=Waves[waveIndex-1];
            state=SumiRunState.Wave;spawned=0;attacker=null;player.controllable=true;
            Banner(plan.banner,2.1f);
            // Shades lead so they take the spread-out gates; the retainers fill in behind them.
            int total=plan.shades+plan.retainers,slot=0;
            for(int i=0;i<plan.shades;i++)Spawn(SumiEnemyKind.Shade,slot++,total);
            for(int i=0;i<plan.retainers;i++)Spawn(SumiEnemyKind.Retainer,slot++,total);
            nextArrowAt=plan.arrows?Time.time+5.2f:float.MaxValue;
        }
        // Clearing a wave either pays out a spell or rolls straight into the next one.
        void ClearWave(){if(Waves[waveIndex-1].upgrade)OpenUpgrade();else Advance();}
        void Advance()
        {
            if(waveIndex<Waves.Length){BeginWave(waveIndex+1);return;}
            state=SumiRunState.BossIntro;stateAt=Time.unscaledTime;player.controllable=false;Banner("A BELL BENEATH THE PAPER",1.8f);
        }
        void BeginBoss(){state=SumiRunState.Boss;spawned=0;attacker=null;player.controllable=true;Spawn(SumiEnemyKind.Oni,0,1);nextArrowAt=Time.time+8;Banner("THE PAINTED ONI",2.3f);}
        void Spawn(SumiEnemyKind kind,int slot,int total)
        {
            var go=new GameObject(kind==SumiEnemyKind.Oni?"Painted Oni":kind==SumiEnemyKind.Shade?"Ink Shade":"Ashen Retainer");go.transform.position=SpawnPoint(slot,total);
            var e=go.AddComponent<SumiEnemy>();e.Init(player,kind,this);enemies.Add(e);spawned++;
        }
        // Up to four arrivals spread evenly over the gates; past that a second rank forms beside and
        // just inside the first, so two bodies can never materialise inside one another.
        Vector3 SpawnPoint(int slot,int total)
        {
            int rank=slot/Gates.Length;
            Vector3 anchor=Gates[(total<=Gates.Length?Mathf.RoundToInt(slot*(float)Gates.Length/total):slot)%Gates.Length];
            Vector3 outward=anchor.normalized,tangent=Vector3.Cross(Vector3.up,outward);
            return anchor+tangent*(rank*1.7f)-outward*(rank*.9f)+Vector3.up*.02f;
        }
        int AliveCount(){int n=0;foreach(var e in enemies)if(e&&!e.dead)n++;return n;}
        public bool RequestAttack(SumiEnemy enemy){if(!CombatActive)return false;if(attacker&&attacker!=enemy&&!attacker.dead)return false;attacker=enemy;return true;}
        public void ReleaseAttack(SumiEnemy enemy){if(attacker==enemy)attacker=null;}
        public int OrbitIndex(SumiEnemy e){int i=enemies.IndexOf(e);return i<0?0:i;}
        public void EnemyDied(SumiEnemy enemy){ReleaseAttack(enemy);}
        void SpawnArrow(){if(!CombatActive||!player.controllable)return;new GameObject("Announced ink arrow").AddComponent<SumiArrowStrike>().Init(player,this);}

        void OpenUpgrade()
        {
            offered.Clear();FillOffers();
            // Nothing left to offer is not a menu; roll on rather than show an empty wheel.
            if(offered.Count==0){Advance();return;}
            state=SumiRunState.Upgrade;player.controllable=false;SumiTime.Menu(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            Banner("CHOOSE A SPELL",99);
            player.combat.health=Mathf.Min(player.combat.maxHealth,player.combat.health+12);
        }
        void Choose(int slot)
        {
            FillOffers();if(slot<0||slot>=offered.Count)return;int id=offered[slot];chosen.Add(id);player.combat.ApplyUpgrade(id);SumiTime.Menu(false);Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
            Advance();
        }
        void EnterEnd(bool victory)
        {
            state=victory?SumiRunState.Victory:SumiRunState.Death;stateAt=Time.unscaledTime;player.controllable=false;attacker=null;SumiTime.Reset();
            foreach(var e in enemies)if(e)e.enabled=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            Banner(victory?"THE NIGHT REMEMBERS":"THE INK TAKES YOU",99);if(!victory)redPulse=1;
        }
        public void PlayerDamaged(){redPulse=1;if(player.combat.health<=0)EnterEnd(false);}
        void ApplyColor()
        {
            if(!player||player.combat==null)return;float red=Mathf.Max(redPulse,Mathf.Clamp01((45-player.combat.health)/45f));
            if(playerRenderers==null)playerRenderers=player.GetComponentsInChildren<Renderer>(true);
            if(colorBlock==null)colorBlock=new MaterialPropertyBlock();
            foreach(var r in playerRenderers){if(!r)continue;r.GetPropertyBlock(colorBlock);colorBlock.SetFloat("_Gold",0);colorBlock.SetFloat("_Red",red);r.SetPropertyBlock(colorBlock);}
        }
        // Walks the pool once from a random start instead of drawing until a miss. The old loop
        // spun forever the moment fewer than three upgrades were left unchosen, which more waves
        // now make reachable.
        void FillOffers()
        {
            if(offered.Count>=3)return;
            int start=Random.Range(0,UpgradeNames.Length);
            for(int i=0;i<UpgradeNames.Length&&offered.Count<3;i++)
            {
                int id=(start+i)%UpgradeNames.Length;
                if(!chosen.Contains(id)&&!offered.Contains(id))offered.Add(id);
            }
        }
        public void TogglePause()
        {
            if(state==SumiRunState.Death||state==SumiRunState.Victory)return;
            if(state==SumiRunState.Paused){state=beforePause;bool choosing=state==SumiRunState.Upgrade;SumiTime.Menu(choosing);Cursor.lockState=choosing?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=choosing;}
            else {beforePause=state;state=SumiRunState.Paused;SumiTime.Menu(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        void Restart(){SumiTime.Reset();SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);}
        public void DebugChoose(int slot){Choose(slot);}
        public void DebugRestart(){Restart();}
        void Banner(string text,float duration){banner=text;bannerUntil=Time.unscaledTime+duration;}

        void Styles()
        {
            if(title!=null)return;
            Font font=Resources.Load<Font>("Fonts/JiayouAkira-MAVEY");
            title=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,font=font,fontSize=Mathf.RoundToInt(Screen.height*.052f)};title.normal.textColor=new Color(.07f,.055f,.05f);
            small=new GUIStyle(title){fontSize=Mathf.RoundToInt(Screen.height*.020f)};
            hud=new GUIStyle(small);hud.normal.textColor=new Color(.08f,.07f,.065f);
            card=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleCenter,font=font,fontSize=Mathf.RoundToInt(Screen.height*.025f)};card.normal.textColor=new Color(.07f,.05f,.04f);
            center=new GUIStyle(small){fontSize=Mathf.RoundToInt(Screen.height*.028f)};
            wheelName=new GUIStyle(title){fontSize=Mathf.RoundToInt(Screen.height*.027f)};wheelName.normal.textColor=Color.white;
            wheelNote=new GUIStyle(small){fontSize=Mathf.RoundToInt(Screen.height*.017f)};wheelNote.normal.textColor=Color.white;
            BuildHudTextures();
        }
        void OnGUI()
        {
            Styles();float w=Screen.width,h=Screen.height;Color old=GUI.color;
            float low=player&&player.combat!=null?Mathf.Clamp01((45-player.combat.health)/45f):0;
            if(low>0||redPulse>0){GUI.color=new Color(.48f,.015f,.02f,Mathf.Max(low*.30f,redPulse*.42f));GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);}
            GUI.color=old;
            if(player&&player.combat!=null)
            {
                if(state!=SumiRunState.Upgrade){DrawCombo(w,h);DrawHealth(w,h);}
                if(player.combat.ExecutionTarget)GUI.Label(new Rect(w*.37f,h*.69f,w*.26f,42),"E  —  DECISIVE CUT",center);
            }
            if(state!=SumiRunState.Upgrade&&player&&player.locked&&player.target){var lockedEnemy=player.target.GetComponent<SumiEnemy>();if(lockedEnemy&&!lockedEnemy.dead)DrawEnemyHealth(w,h,lockedEnemy);}
            if(Time.unscaledTime<bannerUntil&&state!=SumiRunState.Upgrade)GUI.Label(new Rect(w*.15f,h*.13f,w*.70f,h*.10f),banner,title);
            if(state==SumiRunState.Intro)GUI.Label(new Rect(w*.2f,h*.89f,w*.6f,25),"F  THROW INK",small);
            if(state==SumiRunState.Intro){GUI.Label(new Rect(w*.2f,h*.73f,w*.6f,h*.16f),"WASD move   •   LMB shoulder cut   •   RMB hold / perfect deflect\nSPACE Brush Flash   •   Q lock   •   E execute",small);}
            if(state==SumiRunState.Upgrade)
            {
                DrawSpellWheel(w,h);
            }
            if(state==SumiRunState.Death||state==SumiRunState.Victory){GUI.Label(new Rect(w*.2f,h*.58f,w*.6f,50),state==SumiRunState.Victory?"THE COURT IS QUIET":"YOUR GOLD RETURNS TO PAPER",center);if(GUI.Button(new Rect(w*.39f,h*.69f,w*.22f,48),"R  —  PAINT AGAIN",card))Restart();}
            if(state==SumiRunState.Paused){GUI.color=new Color(.88f,.87f,.82f,.93f);GUI.DrawTexture(new Rect(w*.31f,h*.27f,w*.38f,h*.40f),Texture2D.whiteTexture);GUI.color=old;GUI.Label(new Rect(w*.32f,h*.31f,w*.36f,60),"STILLNESS",title);if(GUI.Button(new Rect(w*.40f,h*.46f,w*.20f,45),"RESUME",card))TogglePause();if(GUI.Button(new Rect(w*.40f,h*.55f,w*.20f,45),"RESTART RUN",card))Restart();}
        }
        void DrawCombo(float w,float h)
        {
            float x=w*.035f,y=h*.077f,size=Mathf.Clamp(h*.028f,20,30);int step=player.combat.ComboStep;
            GUI.Label(new Rect(x,y-30,170,26),"COMBO",hud);
            for(int i=0;i<3;i++)
            {
                float px=x+i*(size+8);GUI.color=new Color(.02f,.018f,.02f,.90f);GUI.DrawTexture(new Rect(px-2,y-2,size+4,size+4),pipTexture);
                GUI.color=i<step?(i==2?new Color(.68f,.11f,.13f):new Color(.96f,.90f,.78f)):new Color(.25f,.23f,.23f);
                GUI.DrawTexture(new Rect(px,y,size,size),pipTexture);
            }
            GUI.color=Color.white;GUI.Label(new Rect(x,y+size+7,200,24),player.combat.ThrowCooldownRemaining>0?"F  INK  "+player.combat.ThrowCooldownRemaining.ToString("0.0"):"F  INK",hud);
        }
        void DrawHealth(float w,float h)
        {
            float x=w*.035f,width=Mathf.Clamp(w*.29f,220,400),y=h*.94f;
            GUI.Label(new Rect(x,y-32,width,28),"HEALTH",hud);
            Rect bar=new Rect(x,y,width,9);
            GUI.color=new Color(.015f,.014f,.018f,.9f);GUI.DrawTexture(new Rect(bar.x-2,bar.y-2,bar.width+4,bar.height+4),Texture2D.whiteTexture);
            GUI.color=new Color(.23f,.05f,.06f,1);GUI.DrawTexture(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(trailHealth),bar.height),Texture2D.whiteTexture);
            GUI.color=Color.white;GUI.DrawTexture(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(shownHealth),bar.height),barTexture);
            GUI.color=Color.white;
        }
        void DrawEnemyHealth(float w,float h,SumiEnemy enemy)
        {
            float width=Mathf.Clamp(w*.23f,210,360),x=w-width-w*.035f,y=h*.078f;
            string name=enemy.kind==SumiEnemyKind.Oni?"PAINTED ONI":enemy.kind==SumiEnemyKind.Shade?"INK SHADE":"DEVIL";
            GUI.Label(new Rect(x,y-38,width,34),name,hud);
            GUI.color=new Color(.025f,.022f,.025f,.90f);GUI.DrawTexture(new Rect(x-2,y-2,width+4,10),Texture2D.whiteTexture);
            GUI.color=new Color(.68f,.10f,.13f);GUI.DrawTexture(new Rect(x,y,width*Mathf.Clamp01(enemy.health/enemy.maxHealth),6),Texture2D.whiteTexture);GUI.color=Color.white;
        }
        void DrawSpellWheel(float w,float h)
        {
            FillOffers();
            Color old=GUI.color;GUI.color=new Color(.015f,.013f,.014f,.52f);GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);GUI.color=old;
            float size=Mathf.Min(w*.44f,h*.68f),cx=w*.5f,cy=h*.52f;Rect wheel=new Rect(cx-size*.5f,cy-size*.5f,size,size);
            Vector2 pointer=Event.current.mousePosition-new Vector2(cx,cy);float radius=pointer.magnitude;
            // A late wheel can run short of unchosen spells, so every sector is gated on an offer.
            int filled=Mathf.Min(3,offered.Count);
            int hover=-1;if(radius>size*.10f&&radius<size*.48f){float a=(Mathf.Atan2(pointer.y,pointer.x)*Mathf.Rad2Deg+150+360)%360;hover=Mathf.FloorToInt(a/120);if(hover>=filled)hover=-1;}
            for(int i=0;i<3;i++){GUI.color=i==hover?new Color(.68f,.07f,.10f):new Color(.045f,.040f,.044f);GUI.DrawTexture(wheel,wheelSegments[i]);}
            GUI.color=new Color(.025f,.023f,.026f,.98f);GUI.DrawTexture(new Rect(cx-size*.11f,cy-size*.11f,size*.22f,size*.22f),pipTexture);GUI.color=old;
            for(int i=0;i<filled;i++)
            {
                float angle=(-90+i*120)*Mathf.Deg2Rad;Vector2 point=new Vector2(cx+Mathf.Cos(angle)*size*.31f,cy+Mathf.Sin(angle)*size*.31f);
                GUI.Label(new Rect(point.x-size*.23f,point.y-36,size*.46f,48),UpgradeNames[offered[i]],wheelName);
                GUI.Label(new Rect(point.x-size*.23f,point.y+10,size*.46f,31),UpgradeText[offered[i]],wheelNote);
            }
            GUI.color=old;
            if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&hover>=0){Choose(hover);Event.current.Use();}
        }
        void BuildHudTextures()
        {
            pipTexture=new Texture2D(48,48,TextureFormat.RGBA32,false);pipTexture.filterMode=FilterMode.Bilinear;
            for(int y=0;y<48;y++)for(int x=0;x<48;x++){float r=Vector2.Distance(new Vector2(x,y),new Vector2(23.5f,23.5f));float alpha=Mathf.Clamp01((23-r)*.75f);pipTexture.SetPixel(x,y,new Color(1,1,1,alpha));}pipTexture.Apply();
            barTexture=new Texture2D(256,1,TextureFormat.RGBA32,false);barTexture.filterMode=FilterMode.Bilinear;
            for(int x=0;x<256;x++){float t=x/255f;barTexture.SetPixel(x,0,Color.Lerp(new Color(.76f,.13f,.18f),new Color(.055f,.015f,.025f),Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.65f)/.35f))));}barTexture.Apply();
            wheelSegments=new Texture2D[3];for(int i=0;i<3;i++){wheelSegments[i]=new Texture2D(384,384,TextureFormat.RGBA32,false);wheelSegments[i].filterMode=FilterMode.Bilinear;}
            for(int y=0;y<384;y++)for(int x=0;x<384;x++)
            {
                float dx=x-191.5f,dy=191.5f-y,r=Mathf.Sqrt(dx*dx+dy*dy)/384f;
                float a=(Mathf.Atan2(dy,dx)*Mathf.Rad2Deg+150+360)%360;
                int sector=Mathf.FloorToInt(a/120);float local=a%120,edge=Mathf.Min(local,120-local);
                float alpha=Mathf.Clamp01((r-.105f)*180)*Mathf.Clamp01((.49f-r)*180)*Mathf.Clamp01((edge-1.3f)*.6f);
                for(int i=0;i<3;i++)wheelSegments[i].SetPixel(x,y,new Color(1,1,1,i==sector?alpha:0));
            }
            foreach(var segment in wheelSegments)segment.Apply();
        }
        void OnDestroy(){if(pipTexture)Destroy(pipTexture);if(barTexture)Destroy(barTexture);if(wheelSegments!=null)foreach(var segment in wheelSegments)if(segment)Destroy(segment);}
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

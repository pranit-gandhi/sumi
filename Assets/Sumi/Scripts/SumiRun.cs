using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Sumi
{
    public enum SumiRunState { Title,Intro,Wave,Upgrade,BossIntro,Boss,Victory,Death,Paused }
    public enum SumiEnemyKind { Retainer,Shade,Oni }

    // One owner for hit stop, death slow-motion and menus prevents a late effect from unpausing the game.
    public static class SumiTime
    {
        static float hitUntil,slowStart,slowUntil,slowHold,slowFade,slowScale=1;static bool menu;
        public static bool MenuPaused=>menu;
        public static void HitStop(float seconds){hitUntil=Mathf.Max(hitUntil,Time.unscaledTime+seconds);Tick();}
        public static void SlowMotion(float scale,float hold,float fade)
        {
            slowScale=Mathf.Clamp(scale,.05f,1f);slowHold=Mathf.Max(0,hold);slowFade=Mathf.Max(.01f,fade);
            slowStart=Mathf.Max(Time.unscaledTime,hitUntil);slowUntil=slowStart+slowHold+slowFade;Tick();
        }
        public static void Menu(bool value){menu=value;Tick();}
        public static void Tick()
        {
            float now=Time.unscaledTime;
            if(menu||now<hitUntil){Time.timeScale=0;return;}
            if(now<slowUntil)
            {
                float t=now-slowStart;
                Time.timeScale=t<=slowHold?slowScale:Mathf.Lerp(slowScale,1,Mathf.Clamp01((t-slowHold)/slowFade));
                return;
            }
            Time.timeScale=1;
        }
        public static void Reset(){hitUntil=0;menu=false;slowUntil=0;slowStart=0;slowScale=1;Time.timeScale=1;}
    }

    public sealed class SumiRunDirector:MonoBehaviour
    {
        public SumiRunState state=SumiRunState.Title;public SumiPlayer player;
        public bool CombatActive=>state==SumiRunState.Wave||state==SumiRunState.Boss;
        public bool Paused=>state==SumiRunState.Paused;
        public float redPulse;public string banner="THE PAINTED COURT";
        readonly List<SumiEnemy> enemies=new List<SumiEnemy>();readonly List<int> offered=new List<int>();readonly HashSet<int> chosen=new HashSet<int>();
        readonly HashSet<string> shownHints=new HashSet<string>();
        float nextThreatAt,hintUntil;int lastAttackerId;string combatHint;
        SumiRunState beforePause;SumiEnemy attacker;float stateAt,nextArrowAt,bannerUntil,nextColorUpdate,shownHealth=1,trailHealth=1,lastHealth=1,trailDelay,healthVelocity,trailVelocity;int spawned,waveIndex;GUIStyle title,small,hud,card,center,wheelName,wheelNameLit,wheelNote,wave,pauseNote,menuItem,menuItemLit,brand,controlsTitle,controlsBody,aboutBody,cues;Renderer[] playerRenderers;MaterialPropertyBlock colorBlock;Texture2D pipTexture,barTexture,sealTexture,paperTexture,vignetteTexture,ensoTexture;
        bool controlsOpen,aboutOpen,combosOpen,combosRevealed,overlayFromTitle;static bool skipTitle;
        float upgradeArmedAt;int upgradePress=-1;
        static readonly Vector3[] Gates={new Vector3(0,0,17.5f),new Vector3(17.5f,0,0),new Vector3(0,0,-17.5f),new Vector3(-17.5f,0,0)};
        static readonly string ControlsBody="WASD move   •   LMB chain cuts   •   R heavy   •   Q / RMB parry\n\nSPACE Dash   •   TAB lock   •   E execute   •   F shuriken\n\nESC pause";
        static readonly string AboutBody="Sumi is a ronin who fell in battle in his own world.\n\nDeath has given him one final chance.\n\nCaught between life and death, he must fight his way through the ink.\n\nCan he return?";
        static readonly string CombosBody="LMB + LMB + LMB     three-cut chain\n\nLMB + R             heavy follow-up\n\nR                   heavy cut\n\nSPACE + LMB         dash into a cut\n\nSPACE + R           dash into heavy";
        static readonly string CombosGate="A true ronin would spend time\nfiguring out the combos himself.\n\nContinue?";
        static readonly string ActionCues="LMB  cuts\nR  heavy\nQ  parry\nF  shuriken\nSPACE  dash\nTAB  lock";

        // Each wave adds pressure using enemies the run has already taught, while the Oni remains
        // the only new read at the end. At most four waves can open upgrades because the pool has
        // six choices and the late wheel can run short of three unchosen spells.
        struct Wave { public int retainers,shades;public bool arrows,upgrade;public string banner; }
        static readonly Wave[] Waves=
        {
            new Wave{retainers=2,shades=0,arrows=false,upgrade=false,banner="ACT I"},
            new Wave{retainers=3,shades=0,arrows=false,upgrade=true, banner="ACT II"},
            new Wave{retainers=3,shades=1,arrows=true, upgrade=false,banner="ACT III"},
            new Wave{retainers=4,shades=1,arrows=true, upgrade=true, banner="ACT IV"},
            new Wave{retainers=4,shades=2,arrows=true, upgrade=false,banner="ACT V"},
            new Wave{retainers=5,shades=2,arrows=true, upgrade=true, banner="ACT VI"},
            new Wave{retainers=5,shades=3,arrows=true, upgrade=false,banner="ACT VII"},
            new Wave{retainers=6,shades=3,arrows=true, upgrade=true, banner="ACT VIII"},
        };
        static readonly string[] UpgradeNames={"RED THREAD","SECOND BREATH","STEADY HEART","TWIN STARS","DEEP CUT","QUICK DRAW"};
        static readonly string[] UpgradeText={"+25 max health","Executions heal 10","Take 20% less damage","Throw two shuriken","Shuriken hit harder","Throw more often"};
        // Letterspaced by hand: IMGUI has no tracking, and emptiness does the rest.
        const string PauseLine="T H E   C O U R T   H O L D S   I T S   B R E A T H";
        const string ChoosePrompt="C H O O S E   O N E   S T R O K E";
        const string DeathLine="T H E   I N K   T A K E S   Y O U";
        const string ControlsLine="C O N T R O L S";
        const string AboutLine="A B O U T";
        const string CombosLine="C O M B O S";

        public void Init(SumiPlayer p)
        {
            player=p;stateAt=Time.unscaledTime;playerRenderers=p.GetComponentsInChildren<Renderer>(true);colorBlock=new MaterialPropertyBlock();
            controlsOpen=false;aboutOpen=false;combosOpen=false;overlayFromTitle=false;upgradePress=-1;
            bool rise=skipTitle;skipTitle=false;
            if(rise)EnterIntro();
            else EnterTitle();
        }
        void EnterTitle()
        {
            state=SumiRunState.Title;stateAt=Time.unscaledTime;bannerUntil=0;SumiTime.Menu(true);
            controlsOpen=false;aboutOpen=false;combosOpen=false;
            if(player){player.controllable=false;SetPlayerVisible(false);}
            if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.HoldTitle(true);
            if(SumiGame.I)SumiGame.I.SetTitleMusic(true);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void EnterIntro()
        {
            state=SumiRunState.Intro;stateAt=Time.unscaledTime;bannerUntil=stateAt+2.8f;banner="THE PAINTED COURT";
            SumiTime.Reset();controlsOpen=false;aboutOpen=false;combosOpen=false;
            if(player){player.controllable=true;SetPlayerVisible(true);}
            if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.HoldTitle(false);
            if(SumiGame.I)SumiGame.I.SetTitleMusic(false);
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        }
        void BeginJourney()
        {
            controlsOpen=false;aboutOpen=false;combosOpen=false;EnterIntro();
        }
        void ReturnToTitle()
        {
            skipTitle=false;SumiTime.Reset();SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        void SetPlayerVisible(bool visible)
        {
            if(playerRenderers==null&&player)playerRenderers=player.GetComponentsInChildren<Renderer>(true);
            if(playerRenderers==null)return;
            foreach(var r in playerRenderers)if(r)r.enabled=visible&&r.name!="Broken kasa strokes"&&r.name!="Kasa woven ribs";
        }
        void Update()
        {
            SumiTime.Tick();redPulse=Mathf.MoveTowards(redPulse,0,Time.unscaledDeltaTime*1.8f);
            if(player&&player.combat!=null){float target=Mathf.Clamp01(player.combat.health/player.combat.maxHealth);float dt=Time.unscaledDeltaTime;if(target<lastHealth-.001f)trailDelay=Time.unscaledTime+.27f;lastHealth=target;if(target<shownHealth)shownHealth=Mathf.SmoothDamp(shownHealth,target,ref healthVelocity,.17f,Mathf.Infinity,dt);else shownHealth=Mathf.MoveTowards(shownHealth,target,dt*.75f);if(trailHealth<target)trailHealth=Mathf.MoveTowards(trailHealth,target,dt*.75f);else if(Time.unscaledTime>trailDelay)trailHealth=Mathf.SmoothDamp(trailHealth,target,ref trailVelocity,.56f,Mathf.Infinity,dt);}
            if(Time.unscaledTime>=nextColorUpdate){nextColorUpdate=Time.unscaledTime+.05f;ApplyColor();}
            var k=Keyboard.current;
            if(state==SumiRunState.Victory&&k!=null&&k.rKey.wasPressedThisFrame){Restart();return;}
            if(state==SumiRunState.Death)
            {
                bool skip=k!=null&&(k.rKey.wasPressedThisFrame||k.spaceKey.wasPressedThisFrame||k.enterKey.wasPressedThisFrame);
                var mouse=Mouse.current;if(mouse!=null&&mouse.leftButton.wasPressedThisFrame)skip=true;
                if(skip)SumiDeath.Skip();
            }
            if(state==SumiRunState.Intro&&Time.unscaledTime-stateAt>2.2f)BeginWave(1);
            if(state==SumiRunState.Wave||state==SumiRunState.Boss)
            {
                enemies.RemoveAll(e=>!e);
                if(player.combat.health<=0){EnterEnd(false);return;}
                if(Time.time>=nextArrowAt&&SpawnArrow())nextArrowAt=Time.time+ArrowDelay();
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
            if(SumiGame.I&&SumiGame.I.view)SumiGame.I.view.HoldTitle(false);
            SetPlayerVisible(true);controlsOpen=false;aboutOpen=false;combosOpen=false;
            waveIndex=Mathf.Clamp(wave,1,Waves.Length);var plan=Waves[waveIndex-1];
            state=SumiRunState.Wave;spawned=0;attacker=null;nextThreatAt=Time.time+.4f;lastAttackerId=0;player.controllable=true;
            Banner(plan.banner,2.1f);
            // Shades lead so they claim the spread-out gates; retainers fill in behind them.
            int total=plan.shades+plan.retainers,slot=0;
            for(int i=0;i<plan.shades;i++)Spawn(SumiEnemyKind.Shade,slot++,total);
            for(int i=0;i<plan.retainers;i++)Spawn(SumiEnemyKind.Retainer,slot++,total);
            nextArrowAt=Time.time+4f;
        }
        void ClearWave(){if(Waves[waveIndex-1].upgrade)OpenUpgrade();else Advance();}
        void Advance()
        {
            if(waveIndex<Waves.Length){BeginWave(waveIndex+1);return;}
            state=SumiRunState.BossIntro;stateAt=Time.unscaledTime;player.controllable=false;Banner("A BELL BENEATH THE PAPER",1.8f);
        }
        void BeginBoss(){state=SumiRunState.Boss;spawned=0;attacker=null;nextThreatAt=Time.time+.65f;player.controllable=true;Spawn(SumiEnemyKind.Oni,0,1);nextArrowAt=Time.time+3.5f;Banner("THE PAINTED ONI",2.3f);}
        void Spawn(SumiEnemyKind kind,int slot,int total)
        {
            var go=new GameObject(kind==SumiEnemyKind.Oni?"Painted Oni":kind==SumiEnemyKind.Shade?"Ink Shade":"Ashen Retainer");go.transform.position=SpawnPoint(slot,total);
            var e=go.AddComponent<SumiEnemy>();e.Init(player,kind,this);enemies.Add(e);spawned++;
        }
        // Up to four arrivals spread evenly over the gates; later bodies form a second rank so
        // two enemies never materialise inside one another.
        Vector3 SpawnPoint(int slot,int total)
        {
            int rank=slot/Gates.Length;
            Vector3 anchor=Gates[(total<=Gates.Length?Mathf.RoundToInt(slot*(float)Gates.Length/total):slot)%Gates.Length];
            Vector3 outward=anchor.normalized,tangent=Vector3.Cross(Vector3.up,outward);
            return anchor+tangent*(rank*1.7f)-outward*(rank*.9f)+Vector3.up*.02f;
        }
        int AliveCount(){int n=0;foreach(var e in enemies)if(e&&!e.dead)n++;return n;}
        public bool RequestAttack(SumiEnemy enemy)
        {
            if(!CombatActive||Time.time<nextThreatAt)return false;
            if(attacker&&!attacker.dead)return attacker==enemy;
            SumiEnemy best=null;float bestScore=float.MaxValue;
            foreach(var candidate in enemies)
            {
                if(!candidate||!candidate.ReadyToAttack||!CanThreaten(candidate))continue;
                float distance=Vector3.Distance(candidate.transform.position,player.transform.position);
                if(distance>candidate.EngagementRange)continue;
                float score=distance+(candidate.GetInstanceID()==lastAttackerId?1.4f:0);
                if(score<bestScore){bestScore=score;best=candidate;}
            }
            if(best!=enemy)return false;
            attacker=enemy;lastAttackerId=enemy.GetInstanceID();return true;
        }
        bool CanThreaten(SumiEnemy enemy)
        {
            if(Physics.Linecast(enemy.transform.position+Vector3.up,player.transform.position+Vector3.up,1<<8,QueryTriggerInteraction.Ignore))return false;
            var cam=Camera.main;if(!cam)return true;
            Vector3 screen=cam.WorldToViewportPoint(enemy.transform.position+Vector3.up);
            return screen.z>0&&screen.x>.04f&&screen.x<.96f&&screen.y>0&&screen.y<1;
        }
        public void ReleaseAttack(SumiEnemy enemy)
        {
            if(attacker!=enemy)return;
            attacker=null;nextThreatAt=Mathf.Max(nextThreatAt,Time.time+.16f);
        }
        public void CombatHint(string text,float duration)
        {
            if(!shownHints.Add(text))return;
            combatHint=text;hintUntil=Time.time+duration;
        }
        public int OrbitIndex(SumiEnemy e){int i=enemies.IndexOf(e);return i<0?0:i;}
        public void EnemyDied(SumiEnemy enemy){ReleaseAttack(enemy);}
        bool SpawnArrow()
        {
            if(!CombatActive||!player.controllable)return false;
            if(FindFirstObjectByType<SumiArrowStrike>())return false;
            new GameObject("Announced ink arrow").AddComponent<SumiArrowStrike>().Init(player,this);
            return true;
        }
        float ArrowDelay()
        {
            var volley=player&&player.config?player.config.arrowVolley:null;
            return volley!=null?volley.NextDelay(state==SumiRunState.Boss):state==SumiRunState.Boss?8.5f:10f;
        }

        void OpenUpgrade()
        {
            offered.Clear();FillOffers();
            // Nothing left to offer is not a menu; continue rather than showing an empty wheel.
            if(offered.Count==0){Advance();return;}
            state=SumiRunState.Upgrade;player.controllable=false;SumiTime.Menu(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            upgradeArmedAt=Time.unscaledTime+.45f;upgradePress=-1;
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
            state=victory?SumiRunState.Victory:SumiRunState.Death;stateAt=Time.unscaledTime;player.controllable=false;attacker=null;
            foreach(var e in enemies)if(e)e.enabled=false;
            if(victory){SumiTime.Reset();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Banner("THE NIGHT REMEMBERS",99);}
            else redPulse=1;
        }
        public void PlayerDamaged(){redPulse=1;if(player.combat.health<=0)EnterEnd(false);}
        void ApplyColor()
        {
            if(!player||player.combat==null)return;float red=Mathf.Max(redPulse,Mathf.Clamp01((45-player.combat.health)/45f));
            if(playerRenderers==null)playerRenderers=player.GetComponentsInChildren<Renderer>(true);
            if(colorBlock==null)colorBlock=new MaterialPropertyBlock();
            foreach(var r in playerRenderers){if(!r)continue;r.GetPropertyBlock(colorBlock);colorBlock.SetFloat("_Gold",0);colorBlock.SetFloat("_Red",red);r.SetPropertyBlock(colorBlock);}
        }
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
            if(controlsOpen||aboutOpen||combosOpen){CloseOverlay();return;}
            if(state==SumiRunState.Death||state==SumiRunState.Victory||state==SumiRunState.Title)return;
            if(state==SumiRunState.Paused){state=beforePause;bool choosing=state==SumiRunState.Upgrade;SumiTime.Menu(choosing);Cursor.lockState=choosing?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=choosing;}
            else {beforePause=state;state=SumiRunState.Paused;SumiTime.Menu(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        void OpenControls(bool fromTitle){aboutOpen=false;combosOpen=false;controlsOpen=true;overlayFromTitle=fromTitle;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        void OpenAbout(){controlsOpen=false;combosOpen=false;aboutOpen=true;overlayFromTitle=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        void OpenCombos(bool fromTitle){controlsOpen=false;aboutOpen=false;combosOpen=true;combosRevealed=false;overlayFromTitle=fromTitle;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        void CloseOverlay()
        {
            controlsOpen=false;aboutOpen=false;combosOpen=false;combosRevealed=false;
            if(overlayFromTitle||state==SumiRunState.Title){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            else if(state==SumiRunState.Paused){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        void Restart(){skipTitle=true;SumiTime.Reset();SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);}
        public void DebugChoose(int slot){Choose(slot);}
        public void DebugRestart(){Restart();}
        public void DebugBeginJourney(){BeginJourney();}
        void Banner(string text,float duration){banner=text;bannerUntil=Time.unscaledTime+duration;}

        void Styles()
        {
            // Hot reload can keep styles alive while wiping Texture2D fields. Rebuild when any art is gone.
            bool artReady=paperTexture&&sealTexture&&vignetteTexture&&ensoTexture;
            if(title!=null&&wave!=null&&menuItem!=null&&brand!=null&&controlsTitle!=null&&controlsBody!=null&&aboutBody!=null&&cues!=null&&artReady)return;
            Font font=Resources.Load<Font>("Fonts/JiayouAkira-MAVEY");
            Color cream=new Color(.96f,.92f,.82f);
            title=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,font=font,fontSize=Mathf.RoundToInt(Screen.height*.052f)};title.normal.textColor=cream;
            small=new GUIStyle(title){fontSize=Mathf.RoundToInt(Screen.height*.020f)};
            hud=new GUIStyle(small);hud.normal.textColor=cream;
            card=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleCenter,font=font,fontSize=Mathf.RoundToInt(Screen.height*.025f)};card.normal.textColor=cream;
            center=new GUIStyle(small){fontSize=Mathf.RoundToInt(Screen.height*.028f)};
            // Ofuda text: dark ink on paper. Paint every style state so hover never flashes white.
            Color inkDark=new Color(.055f,.045f,.040f),inkRed=new Color(.42f,.040f,.055f),inkNote=new Color(.22f,.18f,.15f);
            wheelName=new GUIStyle(title){fontSize=Mathf.RoundToInt(Screen.height*.034f),wordWrap=true,alignment=TextAnchor.UpperCenter};PaintStyle(wheelName,inkDark);
            wheelNameLit=new GUIStyle(wheelName);PaintStyle(wheelNameLit,inkRed);
            wheelNote=new GUIStyle(small){fontSize=Mathf.RoundToInt(Screen.height*.024f),wordWrap=true,alignment=TextAnchor.UpperCenter};PaintStyle(wheelNote,inkNote);
            wave=new GUIStyle(title){alignment=TextAnchor.UpperLeft,fontSize=Mathf.RoundToInt(Screen.height*.040f)};wave.normal.textColor=cream;
            // Pause sits on a dark wash, so the line and choices read as pale ink.
            pauseNote=new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(Screen.height*.022f)};pauseNote.normal.textColor=new Color(.86f,.82f,.72f);
            menuItem=new GUIStyle(title){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(Screen.height*.038f)};menuItem.normal.textColor=new Color(.62f,.58f,.50f);
            menuItemLit=new GUIStyle(menuItem);menuItemLit.normal.textColor=cream;
            brand=new GUIStyle(menuItemLit){fontSize=Mathf.RoundToInt(Screen.height*.11f)};
            controlsTitle=new GUIStyle(menuItemLit){fontSize=Mathf.RoundToInt(Screen.height*.048f)};
            controlsBody=new GUIStyle(pauseNote){fontSize=Mathf.RoundToInt(Screen.height*.028f),wordWrap=true,alignment=TextAnchor.MiddleCenter};controlsBody.normal.textColor=cream;
            aboutBody=new GUIStyle(controlsBody){fontSize=Mathf.RoundToInt(Screen.height*.034f)};
            cues=new GUIStyle(hud){alignment=TextAnchor.LowerRight,fontSize=Mathf.RoundToInt(Screen.height*.018f)};
            BuildHudTextures();
        }
        void OnGUI()
        {
            Styles();float w=Screen.width,h=Screen.height;Color old=GUI.color;
            if(state==SumiRunState.Title)
            {
                if(controlsOpen)DrawControls(w,h);
                else if(combosOpen)DrawCombos(w,h);
                else if(aboutOpen)DrawAbout(w,h);
                else DrawTitleMenu(w,h);
                return;
            }
            float low=player&&player.combat!=null?Mathf.Clamp01((45-player.combat.health)/45f):0;
            if(state!=SumiRunState.Death&&state!=SumiRunState.Victory&&(low>0||redPulse>0)){GUI.color=new Color(.48f,.015f,.02f,Mathf.Max(low*.30f,redPulse*.42f));GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);}
            GUI.color=old;
            bool menuOpen=state==SumiRunState.Paused||state==SumiRunState.Upgrade||state==SumiRunState.Death||state==SumiRunState.Victory;
            if(player&&player.combat!=null&&!menuOpen)
            {
                DrawCombo(w,h);DrawHealth(w,h);DrawActionCues(w,h);
                if(player.combat.ExecutionTarget)GUI.Label(new Rect(w*.37f,h*.69f,w*.26f,42),"E  —  DECISIVE CUT",center);
            }
            if(!menuOpen&&state!=SumiRunState.Intro)DrawWaveCounter(w,h);
            if(!menuOpen&&player&&player.locked&&player.target){var lockedEnemy=player.target.GetComponent<SumiEnemy>();if(lockedEnemy&&!lockedEnemy.dead)DrawEnemyHealth(w,h,lockedEnemy);}
            if(Time.unscaledTime<bannerUntil&&!menuOpen)GUI.Label(new Rect(w*.15f,h*.13f,w*.70f,h*.10f),banner,title);
            if(state==SumiRunState.Intro)GUI.Label(new Rect(w*.12f,h*.68f,w*.76f,h*.26f),ControlsBody,controlsBody??small??GUI.skin.label);
            if(CombatActive&&Time.time<hintUntil)GUI.Label(new Rect(w*.15f,h*.79f,w*.70f,35),combatHint,small);
            if(CombatActive&&attacker&&attacker.Attacking&&attacker.CurrentAttack!=null)
                GUI.Label(new Rect(w*.25f,h*.735f,w*.5f,30),attacker.CurrentAttack.unblockable?"CRIMSON SWEEP  —  EVADE":attacker.Braced?"BRACED  —  HEAVY / DEFLECT":attacker.CurrentAttack.name,small);
            if(state==SumiRunState.Upgrade)DrawSpellWheel(w,h);
            if(state==SumiRunState.Death)DrawDeathMenu(w,h);
            else if(state==SumiRunState.Victory){GUI.Label(new Rect(w*.2f,h*.58f,w*.6f,50),"THE COURT IS QUIET",center);if(GUI.Button(new Rect(w*.39f,h*.69f,w*.22f,48),"R  —  RISE AGAIN",card))Restart();}
            if(controlsOpen)DrawControls(w,h);
            else if(combosOpen)DrawCombos(w,h);
            else if(aboutOpen)DrawAbout(w,h);
            else if(state==SumiRunState.Paused)DrawPauseMenu(w,h);
        }
        void DrawWaveCounter(float w,float h)
        {
            int current=Mathf.Clamp(waveIndex,1,Waves.Length);
            GUIStyle counter=wave??hud??GUI.skin.label;
            Color old=GUI.color;Rect r=new Rect(w*.035f,h*.046f,180,60);
            GUI.color=new Color(.012f,.011f,.010f,.18f);GUI.Label(new Rect(r.x+2,r.y+2,r.width,r.height),current+"/"+Waves.Length,counter);
            GUI.color=old;GUI.Label(r,current+"/"+Waves.Length,counter);
            GUI.color=old;
        }
        void DrawActionCues(float w,float h)
        {
            float cueW=Mathf.Clamp(w*.20f,170,260),cueH=Mathf.Clamp(h*.22f,130,190);
            GUI.Label(new Rect(w-cueW-w*.03f,h-cueH-h*.045f,cueW,cueH),ActionCues,cues??hud??small??GUI.skin.label);
        }
        // No panel. No rods. Night wash, one quiet line, two ink choices. Ma does the rest.
        void DrawPauseMenu(float w,float h)
        {
            Color old=GUI.color;
            GUI.color=new Color(.010f,.010f,.012f,.58f);GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);
            Tex(vignetteTexture,new Rect(0,0,w,h),new Color(.008f,.008f,.009f,.72f));
            float lineY=h*.18f;
            GUI.color=old;
            GUI.Label(new Rect(w*.08f,lineY,w*.84f,42),PauseLine,pauseNote??small??GUI.skin.label);
            InkRule(w*.32f,lineY+46,w*.36f,2.2f,new Color(.86f,.82f,.72f,.38f));
            float iw=Mathf.Clamp(w*.42f,320,560),ih=Mathf.Max(40,h*.052f),ix=(w-iw)*.5f,iy=h*.30f,gap=ih*1.12f;
            if(PauseChoice(new Rect(ix,iy,iw,ih),"RESUME"))TogglePause();
            if(PauseChoice(new Rect(ix,iy+gap,iw,ih),"RISE AGAIN"))Restart();
            if(PauseChoice(new Rect(ix,iy+gap*2f,iw,ih),"CONTROLS"))OpenControls(false);
            if(PauseChoice(new Rect(ix,iy+gap*3f,iw,ih),"COMBOS"))OpenCombos(false);
            if(PauseChoice(new Rect(ix,iy+gap*4f,iw,ih),"MAIN MENU"))ReturnToTitle();
            float sig=Mathf.Max(22,h*.032f);
            Tex(sealTexture,new Rect(w*.5f-sig*.5f,h*.88f,sig,sig),new Color(.72f,.055f,.075f,.78f));
            GUI.color=old;
        }
        void DrawDeathMenu(float w,float h)
        {
            float reveal=SumiDeath.UiReveal;if(reveal<.02f)return;
            // Stretch the wash across nearly the whole reveal so death reads before the menu owns the frame.
            float a=Mathf.SmoothStep(0,1,Mathf.Clamp01((reveal-.03f)/.90f));
            Color old=GUI.color;
            GUI.color=new Color(.010f,.010f,.012f,.60f*a);GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);
            Tex(vignetteTexture,new Rect(0,0,w,h),new Color(.008f,.008f,.009f,.78f*a));
            GUI.color=old;
            float lineY=h*.26f;
            GUI.color=new Color(1,1,1,a);
            GUI.Label(new Rect(w*.06f,lineY,w*.88f,56),DeathLine,menuItemLit??menuItem??pauseNote??GUI.skin.label);
            InkRule(w*.28f,lineY+72,w*.44f,2.4f,new Color(.86f,.82f,.72f,.42f*a));
            InkRule(w*.36f,lineY+78,w*.28f,1.4f,new Color(.70f,.050f,.070f,.55f*a));
            float iw=Mathf.Clamp(w*.42f,320,560),ih=Mathf.Max(48,h*.065f),ix=(w-iw)*.5f,iy=h*.44f,gap=ih*1.28f;
            if(PauseChoice(new Rect(ix,iy,iw,ih),"RISE AGAIN"))SumiDeath.Skip();
            if(PauseChoice(new Rect(ix,iy+gap,iw,ih),"MAIN MENU"))ReturnToTitle();
            float sig=Mathf.Max(24,h*.036f);
            Tex(sealTexture,new Rect(w*.5f-sig*.5f,h*.86f,sig,sig),new Color(.72f,.055f,.075f,.90f*a));
            GUI.color=old;
        }
        void DrawTitleMenu(float w,float h)
        {
            Color old=GUI.color;
            Tex(vignetteTexture,new Rect(0,0,w,h),new Color(.008f,.008f,.009f,.42f));
            GUI.color=old;
            float brandY=h*.10f;
            GUI.Label(new Rect(w*.1f,brandY,w*.8f,Mathf.RoundToInt(h*.14f)),Identity.Title,brand??menuItemLit??GUI.skin.label);
            InkRule(w*.38f,brandY+h*.13f,w*.24f,2.2f,new Color(.86f,.82f,.72f,.38f));
        float iw=Mathf.Clamp(w*.34f,280,460),ih=Mathf.Max(44,h*.058f),ix=(w-iw)*.5f,iy=h*.50f,gap=ih*1.18f;
            if(TitleChoice(new Rect(ix,iy,iw,ih),"BEGIN JOURNEY"))BeginJourney();
            if(TitleChoice(new Rect(ix,iy+gap,iw,ih),"CONTROLS"))OpenControls(true);
            if(TitleChoice(new Rect(ix,iy+gap*2f,iw,ih),"COMBOS"))OpenCombos(true);
            if(TitleChoice(new Rect(ix,iy+gap*3f,iw,ih),"ABOUT"))OpenAbout();
            float sig=Mathf.Max(22,h*.032f);
            Tex(sealTexture,new Rect(w*.5f-sig*.5f,h*.88f,sig,sig),new Color(.72f,.055f,.075f,.78f));
            GUI.color=old;
        }
        void DrawControls(float w,float h)
        {
            Color old=GUI.color;
            GUI.color=new Color(.010f,.010f,.012f,.62f);GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);
            Tex(vignetteTexture,new Rect(0,0,w,h),new Color(.008f,.008f,.009f,.72f));
            float lineY=h*.16f;
            GUI.color=old;
            GUI.Label(new Rect(w*.08f,lineY,w*.84f,56),ControlsLine,controlsTitle??menuItemLit??GUI.skin.label);
            InkRule(w*.36f,lineY+70,w*.28f,2.2f,new Color(.86f,.82f,.72f,.38f));
            GUI.Label(new Rect(w*.08f,h*.34f,w*.84f,h*.28f),ControlsBody,controlsBody??pauseNote??GUI.skin.label);
            float iw=Mathf.Clamp(w*.34f,280,460),ih=Mathf.Max(48,h*.065f),ix=(w-iw)*.5f,iy=h*.72f;
            if(PauseChoice(new Rect(ix,iy,iw,ih),"BACK"))CloseOverlay();
            float sig=Mathf.Max(22,h*.032f);
            Tex(sealTexture,new Rect(w*.5f-sig*.5f,h*.88f,sig,sig),new Color(.72f,.055f,.075f,.78f));
            GUI.color=old;
        }
        void DrawAbout(float w,float h)
        {
            Color old=GUI.color;
            GUI.color=new Color(.010f,.010f,.012f,.62f);GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);
            Tex(vignetteTexture,new Rect(0,0,w,h),new Color(.008f,.008f,.009f,.72f));
            float lineY=h*.14f;
            GUI.color=old;
            GUI.Label(new Rect(w*.08f,lineY,w*.84f,56),AboutLine,controlsTitle??menuItemLit??GUI.skin.label);
            InkRule(w*.38f,lineY+70,w*.24f,2.2f,new Color(.86f,.82f,.72f,.38f));
            GUI.Label(new Rect(w*.10f,h*.30f,w*.80f,h*.36f),AboutBody,aboutBody??controlsBody??pauseNote??GUI.skin.label);
            float iw=Mathf.Clamp(w*.34f,280,460),ih=Mathf.Max(48,h*.065f),ix=(w-iw)*.5f,iy=h*.72f;
            if(PauseChoice(new Rect(ix,iy,iw,ih),"BACK"))CloseOverlay();
            float sig=Mathf.Max(22,h*.032f);
            Tex(sealTexture,new Rect(w*.5f-sig*.5f,h*.88f,sig,sig),new Color(.72f,.055f,.075f,.78f));
            GUI.color=old;
        }
        void DrawCombos(float w,float h)
        {
            Color old=GUI.color;
            GUI.color=new Color(.010f,.010f,.012f,.62f);GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);
            Tex(vignetteTexture,new Rect(0,0,w,h),new Color(.008f,.008f,.009f,.72f));
            float lineY=h*.12f;
            GUI.color=old;
            GUI.Label(new Rect(w*.08f,lineY,w*.84f,56),CombosLine,controlsTitle??menuItemLit??GUI.skin.label);
            InkRule(w*.38f,lineY+70,w*.24f,2.2f,new Color(.86f,.82f,.72f,.38f));
            float iw=Mathf.Clamp(w*.34f,280,460),ih=Mathf.Max(48,h*.065f),ix=(w-iw)*.5f;
            if(!combosRevealed)
            {
                GUI.Label(new Rect(w*.12f,h*.32f,w*.76f,h*.28f),CombosGate,aboutBody??controlsBody??pauseNote??GUI.skin.label);
                if(PauseChoice(new Rect(ix,h*.66f,iw,ih),"CONTINUE"))combosRevealed=true;
                if(PauseChoice(new Rect(ix,h*.66f+ih*1.25f,iw,ih),"BACK"))CloseOverlay();
            }
            else
            {
                GUI.Label(new Rect(w*.12f,h*.28f,w*.76f,h*.40f),CombosBody,aboutBody??controlsBody??pauseNote??GUI.skin.label);
                if(PauseChoice(new Rect(ix,h*.74f,iw,ih),"BACK"))CloseOverlay();
            }
            float sig=Mathf.Max(22,h*.032f);
            Tex(sealTexture,new Rect(w*.5f-sig*.5f,h*.90f,sig,sig),new Color(.72f,.055f,.075f,.78f));
            GUI.color=old;
        }
        bool TitleChoice(Rect r,string label)
        {
            Color old=GUI.color;bool hover=r.Contains(Event.current.mousePosition);
            if(hover)
            {
                float seal=Mathf.Max(16,r.height*.42f);
                Tex(sealTexture,new Rect(r.x-seal*1.35f,r.y+(r.height-seal)*.5f,seal,seal),new Color(.74f,.055f,.078f,.95f));
            }
            GUI.color=old;
            GUI.Label(r,label,menuItemLit??menuItem??title??GUI.skin.label);
            InkRule(r.x+r.width*(hover?.05f:.28f),r.y+r.height*.82f,r.width*(hover?.90f:.44f),hover?2.8f:1.5f,new Color(.86f,.82f,.72f,hover?.72f:.28f));
            return GUI.Button(r,GUIContent.none,GUIStyle.none);
        }
        bool PauseChoice(Rect r,string label)
        {
            Color old=GUI.color;bool hover=r.Contains(Event.current.mousePosition);
            if(hover)
            {
                float seal=Mathf.Max(16,r.height*.42f);
                Tex(sealTexture,new Rect(r.x-seal*1.35f,r.y+(r.height-seal)*.5f,seal,seal),new Color(.74f,.055f,.078f,.95f));
            }
            GUI.color=old;
            GUI.Label(r,label,(hover?menuItemLit:menuItem)??title??GUI.skin.label);
            InkRule(r.x+r.width*(hover?.05f:.28f),r.y+r.height*.82f,r.width*(hover?.90f:.44f),hover?2.8f:1.5f,new Color(.86f,.82f,.72f,hover?.72f:.22f));
            return GUI.Button(r,GUIContent.none,GUIStyle.none);
        }
        // A drawn rule rather than a rectangle: the brush presses down, holds, then lifts dry.
        void InkRule(float x,float y,float width,float thickness,Color color)
        {
            Color old=GUI.color;const int Steps=22;
            for(int i=0;i<Steps;i++)
            {
                float t=i/(float)(Steps-1),fade=Mathf.Sin(Mathf.PI*Mathf.Pow(t,.78f));
                GUI.color=new Color(color.r,color.g,color.b,color.a*Mathf.Clamp01(.18f+fade*.95f));
                GUI.DrawTexture(new Rect(x+width*t,y,width/Steps+1.2f,thickness*(.40f+fade*.80f)),Texture2D.whiteTexture);
            }
            GUI.color=old;
        }
        void InkColumn(float x,float y,float height,float thickness,Color color)
        {
            Color old=GUI.color;const int Steps=24;
            for(int i=0;i<Steps;i++)
            {
                float t=i/(float)(Steps-1),fade=Mathf.Sin(Mathf.PI*Mathf.Pow(t,.78f));
                GUI.color=new Color(color.r,color.g,color.b,color.a*Mathf.Clamp01(.18f+fade*.95f));
                GUI.DrawTexture(new Rect(x-thickness*(.40f+fade*.80f)*.5f,y+height*t,thickness*(.40f+fade*.80f),height/Steps+1.2f),Texture2D.whiteTexture);
            }
            GUI.color=old;
        }
        static void Tex(Texture2D texture,Rect r,Color tint)
        {
            if(!texture)return;
            Color old=GUI.color;GUI.color=tint;GUI.DrawTexture(r,texture);GUI.color=old;
        }
        void DrawCombo(float w,float h)
        {
            float x=w*.035f,size=Mathf.Clamp(h*.028f,20,30),y=h*.94f-size-18;int step=player.combat.ComboStep;
            for(int i=0;i<3;i++)
            {
                float px=x+i*(size+8);GUI.color=new Color(.02f,.018f,.02f,.90f);GUI.DrawTexture(new Rect(px-2,y-2,size+4,size+4),pipTexture);
                GUI.color=i<step?(i==2?new Color(.68f,.11f,.13f):new Color(.96f,.90f,.78f)):new Color(.25f,.23f,.23f);
                GUI.DrawTexture(new Rect(px,y,size,size),pipTexture);
            }
            GUI.color=Color.white;
        }
        void DrawHealth(float w,float h)
        {
            float x=w*.035f,width=Mathf.Clamp(w*.29f,220,400),y=h*.94f;
            Rect bar=new Rect(x,y,width,9);
            GUI.color=new Color(.015f,.014f,.018f,.9f);GUI.DrawTexture(new Rect(bar.x-2,bar.y-2,bar.width+4,bar.height+4),Texture2D.whiteTexture);
            GUI.color=new Color(.23f,.05f,.06f,1);GUI.DrawTexture(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(trailHealth),bar.height),Texture2D.whiteTexture);
            GUI.color=Color.white;GUI.DrawTexture(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(shownHealth),bar.height),barTexture);
            GUI.color=Color.white;
        }
        void DrawEnemyHealth(float w,float h,SumiEnemy enemy)
        {
            float width=Mathf.Clamp(w*.23f,210,360),x=w-width-w*.035f,y=h*.078f;
            string name=enemy.kind==SumiEnemyKind.Oni?(enemy.Enraged?"PAINTED ONI — AWAKENED":"PAINTED ONI"):enemy.kind==SumiEnemyKind.Shade?"INK SHADE":"ASHEN RETAINER";
            GUI.Label(new Rect(x,y-38,width,34),name,hud);
            GUI.color=new Color(.025f,.022f,.025f,.90f);GUI.DrawTexture(new Rect(x-2,y-2,width+4,10),Texture2D.whiteTexture);
            GUI.color=new Color(.68f,.10f,.13f);GUI.DrawTexture(new Rect(x,y,width*Mathf.Clamp01(enemy.health/enemy.maxHealth),6),Texture2D.whiteTexture);GUI.color=Color.white;
            if(enemy.Attacking&&enemy.CurrentAttack!=null)
                GUI.Label(new Rect(x,y+15,width,50),enemy.CurrentAttack.unblockable?"CRIMSON SWEEP\nEVADE":enemy.Braced?"BRACED\nHEAVY / DEFLECT":enemy.CurrentAttack.name,hud);
        }
        // Three ofuda slips instead of a pie chart. Empty night behind them; vermillion marks the chosen one.
        void DrawSpellWheel(float w,float h)
        {
            FillOffers();
            Color old=GUI.color;
            GUI.color=new Color(.012f,.011f,.013f,.58f);GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);
            Tex(vignetteTexture,new Rect(0,0,w,h),new Color(.008f,.008f,.009f,.70f));
            float ring=Mathf.Min(w,h)*.72f;
            Tex(ensoTexture,new Rect((w-ring)*.5f,(h-ring)*.52f,ring,ring),new Color(.92f,.88f,.78f,.14f));
            GUI.color=old;
            GUI.Label(new Rect(w*.1f,h*.08f,w*.8f,36),ChoosePrompt,pauseNote??small??GUI.skin.label);
            InkRule(w*.38f,h*.08f+40,w*.24f,2f,new Color(.86f,.82f,.72f,.32f));
            int filled=Mathf.Min(3,offered.Count);
            float slipW=Mathf.Clamp(w*.17f,150,230),slipH=Mathf.Clamp(h*.52f,320,520),gap=Mathf.Clamp(w*.035f,24,48);
            float total=filled*slipW+(filled-1)*gap,x0=(w-total)*.5f,y0=h*.22f;
            bool armed=Time.unscaledTime>=upgradeArmedAt;
            int hover=-1;
            for(int i=0;i<filled;i++)
            {
                Rect slip=new Rect(x0+i*(slipW+gap),y0,slipW,slipH);
                bool over=slip.Contains(Event.current.mousePosition);
                if(over)hover=i;
                DrawOfuda(slip,UpgradeNames[offered[i]],UpgradeText[offered[i]],i==hover);
                if(!armed)continue;
                if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&over){upgradePress=i;Event.current.Use();}
                if(Event.current.type==EventType.MouseUp&&Event.current.button==0&&over&&upgradePress==i){Choose(i);upgradePress=-1;Event.current.Use();}
            }
            if(Event.current.type==EventType.MouseUp&&Event.current.button==0)upgradePress=-1;
            GUI.color=old;
        }
        void DrawOfuda(Rect slip,string name,string note,bool lit)
        {
            Color old=GUI.color;
            Tex(paperTexture,new Rect(slip.x+6,slip.y+10,slip.width,slip.height),new Color(.02f,.018f,.015f,.28f));
            Tex(paperTexture,slip,lit?new Color(.96f,.93f,.84f,.98f):new Color(.90f,.875f,.80f,.94f));
            if(lit)InkColumn(slip.x+10,slip.y+18,slip.height-36,4.5f,new Color(.70f,.050f,.070f,.88f));
            else InkColumn(slip.x+slip.width*.5f,slip.y+18,slip.height*.12f,2f,new Color(.08f,.07f,.06f,.45f));
            float nameH=slip.height*.22f;
            GUI.Label(new Rect(slip.x+10,slip.y+slip.height*.18f,slip.width-20,nameH),name,lit?wheelNameLit:wheelName);
            InkRule(slip.x+slip.width*.18f,slip.y+slip.height*.42f,slip.width*.64f,1.8f,new Color(.08f,.07f,.06f,lit?.55f:.28f));
            GUI.Label(new Rect(slip.x+12,slip.y+slip.height*.48f,slip.width-24,slip.height*.28f),note,wheelNote);
            if(lit)
            {
                float seal=Mathf.Max(18,slip.width*.18f);
                Tex(sealTexture,new Rect(slip.xMax-seal-14,slip.yMax-seal-18,seal,seal),new Color(.72f,.055f,.075f,.92f));
            }
            GUI.color=old;
        }
        static void PaintStyle(GUIStyle style,Color color)
        {
            if(style==null)return;
            style.normal.textColor=color;style.hover.textColor=color;style.active.textColor=color;style.focused.textColor=color;
            style.onNormal.textColor=color;style.onHover.textColor=color;style.onActive.textColor=color;style.onFocused.textColor=color;
        }
        void BuildHudTextures()
        {
            if(pipTexture)Destroy(pipTexture);if(barTexture)Destroy(barTexture);
            if(sealTexture)Destroy(sealTexture);if(paperTexture)Destroy(paperTexture);
            if(vignetteTexture)Destroy(vignetteTexture);if(ensoTexture)Destroy(ensoTexture);
            pipTexture=new Texture2D(48,48,TextureFormat.RGBA32,false);pipTexture.filterMode=FilterMode.Bilinear;
            for(int y=0;y<48;y++)for(int x=0;x<48;x++){float r=Vector2.Distance(new Vector2(x,y),new Vector2(23.5f,23.5f));float alpha=Mathf.Clamp01((23-r)*.75f);pipTexture.SetPixel(x,y,new Color(1,1,1,alpha));}pipTexture.Apply();
            barTexture=new Texture2D(256,1,TextureFormat.RGBA32,false);barTexture.filterMode=FilterMode.Bilinear;
            for(int x=0;x<256;x++){float t=x/255f;barTexture.SetPixel(x,0,Color.Lerp(new Color(.76f,.13f,.18f),new Color(.055f,.015f,.025f),Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.65f)/.35f))));}barTexture.Apply();
            BuildSeal();BuildPaper();BuildVignette();BuildEnso();
        }
        // Deterministic value noise. The menus should look hand-made but rebuild identically.
        static float Noise(float x,float y){float n=Mathf.Sin(x*12.9898f+y*78.233f)*43758.5453f;return n-Mathf.Floor(n);}
        static float Fbm(float x,float y)=>Noise(x,y)*.55f+Noise(x*2.3f+11.7f,y*2.3f-4.1f)*.30f+Noise(x*5.1f-3.3f,y*5.1f+8.8f)*.15f;
        // One incomplete enso behind the ofuda: a single brush circle that never quite closes.
        void BuildEnso()
        {
            const int S=384;float c=(S-1)*.5f;var pixels=new Color[S*S];
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float dx=x-c,dy=c-y,r=Mathf.Sqrt(dx*dx+dy*dy)/S;
                float ang=(Mathf.Atan2(dy,dx)*Mathf.Rad2Deg+360)%360,t=ang/360f;
                float band=.34f+.01f*Mathf.Sin(t*8.1f);
                float swell=Mathf.Pow(Mathf.Max(0,Mathf.Sin(Mathf.PI*Mathf.Pow(Mathf.Clamp01((t-.04f)/.88f),.82f))),.55f);
                float width=(.018f*swell+.0025f)*(1f-.35f*t);
                float alpha=0;
                if(t>.04f&&t<.94f)
                {
                    alpha=Mathf.Clamp01(1f-(Mathf.Abs(r-band)-width*.45f)/Mathf.Max(width*.55f,.0001f));
                    alpha*=Mathf.Clamp01(.55f+Fbm(ang*.5f,r*90f)*.8f);
                    alpha*=1f-.5f*Mathf.Clamp01((Fbm(t*30f,r*140f)-.62f)*3.2f);
                    alpha*=Mathf.Clamp01(1.05f-.55f*t*t);
                }
                pixels[y*S+x]=new Color(1,1,1,Mathf.Clamp01(alpha));
            }
            ensoTexture=new Texture2D(S,S,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            ensoTexture.SetPixels(pixels);ensoTexture.Apply();
        }
        // A carved stone seal: solid block, two cut strokes, edges eaten away by the stone.
        void BuildSeal()
        {
            const int S=128;var pixels=new Color[S*S];
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=x/(float)(S-1),v=y/(float)(S-1);
                float alpha=u>.07f&&u<.93f&&v>.07f&&v<.93f?1:0;
                bool cut=(v>.44f&&v<.55f&&u>.19f&&u<.81f)||(u>.45f&&u<.56f&&v>.21f&&v<.79f)||(v>.70f&&v<.78f&&u>.27f&&u<.73f);
                if(cut)alpha=0;
                alpha*=Mathf.Clamp01(.55f+Fbm(u*9f,v*9f)*.95f);
                float edge=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v));
                alpha*=Mathf.Clamp01((edge-.045f)*26f+Fbm(u*22f,v*22f)*.70f);
                pixels[y*S+x]=new Color(1,1,1,Mathf.Clamp01(alpha));
            }
            sealTexture=new Texture2D(S,S,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            sealTexture.SetPixels(pixels);sealTexture.Apply();
        }
        // Handmade sheet: fibres in the tone, and a deckle edge that wanders down both sides.
        void BuildPaper()
        {
            const int W=128,H=384;var pixels=new Color[W*H];
            for(int y=0;y<H;y++)
            {
                float left=1.4f+Fbm(y*.090f,3.1f)*4.2f,right=W-1.4f-Fbm(y*.085f,17.7f)*4.2f;
                for(int x=0;x<W;x++)
                {
                    float alpha=Mathf.Clamp01((x-left)*1.5f)*Mathf.Clamp01((right-x)*1.5f);
                    float fibre=.90f+Fbm(x*.28f,y*.070f)*.18f;
                    pixels[y*W+x]=new Color(fibre,fibre,fibre,alpha);
                }
            }
            paperTexture=new Texture2D(W,H,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            paperTexture.SetPixels(pixels);paperTexture.Apply();
        }
        void BuildVignette()
        {
            const int S=128;var pixels=new Color[S*S];
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                float u=x/(float)(S-1)*2-1,v=y/(float)(S-1)*2-1;
                pixels[y*S+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(Mathf.Sqrt(u*u+v*v)*.72f),2.1f));
            }
            vignetteTexture=new Texture2D(S,S,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            vignetteTexture.SetPixels(pixels);vignetteTexture.Apply();
        }
        void OnDestroy()
        {
            if(pipTexture)Destroy(pipTexture);if(barTexture)Destroy(barTexture);
            if(sealTexture)Destroy(sealTexture);if(paperTexture)Destroy(paperTexture);
            if(vignetteTexture)Destroy(vignetteTexture);if(ensoTexture)Destroy(ensoTexture);
        }
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace MergeTD
{
    public sealed class DefenseBattle
    {
        public const int Columns=18,Rows=11;
        public sealed class Tower
        {
            public string id; public int level,tier,index,targetMode; public UnitDefinition definition; public JObject profile;
            public Vector2 position;public int[] paths=new int[3];
            public float hp,maxHp,shield,shieldUntil,regenUntil,regen,attackBuff,hasteBuff,guardBuff,healBuff;
            public float cooldown,charge,abilityClock,reactClock,focus,heat;public int attacks,chargeCount;
            public Enemy target;public readonly Dictionary<string,float> clocks=new Dictionary<string,float>();public bool Alive=>hp>0;
        }
        public sealed class Enemy
        {
            public int serial;public string monsterId;public Vector2 position;public float progress,hp,maxHp,speed,attack,cooldown,slow,slowUntil,rootUntil,rootImmune;
            public bool boss;public Tower blocker;public float markBonus,markUntil;public readonly Dictionary<Tower,Mark> marks=new Dictionary<Tower,Mark>();public bool Alive=>hp>0;
        }
        public sealed class Mark {public string kind;public float expires,tick,damage;public int transfers;}
        public sealed class Shot
        {
            public Tower owner;public Enemy target;public Vector2 position,direction,destination;public float speed,damage,range,travel,radius,width;
            public int remaining;public bool homing;public string mode;public readonly HashSet<int> hit=new HashSet<int>();
        }
        public sealed class Flash {public Vector2 from,to;public Color color;public float life;}
        public readonly List<Tower> Towers=new List<Tower>();public readonly List<Enemy> Enemies=new List<Enemy>();public readonly List<Shot> Shots=new List<Shot>();public readonly List<Flash> Flashes=new List<Flash>();
        public readonly Vector2[] Route={new Vector2(-.5f,2),new Vector2(16,2),new Vector2(16,8),new Vector2(3,8),new Vector2(3,5)};
        public readonly GameContent Content;private readonly MergeGameDatabase database;
        public int Cash{get;private set;}public int Lives{get;private set;}=20;public int Wave{get;private set;}public int Kills{get;private set;}
        public bool Running{get;private set;}public bool Finished{get;private set;}public bool Won{get;private set;}
        public int TotalWaves=>(int)Content.Rules["totalWaves"];public float Time{get;private set;}public event Action Changed;
        private int remaining,spawned,serial;private float spawnClock,intermission=1;private bool waveActive;
        public DefenseBattle(MergeGameDatabase db,GameContent content){database=db;Content=content;Cash=(int)content.Rules["initialBattleCash"];}
        public void Start(){if(!Finished)Running=true;}
        public bool IsPath(int x,int y)=>y==2&&x<=16||x==16&&y>=2&&y<=8||y==8&&x>=3&&x<=16||x==3&&y>=5&&y<=8;
        public bool Deploy(string id,int level,Vector2 position,out string reason)
        {
            reason="";int cost=(int)Content.Rules["deployCost"]*database.GetUnit(id).tier;
            if(Finished||level<1||level>Content.MaxLevel||position.x<0||position.x>=Columns||position.y<0||position.y>=Rows||IsPath(Mathf.RoundToInt(position.x),Mathf.RoundToInt(position.y))||Towers.Any(t=>t.Alive&&Vector2.Distance(t.position,position)<.5f)){reason="빈 배치 칸을 선택하세요";return false;}
            if(Cash<cost){reason="전투 재화가 부족합니다";return false;}
            var u=new Tower{id=id,level=level,tier=database.GetUnit(id).tier,index=Towers.Count,definition=database.GetUnit(id),profile=Content.Profile(id),position=position};
            u.maxHp=BaseHp(u);u.hp=u.maxHp;u.abilityClock=.5f;Towers.Add(u);Cash-=cost;
            if(Kind(u)=="court_start_buff_shield"&&Running)StartRoyal(u);Changed?.Invoke();return true;
        }
        public bool Upgrade(Tower u,int path,out string reason)
        {
            reason="";if(u==null||!u.Alive||Finished){reason="살아 있는 아군을 선택하세요";return false;}
            if(!Content.CanUpgrade(u.level,u.paths,path,out reason))return false;
            int cost=Content.UpgradeCost(u.tier,u.paths[path]+1);if(Cash<cost){reason="전투 재화가 부족합니다";return false;}
            float oldMax=u.maxHp;u.paths[path]++;Cash-=cost;u.maxHp=BaseHp(u)*(1+Trait(u,"tank")*.18f);u.hp=Mathf.Min(u.maxHp,u.hp+u.maxHp-oldMax);Changed?.Invoke();return true;
        }
        public float Trait(Tower u,string type){float n=0;for(int i=0;i<3;i++)if((string)Content.Path(u.id,i)["archetype"]==type)n+=u.paths[i]*(u.paths[i]==5?1.5f:1);return n;}
        private float BaseHp(Tower u)=>(150+u.tier*50+u.definition.knight*40)*Mathf.Pow(1.18f,u.level-1);
        private float Power(Tower u)=>(16+u.tier*9)*Mathf.Pow(1.22f,u.level-1);
        public float AttackRange(Tower u)=>((N(u.profile["attack"],"rangeTiles")>0?N(u.profile["attack"],"rangeTiles"):HasOffense(u)?3:0)+(Kind(u)=="focus_range_stack"?u.focus*.2f:0))*(1+Trait(u,"reach")*.10f);
        public float SupportRange(Tower u) { var a=u.profile["ability"]; bool baseSupport=N(a,"maxDistinctAllyTargetsPerProc")>0; bool extra=Trait(u,"heal")+Trait(u,"regen")+Trait(u,"shield")+Trait(u,"aura")+Trait(u,"guard")+Trait(u,"cleanse")>0; if(!baseSupport&&!extra)return 0; return Mathf.Max(extra?3:0,N(a,"acquisitionRangeTiles"))*(1+Trait(u,"reach")*.10f); }
        private bool HasOffense(Tower u)=>Trait(u,"area")+Trait(u,"dot")+Trait(u,"burst")+Trait(u,"single")+Trait(u,"multi")+Trait(u,"chain")+Trait(u,"pierce")+Trait(u,"mark")>0;
        private static float N(JToken t,string k,float fallback=0)=>GameContent.Number(t,k,fallback);
        private static string S(JToken t,string k)=>GameContent.Text(t,k);
        private JObject Params(Tower u)=>(JObject)u.profile["ability"]["parameters"];
        private string Kind(Tower u)=>S(u.profile["ability"],"kind");
        public void Tick(float dt)
        {
            if(!Running||Finished)return;dt=Mathf.Clamp(dt,0,.1f);Time+=dt;
            foreach(var f in Flashes)f.life-=dt;Flashes.RemoveAll(f=>f.life<=0);
            foreach(var u in Towers){u.attackBuff=u.hasteBuff=u.guardBuff=u.healBuff=0;if(u.shieldUntil<Time)u.shield=0;}
            foreach(var u in Towers.Where(t=>t.Alive))ApplyAuras(u);
            if(!waveActive){intermission-=dt;if(intermission<=0){if(Wave>=TotalWaves){Finish(true);return;}Wave++;remaining=5+Wave*2;spawned=0;spawnClock=0;waveActive=true;if(Wave==1)foreach(var u in Towers.Where(t=>t.Alive))if(Kind(u)=="court_start_buff_shield")StartRoyal(u);Changed?.Invoke();}}
            else if(remaining>0&&Enemies.Count(e=>e.Alive)<32){spawnClock-=dt;if(spawnClock<=0){Spawn();spawnClock=.65f;remaining--;}}
            foreach(var u in Towers.Where(t=>t.Alive).ToArray())TickTower(u,dt);
            foreach(var e in Enemies.Where(e=>e.Alive).ToArray())TickEnemy(e,dt);
            foreach(var shot in Shots.ToArray())TickShot(shot,dt);
            foreach(var e in Enemies.Where(e=>e.Alive).ToArray())TickMarks(e,dt);
            foreach(var e in Enemies.Where(e=>e.hp<=0&&e.hp!=float.MinValue).ToArray())Kill(e,null);
            if(waveActive&&remaining==0&&Enemies.All(e=>!e.Alive)){waveActive=false;intermission=2;Cash+=40;Changed?.Invoke();}
            Enemies.RemoveAll(e=>!e.Alive);if(Lives<=0)Finish(false);
        }
        private void Spawn(){bool boss=Wave%5==0&&spawned==0;float hp=42*Mathf.Pow(1.25f,Wave-1)*(boss?5:1);Enemies.Add(new Enemy{serial=++serial,monsterId="E"+((serial-1)%14+1).ToString("00"),boss=boss,maxHp=hp,hp=hp,attack=7+Wave*2,speed=boss?.48f:.65f+Wave*.025f,position=Route[0]});spawned++;}
        private void Finish(bool win){Finished=true;Won=win;Running=false;Changed?.Invoke();}
        private Vector2 RoutePosition(float distance,out bool exit){for(int i=1;i<Route.Length;i++){float d=Vector2.Distance(Route[i-1],Route[i]);if(distance<=d){exit=false;return Vector2.Lerp(Route[i-1],Route[i],distance/d);}distance-=d;}exit=true;return Route[Route.Length-1];}
        private void TickEnemy(Enemy e,float dt)
        {
            e.blocker=null;
            foreach(var u in Towers.Where(t=>t.Alive&&N(t.profile["placement"],"blockCount")>0).OrderBy(t=>Vector2.Distance(t.position,e.position))){int cap=(int)N(u.profile["placement"],"blockCount");if(Vector2.Distance(u.position,e.position)<=1.05f&&Enemies.Count(other=>other!=e&&other.Alive&&other.blocker==u)<cap){e.blocker=u;break;}}
            float slow=Time<e.slowUntil?Mathf.Min(e.slow,e.boss?.20f:.60f):0;
            if(e.blocker==null&&Time>=e.rootUntil){e.progress+=e.speed*(1-slow)*dt;e.position=RoutePosition(e.progress,out bool exit);if(exit){Lives-=e.boss?5:1;e.hp=float.MinValue;Changed?.Invoke();return;}}
            e.cooldown-=dt;var target=e.blocker;if(target==null&&e.serial%5==0)target=Towers.Where(t=>t.Alive&&Vector2.Distance(t.position,e.position)<2.5f).OrderBy(t=>Vector2.Distance(t.position,e.position)).FirstOrDefault();if(target!=null&&e.cooldown<=0){e.cooldown=1.1f;Hurt(target,e.attack,e);}
        }
        private IEnumerable<Tower> Allies(Tower u,float radius=-1,bool front=false){bool global=GameContent.Flag(u.profile["ability"],"globalTargets");float r=radius>=0?radius:SupportRange(u);return Towers.Where(t=>t.Alive&&(global||Vector2.Distance(t.position,u.position)<=r)&&(!front||t.position.y<u.position.y)).OrderBy(t=>t.hp/t.maxHp).ThenBy(t=>t.index);}
        private List<Enemy> Near(Vector2 origin,float radius)=>Enemies.Where(e=>e.Alive&&Vector2.Distance(e.position,origin)<=radius).OrderBy(e=>Vector2.Distance(e.position,origin)).ThenBy(e=>e.serial).ToList();
        private Enemy Select(Tower u)
        {
            var c=Enemies.Where(e=>e.Alive&&Vector2.Distance(e.position,u.position)<=AttackRange(u)&&Vector2.Distance(e.position,u.position)>=N(u.profile["attack"],"minimumRangeTiles")).ToList();
            if(u.targetMode==1)return c.OrderByDescending(e=>e.progress).ThenBy(e=>e.serial).FirstOrDefault();
            if(u.targetMode==2)return c.OrderByDescending(e=>e.hp).ThenByDescending(e=>e.progress).FirstOrDefault();
            if(u.targetMode==3)return c.OrderBy(e=>Vector2.Distance(e.position,u.position)).ThenBy(e=>e.serial).FirstOrDefault();
            switch(S(u.profile["attack"],"targetPriority")){
                case "blocked_self":if(c.Any(e=>e.blocker==u))c=c.Where(e=>e.blocker==u).ToList();break;
                case "blocked_ally":if(c.Any(e=>e.blocker!=null))c=c.Where(e=>e.blocker!=null).ToList();break;
                case "unblocked_first":if(c.Any(e=>e.blocker==null))c=c.Where(e=>e.blocker==null).ToList();break;
                case "unmarked_first":if(c.Any(e=>!e.marks.ContainsKey(u)))c=c.Where(e=>!e.marks.ContainsKey(u)).ToList();break;
                case "slowed_unmarked_first":case "slowed_first":if(c.Any(e=>e.slowUntil>Time))c=c.Where(e=>e.slowUntil>Time).ToList();break;
                case "weak_first":if(c.Any(e=>database.GetElementDamageMultiplier(u.id,e.monsterId)>1))c=c.Where(e=>database.GetElementDamageMultiplier(u.id,e.monsterId)>1).ToList();break;
                case "cluster":return c.OrderByDescending(e=>Near(e.position,Mathf.Max(.8f,N(u.profile["attack"],"splashRadiusTiles"))).Count).ThenByDescending(e=>e.progress).FirstOrDefault();
                case "strongest_current_hp":return c.OrderByDescending(e=>e.hp).ThenByDescending(e=>e.progress).FirstOrDefault();
                case "strongest_max_hp":return c.OrderByDescending(e=>e.maxHp).ThenByDescending(e=>e.progress).FirstOrDefault();
                case "lowest_hp_blocked":return c.OrderBy(e=>e.blocker!=null?0:1).ThenBy(e=>e.hp/e.maxHp).FirstOrDefault();
                case "closest":return c.OrderBy(e=>Vector2.Distance(e.position,u.position)).FirstOrDefault();
            }return c.OrderByDescending(e=>e.progress).ThenBy(e=>e.serial).FirstOrDefault();
        }
        private void TickTower(Tower u,float dt)
        {
            if(u.regenUntil>Time)Heal(u,u.regen*dt,u);u.cooldown-=dt;u.abilityClock-=dt;u.reactClock-=dt;Periodic(u,dt);
            if(!GameContent.Flag(u.profile["attack"],"enabled")&&!HasOffense(u))return;
            if(u.charge>0){u.charge-=dt;if(u.charge<=0){if(u.target!=null&&u.target.Alive&&Vector2.Distance(u.position,u.target.position)<=AttackRange(u)){Fire(u,u.target);float interval=N(u.profile["attack"],"intervalSec",1.5f);if(interval<=0)interval=1.5f;u.cooldown=Mathf.Max(.2f,interval/(1+Trait(u,"tempo")*.12f+u.hasteBuff)-N(u.profile["attack"],"windupSec"));}else u.cooldown=.1f;}}
            else if(u.cooldown<=0){var next=Select(u);if(next==null)return;if(next!=u.target)u.focus=0;u.target=next;u.charge=Mathf.Max(.05f,N(u.profile["attack"],"windupSec",.2f)/(1+Trait(u,"tempo")*.12f));if(Kind(u)=="windup_shield_shot")Shield(u,Power(u)*.6f,u.charge+.1f);}
        }
        private void Fire(Tower u,Enemy target)
        {
            u.attacks++;u.focus=Mathf.Min(5,u.focus+1);u.chargeCount++;string mode=S(u.profile["attack"],"mode");if(mode=="none")mode="orb";if(Trait(u,"pierce")>0&&(mode=="arrow"||mode=="bolt"||mode=="orb"))mode="line_projectile";
            float damage=Power(u)*(1+Trait(u,"single")*.20f+Trait(u,"burst")*.20f+Trait(u,"area")*.15f+u.attackBuff);
            if(Kind(u)=="focus_stack"||Kind(u)=="stationary_damage_stack")damage*=1+u.focus*.07f;
            if(Kind(u)=="weakness_next_attack"&&u.clocks.TryGetValue("charged",out float charged)&&charged>Time){damage*=1.35f;u.clocks["charged"]=0;}
            var a=u.profile["attack"];float radius=N(a,"splashRadiusTiles")*(1+Trait(u,"area")*.08f);int hits=Mathf.Max(1,(int)N(a,"maxDistinctHits",1))+(int)Trait(u,"pierce");if(Kind(u)=="shield_conditional_pierce")hits=u.shield>0?5:2;
            if(mode=="melee"||mode=="hitscan"){Hit(u,target,damage,false);Beam(u.position,target.position,u);}
            else if(mode=="cone"||mode=="line_melee"){var dir=(target.position-u.position).normalized;foreach(var e in Near(u.position,AttackRange(u)).Where(e=>Vector2.Angle(dir,e.position-u.position)<(mode=="cone"?45:12)).Take(hits).ToArray())Hit(u,e,damage,false);Beam(u.position,target.position,u);}
            else if(mode=="melee_splash"||mode=="ground_burst"){foreach(var e in Near(target.position,Mathf.Max(radius,.8f)).Take(hits).ToArray())Hit(u,e,damage,false);Beam(u.position,target.position,u);}
            else if(Shots.Count<96){Shots.Add(new Shot{owner=u,target=target,position=u.position,destination=target.position,direction=(target.position-u.position).normalized,speed=Mathf.Max(1,N(a["projectile"],"speedTilesPerSec",7)),damage=damage,range=AttackRange(u)*1.25f,radius=radius,width=.25f,remaining=hits,homing=mode=="arrow"||mode=="bolt"||mode=="orb"||mode=="orb_splash",mode=mode});}
            int count=4;foreach(var buff in Towers.Where(t=>t.Alive&&Kind(t)=="attack_count_aura"&&Vector2.Distance(t.position,u.position)<=SupportRange(t)))count=Mathf.Max(2,count-1);
            if(Kind(u)=="four_attack_heal_haste"&&u.attacks%count==0){foreach(var t in Allies(u).Take(3))Heal(t,Power(u)*.25f,u);u.clocks["haste"]=Time+2;}
            if(Kind(u)=="four_attack_chain"&&u.attacks%count==0)foreach(var e in Near(target.position,1.5f).Where(e=>e!=target).Take(4).ToArray())Hit(u,e,Power(u)*.45f,true);
            foreach(var support in Towers.Where(t=>t.Alive&&Vector2.Distance(t.position,u.position)<=SupportRange(t)).ToArray()){
                if(Kind(support)=="four_attack_next_skill"&&u.attacks%count==0)u.clocks["skillBuff"]=Time+6;
                if(Kind(support)=="buffed_ranged_chain"&&GameContent.Flag(u.profile["attack"],"countsAsRangedAttack")&&u.attackBuff+u.hasteBuff>0&&u.attacks%count==0)foreach(var e in Near(target.position,1.8f).Take(4).ToArray())Hit(support,e,Power(support)*.35f,true);
                if(Kind(support)=="arcane_ally_link_blast"&&support.reactClock<=0&&u.definition.themeTags.Contains("비전")&&u!=support){support.reactClock=.8f;foreach(var e in Near(target.position,.8f).Take(5).ToArray())Hit(support,e,Power(support)*.4f,true);}
            }
            float extras=Trait(u,"multi")+Trait(u,"chain");if(extras>0)foreach(var e in Near(target.position,2).Where(e=>e!=target).Take(Mathf.CeilToInt(extras)).ToArray())Hit(u,e,damage*.30f,true);
            if(mode=="melee"&&Trait(u,"area")>0)foreach(var e in Near(target.position,.65f+Trait(u,"area")*.08f).Where(e=>e!=target).Take(4).ToArray())Hit(u,e,damage*.4f,true);
        }
        private void TickShot(Shot shot,float dt)
        {
            if(shot.homing&&(shot.target==null||!shot.target.Alive)){Shots.Remove(shot);return;}Vector2 before=shot.position;if(shot.homing)shot.direction=(shot.target.position-shot.position).normalized;
            float distance=shot.speed*dt;shot.position+=shot.direction*distance;shot.travel+=distance;
            if(shot.mode=="lob_splash"&&Vector2.Distance(shot.position,shot.destination)<distance+.15f){foreach(var e in Near(shot.destination,Mathf.Max(.8f,shot.radius)).Take(shot.remaining).ToArray())Hit(shot.owner,e,shot.damage,false);Beam(before,shot.destination,shot.owner);Shots.Remove(shot);return;}
            var struck=Enemies.Where(e=>e.Alive&&!shot.hit.Contains(e.serial)&&SegmentDistance(e.position,before,shot.position)<shot.width).OrderBy(e=>Vector2.Distance(before,e.position)).ToArray();
            foreach(var e in struck){if(shot.mode=="lob_splash")continue;shot.hit.Add(e.serial);
                if(shot.mode=="orb_splash"||shot.mode=="impact_cone"){var group=Near(e.position,Mathf.Max(.8f,shot.radius));if(shot.mode=="impact_cone")group=group.Where(other=>other==e||Vector2.Angle(shot.direction,other.position-e.position)<30).ToList();foreach(var victim in group.Take(shot.remaining).ToArray())Hit(shot.owner,victim,shot.damage,false);shot.remaining=0;}
                else{float amp=Kind(shot.owner)=="growing_pierce_damage"?1+Mathf.Min(.5f,(shot.hit.Count-1)*.1f):1;Hit(shot.owner,e,shot.damage*amp,false);shot.remaining--;}
                Beam(before,e.position,shot.owner);if(shot.mode!="line_projectile")shot.remaining=0;if(shot.remaining<=0)break;
            }
            if(shot.remaining<=0||shot.travel>=shot.range)Shots.Remove(shot);
        }
        private static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b){Vector2 ab=b-a;float t=ab.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude):0;return Vector2.Distance(p,a+ab*t);}
        private void Hit(Tower u,Enemy e,float raw,bool proc)
        {
            if(!e.Alive)return;float element=database.GetElementDamageMultiplier(u.id,e.monsterId);float damage=raw*element*(element>1?1+Trait(u,"weakness")*.12f:1)*(e.markUntil>Time?1+e.markBonus:1);
            if(!proc&&u.clocks.TryGetValue("skillBuff",out float skill)&&skill>Time){damage*=1.25f;u.clocks["skillBuff"]=0;}
            if(!proc&&Kind(u)=="wounded_blocked_blast"&&e.blocker!=null&&e.hp/e.maxHp<.3f)damage*=1.25f;
            if(!proc&&Kind(u)=="arcane_mark_blast"&&e.marks.Any(m=>m.Value.kind=="blocked_arcane_mark"))damage*=1.3f;
            if(!proc&&Kind(u)=="debuff_count_blast")damage*=1+Mathf.Min(3,e.marks.Count+(e.slowUntil>Time?1:0))*.1f;
            e.hp-=damage;
            if(!proc){ApplyHitAbility(u,e);
                if(Trait(u,"dot")>0)MarkEnemy(u,e,"upgrade_dot",3,Power(u)*.12f*Trait(u,"dot"));
                if(Trait(u,"control")>0)Slow(e,.08f*Trait(u,"control"),2);
                if(Trait(u,"mark")>0){e.markBonus=Mathf.Max(e.markBonus,.04f*Trait(u,"mark"));e.markUntil=Time+5;}
                foreach(var support in Towers.Where(t=>t.Alive&&Vector2.Distance(t.position,u.position)<=SupportRange(t)).ToArray()){
                    if(Kind(support)=="ally_hit_burn_aura")MarkEnemy(support,e,"burn",3,Power(support)*.12f);
                    if(element>1&&Kind(support)=="weakness_bonus_aura"&&support.reactClock<=0){support.reactClock=.5f;e.hp-=Power(support)*.2f;}
                    if(element>1&&Kind(support)=="blocked_weakness_counter"&&e.blocker==support&&support.reactClock<=0){support.reactClock=.6f;e.hp-=Power(support)*.5f;}
                    if(Kind(support)=="ally_hit_charge_counter"&&e.blocker==support&&support.reactClock<=0){support.chargeCount++;support.reactClock=.3f;if(support.chargeCount%4==0)e.hp-=Power(support)*.6f;}
                }
                foreach(var m in e.marks.ToArray())if(m.Key.Alive&&Vector2.Distance(m.Key.position,u.position)<=SupportRange(m.Key)){
                    if(m.Value.kind=="lifesteal_mark"&&Ready(m.Key,"life"+u.index,1))Heal(u,Power(m.Key)*.2f,m.Key);
                    if(m.Value.kind=="direct_damage_heal_link"&&Ready(m.Key,"drain"+u.index,.5f)){var ally=Allies(m.Key).FirstOrDefault(t=>t.hp<t.maxHp);if(ally!=null)Heal(ally,Mathf.Min(damage*.08f,Power(m.Key)*.5f),m.Key);}
                    if(m.Value.kind=="mark_attack_regen"){u.regenUntil=Time+3;u.regen=Power(m.Key)*.12f;}
                }
            }
            if(e.hp<=0)Kill(e,u,!proc);
        }
        private void ApplyHitAbility(Tower u,Enemy e)
        {
            string kind=Kind(u);
            if(kind=="element_vulnerability"||kind=="ranged_focus_mark"){e.markBonus=Mathf.Max(e.markBonus,kind=="ranged_focus_mark"?.15f:.12f);e.markUntil=Time+5;}
            if(kind=="knockback"&&!e.boss&&Ready(u,"push"+e.serial,2)){e.progress=Mathf.Max(0,e.progress-.5f);e.position=RoutePosition(e.progress,out _);e.blocker=null;}
            if(kind=="slow_stack")Slow(e,Mathf.Min(.4f,e.slow+.1f),3);
            if(kind=="pierce_slow_root"){if(e.slowUntil>Time&&!e.boss&&Time>=e.rootImmune){e.rootUntil=Time+.6f;e.rootImmune=Time+4;}Slow(e,.25f,2);}
            if(kind=="line_lightning_stagger"&&!e.boss&&Time>=e.rootImmune){e.rootUntil=Time+.3f;e.rootImmune=Time+3;}
            if(kind=="heal_charged_slow"&&u.clocks.TryGetValue("healCharge",out float charged)&&charged>Time){foreach(var other in Near(e.position,1))Slow(other,.25f,2);u.clocks["healCharge"]=0;}
            if(kind=="weakness_next_attack"&&database.GetElementDamageMultiplier(u.id,e.monsterId)>1&&Ready(u,"weakCharge",.2f))u.clocks["charged"]=Time+5;
            if(kind=="delayed_fissure")foreach(var other in Near(e.position,1.2f).Where(x=>x!=e).Take(6))other.hp-=Power(u)*.6f*database.GetElementDamageMultiplier(u.id,other.monsterId);
            if(new[]{"lifesteal_mark","trail_heal_mark","death_heal_blast","mark_attack_regen","slowed_mark_death_heal","direct_damage_heal_link","marked_death_heal","dot_death_transfer"}.Contains(kind))if(kind!="slowed_mark_death_heal"||e.slowUntil>Time)MarkEnemy(u,e,kind,kind=="dot_death_transfer"?6:5,kind=="dot_death_transfer"?Power(u)*.22f:0);
        }
        private void MarkEnemy(Tower u,Enemy e,string kind,float duration,float damage)
        {
            if(e.marks.TryGetValue(u,out var m)){m.expires=Time+duration;m.damage=damage;m.kind=kind;return;}
            int cap=(int)N(Params(u),"maxConcurrentMarks",3);var old=Enemies.Where(x=>x.marks.ContainsKey(u)).OrderBy(x=>x.marks[u].expires).ToArray();if(old.Length>=cap)old[0].marks.Remove(u);e.marks[u]=new Mark{kind=kind,expires=Time+duration,tick=1,damage=damage};
        }
        private void TickMarks(Enemy e,float dt)
        {
            foreach(var kv in e.marks.ToArray()){
                var m=kv.Value;if(m.expires<Time){e.marks.Remove(kv.Key);continue;}m.tick-=dt;if(m.tick<=0){m.tick=1;if(m.damage>0)Hit(kv.Key,e,m.damage,true);if(m.kind=="trail_heal_mark"&&kv.Key.Alive)foreach(var a in Towers.Where(t=>t.Alive&&Vector2.Distance(t.position,e.position)<=.6f).Take(3))Heal(a,Power(kv.Key)*.1f,kv.Key);}
            }
        }
        private void Kill(Enemy e,Tower killer,bool allowDeathProcs=true)
        {
            if(e.hp==float.MinValue)return;e.hp=float.MinValue;Cash+=e.boss?80:15;Kills++;
            foreach(var kv in e.marks.ToArray()){
                var u=kv.Key;var m=kv.Value;if(!u.Alive||!allowDeathProcs)continue;
                if(m.kind=="death_heal_blast"){foreach(var t in Allies(u).Take(4))Heal(t,Power(u)*.4f,u);foreach(var other in Near(e.position,1.2f).Where(x=>x!=e).Take(8).ToArray())Hit(u,other,Power(u)*.6f,true);}
                if(m.kind=="marked_death_heal"||m.kind=="slowed_mark_death_heal")foreach(var t in Allies(u).Where(t=>t.hp<t.maxHp).Take(m.kind=="marked_death_heal"?1:4))Heal(t,Power(u)*.6f,u);
                if(m.kind=="dot_death_transfer"&&m.transfers<3){var next=Near(e.position,2).FirstOrDefault(x=>x!=e);if(next!=null)next.marks[u]=new Mark{kind=m.kind,expires=m.expires,damage=m.damage,tick=m.tick,transfers=m.transfers+1};}
            }
            foreach(var u in Towers.Where(t=>t.Alive&&Kind(t)=="blocked_death_self_heal"&&e.blocker==t))Heal(u,u.maxHp*.08f,u);Changed?.Invoke();
        }
        private void ApplyAuras(Tower u)
        {
            string kind=Kind(u);
            foreach(var a in Allies(u)){
                bool ranged=GameContent.Flag(a.profile["attack"],"countsAsRangedAttack"),court=a.definition.themeTags.Contains("궁정");
                if(kind=="attack_aura")a.attackBuff=Mathf.Max(a.attackBuff,.12f);
                if(kind=="ranged_speed_aura"&&ranged)a.hasteBuff=Mathf.Max(a.hasteBuff,.12f);
                if(kind=="healing_aura_with_pulse")a.healBuff=Mathf.Max(a.healBuff,.15f);
                if(kind=="court_shield_heal_aura"&&court&&a.shield>0)a.healBuff=Mathf.Max(a.healBuff,.15f);
                if(kind=="nature_marked_damage_aura"&&a.definition.themeTags.Contains("자연"))a.attackBuff=Mathf.Max(a.attackBuff,.15f);
                if((kind=="front_defense_block_aura"||kind=="front_defense_rear_damage")&&a.position.y<u.position.y)a.guardBuff=Mathf.Max(a.guardBuff,.12f);
                if(kind=="front_defense_rear_damage"&&a.position.y>u.position.y&&ranged)a.attackBuff=Mathf.Max(a.attackBuff,.15f);
                if(kind=="front_shield_rear_haste"&&a.position.y>u.position.y&&ranged)a.hasteBuff=Mathf.Max(a.hasteBuff,.15f);
                if(!GameContent.Flag(u.profile["attack"],"enabled")&&N(u.profile["ability"],"intervalSec")==0)a.hasteBuff=Mathf.Max(a.hasteBuff,Trait(u,"tempo")*.12f);a.attackBuff=Mathf.Max(a.attackBuff,Trait(u,"aura")*.04f);a.guardBuff=Mathf.Max(a.guardBuff,Trait(u,"guard")*.03f);
            }
            if(kind=="single_ranged_buff"){var t=Allies(u).Where(a=>GameContent.Flag(a.profile["attack"],"countsAsRangedAttack")).OrderBy(a=>Vector2.Distance(u.position,a.position)).FirstOrDefault();if(t!=null){t.hasteBuff=Mathf.Max(t.hasteBuff,.2f);t.attackBuff=Mathf.Max(t.attackBuff,.1f);}}
            if(u.clocks.TryGetValue("haste",out float haste)&&haste>Time)u.hasteBuff=Mathf.Max(u.hasteBuff,.15f);
            if(u.clocks.TryGetValue("royal",out float royal)&&royal>Time)u.attackBuff=Mathf.Max(u.attackBuff,.15f);
            if(kind=="blocked_element_vulnerability")foreach(var e in Enemies.Where(e=>e.Alive&&e.blocker==u)){e.markBonus=Mathf.Max(e.markBonus,.15f);e.markUntil=Time+.25f;}
            if(kind=="blocked_arcane_mark")foreach(var e in Enemies.Where(e=>e.Alive&&e.blocker==u))MarkEnemy(u,e,kind,2,0);
        }
        private void StartRoyal(Tower u){foreach(var t in Towers.Where(t=>t.Alive&&t.definition.themeTags.Contains("궁정"))){t.clocks["royal"]=Time+8;Shield(t,Power(u),8);}}
        private void Periodic(Tower u,float dt)
        {
            string kind=Kind(u);var p=Params(u);var ability=u.profile["ability"];int cap=Mathf.Max(1,(int)N(ability,"maxDistinctAllyTargetsPerProc",1))+(int)Trait(u,"link");
            if(kind=="block_heat_blast"){bool blocking=Enemies.Any(e=>e.Alive&&e.blocker==u);u.heat=Mathf.Clamp(u.heat+(blocking?dt:-dt),0,5);if(u.heat>=5){u.heat=0;foreach(var e in Near(u.position,1.5f).Take(8).ToArray())Hit(u,e,Power(u)*.8f,true);}}
            if(Trait(u,"heal")>0&&Ready(u,"upgradeHeal",3))foreach(var a in Allies(u).Take(2+(int)Trait(u,"link")))Heal(a,Power(u)*.15f*Trait(u,"heal"),u);
            if(Trait(u,"regen")>0)foreach(var a in Allies(u).Take(3+(int)Trait(u,"link")))Heal(a,Power(u)*.08f*Trait(u,"regen")*dt,u);
            if(Trait(u,"shield")>0&&Ready(u,"upgradeShield",4))foreach(var a in Allies(u).Take(3+(int)Trait(u,"link")))Shield(a,Power(u)*.18f*Trait(u,"shield"),5);
            if(Trait(u,"cleanse")>0&&Ready(u,"upgradeCleanse",3))foreach(var a in Allies(u).Take(2))Shield(a,Power(u)*.1f*Trait(u,"cleanse"),3);
            if(Trait(u,"control")>0&&!GameContent.Flag(u.profile["attack"],"enabled"))foreach(var e in Near(u.position,Mathf.Max(1.5f,SupportRange(u))))Slow(e,.08f*Trait(u,"control"),.3f);
            if(u.abilityClock>0)return;float interval=N(ability,"intervalSec");if(interval<=0)return;u.abilityClock=Mathf.Max(.2f,interval/(1+Trait(u,"tempo")*.12f));
            var targets=Allies(u).ToList();if(kind.StartsWith("front_"))targets=targets.Where(t=>t.position.y<u.position.y).ToList();
            if(kind=="self_ally_heal")targets=new[]{u}.Concat(targets.Where(t=>t!=u).Take(1)).ToList();
            if(kind=="global_heal")targets=Towers.Where(t=>t.Alive).ToList();
            if(kind=="court_shield_heal_aura")targets=targets.Where(t=>t.definition.themeTags.Contains("궁정")).ToList();
            if(kind=="front_shield_rear_haste")targets=targets.Where(t=>t.position.y<u.position.y).ToList();
            if(kind=="slow_heal_zone"||kind=="fixed_heal_slow_zone"){
                var center=Select(u)?.position??u.position;float r=N(ability,"effectRadiusTiles",1.2f);foreach(var e in Near(center,r))Slow(e,.2f,interval);foreach(var a in Towers.Where(t=>t.Alive&&Vector2.Distance(t.position,center)<=r).Take(cap))Heal(a,Power(u)*.5f,u);return;
            }
            if(kind=="blocked_fire_pulse"){foreach(var e in Enemies.Where(e=>e.Alive&&e.blocker!=null&&Vector2.Distance(e.blocker.position,u.position)<=SupportRange(u)).Take(6).ToArray())Hit(u,e,Power(u)*.4f,true);return;}
            if(kind=="self_heal_zone"){foreach(var a in targets.Take(cap))Heal(a,Power(u)*.18f,u);return;}
            if(kind=="buff_extension"){foreach(var a in targets.Take(cap))if(a.shield>0)a.shieldUntil=Mathf.Min(Time+8,a.shieldUntil+1);return;}
            foreach(var a in (kind=="global_heal"?targets:targets.Take(cap))){
                if(kind=="heal"||kind=="global_heal"||kind=="self_ally_heal"||kind=="heal_cleanse"||kind=="heal_cleanse_retaliate"||kind=="front_heal_shield"||kind=="front_heal_blocked_damage"||kind=="front_enemy_heal_link"||kind=="slow_count_heal"||kind=="healing_aura_with_pulse"){
                    float amount=Power(u)*N(p,"healAttackCoefficient",.5f);if(kind=="slow_count_heal")amount*=1+Mathf.Min(6,Enemies.Count(e=>e.Alive&&e.slowUntil>Time&&Vector2.Distance(e.position,u.position)<=SupportRange(u)))*.1f;Heal(a,amount,u);
                    if(kind=="front_heal_blocked_damage"||kind=="front_enemy_heal_link")foreach(var e in Enemies.Where(e=>e.Alive&&e.blocker==a).Take(4).ToArray())Hit(u,e,Power(u)*.4f,true);
                }
                if(kind=="heal_over_time"){a.regenUntil=Time+4;a.regen=Power(u)*.3f;}
                if(kind.Contains("shield")||kind=="front_cleanse_shield_blast")Shield(a,Power(u)*N(p,"shieldAttackCoefficient",.8f),N(p,"shieldDurationSec",5));
                if(kind=="cleanse_next_element_attack"){a.clocks["skillBuff"]=Time+5;Shield(a,Power(u)*.2f,3);}
            }
        }
        private void Heal(Tower target,float amount,Tower source)
        {
            if(!target.Alive||amount<=0)return;amount*=1+target.healBuff+Trait(source,"heal")*.18f+Trait(source,"link")*.08f;float excess=Mathf.Max(0,target.hp+amount-target.maxHp);float actual=Mathf.Min(amount,target.maxHp-target.hp);target.hp=Mathf.Min(target.maxHp,target.hp+amount);
            if(Kind(target)=="heal_charged_slow"&&actual>0)target.clocks["healCharge"]=Time+5;
            if(excess>0)foreach(var support in Towers.Where(t=>t.Alive&&Kind(t)=="overheal_to_shield"&&Vector2.Distance(t.position,target.position)<=SupportRange(t)))Shield(target,Mathf.Min(excess*.35f,target.maxHp*.25f),5);
        }
        private void Shield(Tower target,float amount,float duration){if(!target.Alive)return;target.shield=Mathf.Min(target.maxHp*.5f,Mathf.Max(target.shield,amount));target.shieldUntil=Mathf.Max(target.shieldUntil,Time+duration);}
        private void Slow(Enemy e,float strength,float duration){e.slow=Mathf.Max(e.slowUntil>Time?e.slow:0,Mathf.Min(strength,e.boss?.2f:.6f));e.slowUntil=Time+duration;}
        private bool Ready(Tower u,string key,float delay){if(u.clocks.TryGetValue(key,out float until)&&until>Time)return false;u.clocks[key]=Time+delay;return true;}
        private void Hurt(Tower u,float amount,Enemy attacker,bool transferred=false)
        {
            if(!u.Alive)return;float guard=u.guardBuff+Trait(u,"tank")*.03f;string kind=Kind(u);
            if(kind=="front_damage_reduction"||kind=="block_damage_reduction")guard+=.2f;if(kind=="block_count_damage_reduction")guard+=Mathf.Min(.24f,Enemies.Count(e=>e.Alive&&e.blocker==u)*.06f);
            if(u.clocks.TryGetValue("crisisGuard",out float until)&&until>Time)guard+=.25f;amount*=1-Mathf.Min(.75f,guard);
            if(!transferred){var escort=Towers.FirstOrDefault(t=>t!=u&&t.Alive&&Kind(t)=="rear_damage_transfer"&&u.position.y>t.position.y&&Vector2.Distance(t.position,u.position)<=SupportRange(t));if(escort!=null){float part=amount*.15f;Hurt(escort,part,attacker,true);amount-=part;}}
            bool broken=u.shield>0&&amount>=u.shield;if(u.shield>0){float blocked=Mathf.Min(amount,u.shield);u.shield-=blocked;amount-=blocked;}u.hp=Mathf.Max(0,u.hp-amount);
            if(!u.Alive){u.shield=0;Changed?.Invoke();return;}
            if(!transferred&&Trait(u,"counter")>0&&Ready(u,"counter",.5f))Hit(u,attacker,Power(u)*.15f*Trait(u,"counter"),true);
            foreach(var support in Towers.Where(t=>t.Alive&&Vector2.Distance(t.position,u.position)<=SupportRange(t)).ToArray()){
                string sk=Kind(support);
                if(sk=="shield_attacker_counter"&&u.shield>0&&Ready(support,"shieldCounter"+u.index,1))Hit(support,attacker,Power(support)*.4f,true);
                if(sk=="front_ally_retaliation"&&u.position.y<support.position.y&&Ready(support,"retaliate",.8f))Hit(support,attacker,Power(support)*.5f,true);
                if(broken&&sk=="shield_break_heal"&&Ready(support,"shieldHeal"+u.index,4))Heal(u,Power(support)*.6f,support);
                if(broken&&sk=="shield_break_slow")foreach(var e in Near(u.position,1))Slow(e,.3f,2);
                if(sk=="crisis_shield_regen"&&u.hp/u.maxHp<.35f&&Ready(support,"crisis"+u.index,10)){Shield(u,Power(support),4);u.regenUntil=Time+4;u.regen=Power(support)*.2f;}
            }
            if(kind=="self_crisis_heal_guard"&&u.hp/u.maxHp<.3f&&Ready(u,"selfCrisis",12)){Heal(u,u.maxHp*.15f,u);u.clocks["crisisGuard"]=Time+3;}
            if(kind=="blocked_charge_unblocked_shot"&&Ready(u,"unblockedCounter",1.5f)&&Enemies.Any(e=>e.Alive&&e.blocker==u)){var e=Near(u.position,AttackRange(u)).FirstOrDefault(x=>x.blocker==null);if(e!=null)Hit(u,e,Power(u)*.5f,true);}
        }
        private void Beam(Vector2 from,Vector2 to,Tower u){if(Flashes.Count<64)Flashes.Add(new Flash{from=from,to=to,life=.15f,color=MainCraftingScreen.TierColor(u.tier)});}
    }
}



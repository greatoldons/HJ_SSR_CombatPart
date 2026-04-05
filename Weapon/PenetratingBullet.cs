using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Noise;
using Verse.Sound;

namespace HJ_SSR.Weapons
{
    public class PenetratingBullet : Bullet
    {


        // 获取武器的伤害倍数（从 Thing 获取 StatDefOf.RangedWeapon_DamageMultiplier）
        float weaponDamageMultiplier => equipment?.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) ?? 1f;
        public float PenetratingPowerBase => (MaxPenetratingPower > 0f ? MaxPenetratingPower : penetratingPower) * PenetrationFloorPercentage;
        //public override float ArmorPenetration => Extension == null ? def.projectile.GetArmorPenetration(weaponDamageMultiplier) : def.projectile.GetArmorPenetration(weaponDamageMultiplier) * (penetratingPower / MaxPenetratingPower);
        public override float ArmorPenetration => Extension == null ? def.projectile.GetArmorPenetration(equipment) : (weaponDamageMultiplier * (penetratingPower / MaxPenetratingPower));
        public bool ShouldTerminate;

        Vector3 initialVector;

        Vector3 trueOrigin;
        readonly HashSet<IntVec3> checkedCells = new HashSet<IntVec3>();
        int dbgTickIntervals;
        int dbgPathChecks;
        int dbgStepSamples;
        int dbgUniqueCells;
        int dbgThingCandidates;
        int dbgCanHitPasses;
        int dbgDestroyedOutOfBounds;
        int dbgSubTicks;
        int dbgFriendlySkips;
        int dbgFilteredTransientSkips;
        string dbgDestroyReason = "unknown";
        const float SimpleHitCost = 0.05f;

        ModExtension_PenetratingProjectile Extension => def.GetModExtension<ModExtension_PenetratingProjectile>();

        //  穿透力 和  穿透消耗值
        public override int DamageAmount => ShouldTerminate ? def.projectile.GetDamageAmount(StopperDamageMulti,equipment) : def.projectile.GetDamageAmount(overpenDamageMulti, equipment);
        public float overpenDamageMulti => (Extension == null) ? 0.5f : Extension.overpenDamageMulti;
        public float StopperDamageMulti => (Extension == null) ? 1f : Extension.stopperDamageMulti;
        public float PenetrationFloorPercentage => (Extension == null) ? 0.2f : Extension.PenetrationFloorPercentage;
        public float BuildingEquivalentMulti => (Extension == null) ? 0.005f : Extension.buildingEquivalentMulti;
        public float BodysizeEquivalentMulti => (Extension == null) ? 0.1f : Extension.bodysizeEquivalentMulti;
        public float TreeEquivalent => (Extension == null) ? 0.1f : Extension.treeEquivalent;
        public float ChunkEquivalent => (Extension == null) ? 0.25f : Extension.chunkEquivalent;
        public float BounceEquivalent => (Extension == null) ? 0.2f : Extension.bounceEquivalent;
        public float ArmorEquivalentMulti => (Extension == null) ? 1f : Extension.armorEquivalentMulti;
        public float MinSearchRadius => (Extension == null) ? 5f : Extension.minSearchRadius;
        public float ExtraRange => (Extension == null) ? 5f : Extension.extraRange;
        public float MaxSearchRadius => (Extension == null) ? 20f : Extension.maxSearchRadius;
        public float PostPenetrationDeviationAngle => (Extension == null) ? 5f : Extension.postPenetrationDeviationAngle;

        public float MaxPenetratingPower = 0;

        public float ticksToDetonation = 100;

        public bool fuseActivated = false;
        public bool straightPassThrough => (Extension == null) ? false : Extension.straightPassThrough;
        public CompPostPenetrationExplosive compExplosive => this.TryGetComp<CompPostPenetrationExplosive>();

        public int maxPenetratingCount => (Extension == null) ? -1 :Extension.maxPenetratingCount;

        public int PenetratingCount = -1;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (def.projectile.damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp)
            {
                penetratingPower = def.projectile.GetArmorPenetration(equipment);
                if (Extension != null)
                {
                    penetratingPower *= Extension.penetrationPotentialMultiplier;
                }
            }
            MaxPenetratingPower = penetratingPower;
            if (compExplosive != null)
            {
                ticksToDetonation = compExplosive.Props.wickTicks.RandomInRange;
            }
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget,
                           LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags,
                           bool preventFriendlyFire = false, Thing equipment = null,
                           ThingDef targetCoverDef = null)
        {
            // 1. 调用基类初始化
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags,
                       preventFriendlyFire, equipment, targetCoverDef);

            // 2. 记录真实起点
            trueOrigin = origin;

            // 3. 计算初始方向向量（单位化）
            initialVector = (usedTarget.CenterVector3 - origin).normalized;

            // 4. 计算最大射程（武器射程 + 额外射程）
            float maxRange = equipmentDef.Verbs[0].range + Extension?.extraRange ?? 0;

            // 5. 计算射程尽头的目标位置
            Vector3 maxRangeDestination = origin + initialVector * maxRange;

            // 6. 修正地图边界
            IntVec3 destCell = maxRangeDestination.ToIntVec3().ClampInsideMap(launcher.Map);
            destination = destCell.ToVector3Shifted();

            // 7. 计算初始飞行时间
            ticksToImpact = (int)(StartingTicksToImpact);

            // 8. 调试标记（可选）
            //if (DebugSettings.godMode)
            //{
            //    launcher.Map.debugDrawer.FlashLine(ExactPosition.ToIntVec3(), destination.ToIntVec3(), duration: 100, SimpleColor.Red);
            //}

            // 10. 最大穿透数更新
            PenetratingCount = maxPenetratingCount;

            dbgTickIntervals = 0;
            dbgPathChecks = 0;
            dbgStepSamples = 0;
            dbgUniqueCells = 0;
            dbgThingCandidates = 0;
            dbgCanHitPasses = 0;
            dbgDestroyedOutOfBounds = 0;
            dbgSubTicks = 0;
            dbgFriendlySkips = 0;
            dbgFilteredTransientSkips = 0;
            dbgDestroyReason = "alive";
        }

        protected override void Tick()
        {
            if (penetratingPower <= PenetratingPowerBase || Map == null)
            {
                dbgDestroyReason = $"penetration_end p:{penetratingPower:F3} base:{PenetratingPowerBase:F3} mapNull:{Map == null}";
                canDestroyNow = true;
                Destroy();
                return;
            }
            base.Tick();
            // 爆炸物逻辑
            if (compExplosive != null && fuseActivated)
            {
                ticksToDetonation--;
                if (ticksToDetonation < 0 || penetratingPower <= PenetratingPowerBase)
                {
                    compExplosive.Explode(this, this.Map, this.equipmentDef, this.intendedTarget.Thing);
                }
            }
        }

        protected override void TickInterval(int delta)
        {
            dbgTickIntervals++;
            foreach (ThingComp comp in AllComps)
            {
                comp.CompTickInterval(delta);
            }
            for (int i = 0; i < delta; i++)
            {
                dbgSubTicks++;
                lifetime--;
                if (landed || Destroyed)
                {
                    return;
                }

                Vector3 exactPosition = ExactPosition;
                ticksToImpact--;
                if (!ExactPosition.InBounds(Map))
                {
                    dbgDestroyedOutOfBounds++;
                    dbgDestroyReason = $"out_of_bounds exact:{ExactPosition} delta:{delta}";
                    ticksToImpact++;
                    Position = ExactPosition.ToIntVec3();
                    canDestroyNow = true;
                    Destroy();
                    return;
                }

                Vector3 exactPosition2 = ExactPosition;
                if (CheckForFreeInterceptBetween(exactPosition, exactPosition2))
                {
                    canDestroyNow = true;
                    Destroy();
                    return;
                }

                Position = ExactPosition.ToIntVec3();
                if (ticksToImpact <= 0)
                {
                    if (DestinationCell.InBounds(Map))
                    {
                        Position = DestinationCell;
                    }
                    ImpactSomething();
                    return;
                }
            }
        }

        /// <summary>在 last/new 两个精确坐标之间采样路径格，检查可命中对象。</summary>
        private bool CheckForFreeInterceptBetween(Vector3 lastExactPos, Vector3 newExactPos)
        {
            dbgPathChecks++;
            if (lastExactPos == newExactPos)
            {
                return false;
            }

            List<Thing> interceptors = Map.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor);
            for (int i = 0; i < interceptors.Count; i++)
            {
                if (interceptors[i].TryGetComp<CompProjectileInterceptor>().CheckIntercept(this, lastExactPos, newExactPos))
                {
                    Impact(null, blockedByShield: true);
                    return true;
                }
            }

            IntVec3 intVec = lastExactPos.ToIntVec3();
            IntVec3 intVec2 = newExactPos.ToIntVec3();
            if (intVec2 == intVec)
            {
                return false;
            }
            if (!intVec.InBounds(Map) || !intVec2.InBounds(Map))
            {
                return false;
            }
            if (intVec2.AdjacentToCardinal(intVec))
            {
                return CheckForFreeIntercept(intVec2);
            }

            // 正交移动：Bresenham 枚举线段上每一格，避免 0.2 步长 + floor(maxSteps) 漏掉中间墙格。
            // 必须复制 List：GenSight.BresenhamCellsBetween 返回的是内部复用列表，嵌套调用会被清空。
            if (intVec.x == intVec2.x || intVec.z == intVec2.z)
            {
                List<IntVec3> lineCells = new List<IntVec3>(GenSight.BresenhamCellsBetween(intVec, intVec2));
                for (int li = 0; li < lineCells.Count; li++)
                {
                    IntVec3 c = lineCells[li];
                    if (!c.InBounds(Map))
                    {
                        continue;
                    }
                    dbgStepSamples++;
                    dbgUniqueCells++;
                    if (CheckForFreeIntercept(c))
                    {
                        return true;
                    }
                }
                return false;
            }

            Vector3 vect = lastExactPos;
            Vector3 v = newExactPos - lastExactPos;
            float mag = v.MagnitudeHorizontal();
            if (mag < 1E-4f)
            {
                return false;
            }
            Vector3 step = v.normalized * 0.2f;
            // 向上取整并留余量，避免 (int)(mag/0.2) 偏小而提前结束、跳过终点前多格。
            int maxSteps = Mathf.CeilToInt(mag / 0.2f) + 2;
            checkedCells.Clear();
            int stepCount = 0;
            IntVec3 currentCell;
            do
            {
                vect += step;
                currentCell = vect.ToIntVec3();
                dbgStepSamples++;
                if (!checkedCells.Contains(currentCell))
                {
                    dbgUniqueCells++;
                    if (CheckForFreeIntercept(currentCell))
                    {
                        return true;
                    }
                    checkedCells.Add(currentCell);
                }
                stepCount++;
                if (stepCount > maxSteps)
                {
                    if (!checkedCells.Contains(intVec2) && CheckForFreeIntercept(intVec2))
                    {
                        return true;
                    }
                    return false;
                }
            }
            while (currentCell != intVec2);

            return false;
        }

        List<Thing> thingList = new List<Thing>();
        /// <summary>
        /// 计算子弹是否有击中，是否可以被拦截
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        /// <summary>处理单个格的命中采样：记录日志、应用伤害并扣减穿透。</summary>
        private bool CheckForFreeIntercept(IntVec3 c)
        {
            // TODO: 调试日志 除了调试以外不要开，很多
            // LogPathCell(c);
            thingList = c.GetThingList(base.Map);
            for (int i = 0; i < thingList.Count; i++)
            {
                Thing thing = thingList[i];
                dbgThingCandidates++;
                // 过滤
                if (thing is Projectile || thing is Mote || thing is Filth || thing is Gas)
                {
                    dbgFilteredTransientSkips++;
                    continue;
                }
                // Log.Message($"[PenetratingBullet] CheckForFreeIntercept: Checking thing {i}: {thing?.def?.defName ?? "null"} at {c}");
                if (!CanHit(thing))
                {
                    continue;
                }
                if (attackedThings.Contains(thing))
                {
                    continue;
                }
                if (IsFriendlyBuildingOrPawn(thing))
                {
                    dbgFriendlySkips++;
                    continue;
                }
                dbgCanHitPasses++;
                attackedThings.Add(thing);
                // LogHitCell(c, thing);
                ApplyHitDamage(thing, c);
                // 草类/小植物只做命中日志，不参与本阶段穿透消耗， 会被草  灰尘 气体等消耗，如果有问题check here
                if (thing is Plant plant && !IsTreePlant(plant))
                {
                    continue;
                }
                penetratingPower -= SimpleHitCost;
                if (penetratingPower <= PenetratingPowerBase)
                {
                    ShouldTerminate = true;
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 
        /// 击中后逻辑
        /// </summary>
        /// <param name="hitThing"></param>
        /// <param name="blockedByShield"></param>
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // 路径调试阶段：禁用真实命中与伤害结算，避免干扰轨迹验证。
            // 原版impact 似乎会干扰穿透命中的计算，总会跳cell计算
            landed = false;
        }

        protected override void ImpactSomething()
        {
            dbgDestroyReason = "impact_disabled_path_debug";
            canDestroyNow = true;
            Destroy();
        }
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (!canDestroyNow)
            {
                return;
            }
            base.Destroy(mode);
        }

        /// <summary>仅输出本帧路径检测实际遍历到的格子（不含因等于 destination 而跳过的格）。</summary>
        private void LogPathCell(IntVec3 c)
        {
            Log.Message($"[SSR_PB_PATH][{thingIDNumber}] tick={Find.TickManager.TicksGame} cell={c} p={penetratingPower:F2}");
        }

        private void LogHitCell(IntVec3 c, Thing thing)
        {
            Log.Message($"[SSR_PB_HITCELL][{thingIDNumber}] tick={Find.TickManager.TicksGame} cell={c} thing={thing?.def?.defName ?? "null"} id={thing?.thingIDNumber ?? -1} p={penetratingPower:F2}");
        }

        private static bool IsTreePlant(Plant plant)
        {
            if (plant == null)
            {
                return false;
            }
            return (plant.def.ingestible != null && plant.def.ingestible.foodType == FoodTypeFlags.Tree) || plant.def.defName == "BurnedTree";
        }

        /// <summary>过滤同阵营建筑/生物，避免友军伤害。</summary>
        private bool IsFriendlyBuildingOrPawn(Thing thing)
        {
            if (thing == null || launcher == null || launcher.Faction == null || thing.Faction == null)
            {
                return false;
            }
            if (thing is Building building)
            {
                // 友军墙体不参与过滤：允许被命中判定。
                if (building.def.defName.Contains("Wall"))
                {
                    return false;
                }
                return !thing.Faction.HostileTo(launcher.Faction);
            }
            if (thing is Pawn)
            {
                return !thing.Faction.HostileTo(launcher.Faction);
            }
            return false;
        }

        /// <summary>应用命中视觉效果、战斗日志与伤害。</summary>
        private void ApplyHitDamage(Thing hitThing, IntVec3 impactCell)
        {
            if (hitThing == null)
            {
                return;
            }
            if (hitThing is Pawn hitPawn)
            {
                EffecterDef damageEffecter = hitPawn.RaceProps.FleshType.damageEffecter;
                if (damageEffecter != null)
                {
                    if (hitPawn.health.woundedEffecter != null && hitPawn.health.woundedEffecter.def != damageEffecter)
                    {
                        hitPawn.health.woundedEffecter.Cleanup();
                    }
                    hitPawn.health.woundedEffecter = damageEffecter.Spawn();
                    hitPawn.health.woundedEffecter.Trigger(hitPawn, launcher ?? hitPawn);
                }
            }
            else if (hitThing is Plant plant && IsTreePlant(plant))
            {
                TargetInfo targetInfo = new TargetInfo(plant.Position, Map, false);
                Effecter effecter = EffecterDefOf.Effecter_SSR_PGHitTrunk.Spawn();
                effecter.Trigger(targetInfo, targetInfo);
                effecter.Cleanup();
                if (!plant.LeaflessNow && plant.def.defName != "Plant_SaguaroCactus")
                {
                    Effecter effecter2 = EffecterDefOf.Effecter_SSR_PGHitTree.Spawn();
                    effecter2.Trigger(targetInfo, targetInfo);
                    effecter2.Cleanup();
                }
            }

            BattleLogEntry_RangedImpact battleLogEntry = new BattleLogEntry_RangedImpact(launcher, hitThing, intendedTarget.Thing, equipmentDef, def, targetCoverDef);
            Find.BattleLog.Add(battleLogEntry);
            OriginalNotifyImpact(hitThing, Map, impactCell);

            bool instigatorGuilty = !(launcher is Pawn pawn) || !pawn.Drafted;
            DamageInfo dinfo = new DamageInfo(def.projectile.damageDef, DamageAmount, ArmorPenetration, ExactRotation.eulerAngles.y, launcher, null, equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, intendedTarget.Thing, instigatorGuilty);
            dinfo.SetWeaponQuality(equipmentQuality);
            hitThing.TakeDamage(dinfo).AssociateWithLog(battleLogEntry);
            (hitThing as Pawn)?.stances?.stagger.Notify_BulletImpact(this);
            if (def.projectile.extraDamages == null)
            {
                return;
            }
            foreach (ExtraDamage extraDamage in def.projectile.extraDamages)
            {
                if (Rand.Chance(extraDamage.chance))
                {
                    DamageInfo dinfo2 = new DamageInfo(extraDamage.def, extraDamage.amount, extraDamage.AdjustedArmorPenetration(), ExactRotation.eulerAngles.y, launcher, null, equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, intendedTarget.Thing, instigatorGuilty);
                    hitThing.TakeDamage(dinfo2).AssociateWithLog(battleLogEntry);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref attackedThings, "attackedThings", LookMode.Reference);
            Scribe_Values.Look(ref penetratingPower, "penetratingPower");
        }
        public static bool canDestroyNow = true;
        public List<Thing> attackedThings = new List<Thing>();
        public float penetratingPower;

        /// <summary>向命中点周围对象广播“附近子弹命中”事件，保持原版感知行为。</summary>
        public void OriginalNotifyImpact(Thing hitThing, Map map, IntVec3 position)
        {
            BulletImpactData bulletImpactData = default(BulletImpactData);
            bulletImpactData.bullet = this;
            bulletImpactData.hitThing = hitThing;
            bulletImpactData.impactPosition = position;
            BulletImpactData impactData = bulletImpactData;
            hitThing?.Notify_BulletImpactNearby(impactData);
            int num = 9;
            for (int i = 0; i < num; i++)
            {
                IntVec3 c = position + GenRadial.RadialPattern[i];
                if (!c.InBounds(map))
                {
                    continue;
                }

                List<Thing> thingList = c.GetThingList(map);
                for (int j = 0; j < thingList.Count; j++)
                {
                    if (thingList[j] != hitThing)
                    {
                        thingList[j].Notify_BulletImpactNearby(impactData);
                    }
                }
            }
        }
    }
    

    public class ModExtension_PenetratingProjectile : DefModExtension
    {
        public float overpenDamageMulti = 0.5f;

        public float stopperDamageMulti = 1f;

        public float PenetrationFloorPercentage = 0.2f;

        public float penetrationPotentialMultiplier = 1;

        public float postPenetrationDeviationAngle = 15;

        public float minSearchRadius = 5;

        public float maxSearchRadius = 20;

        public float extraRange = 20;

        public float buildingEquivalentMulti = 0.005f;

        public float armorEquivalentMulti = 1f;

        public float bodysizeEquivalentMulti = 0.1f;

        public float treeEquivalent = 0.1f;

        public float chunkEquivalent = 0.25f;

        public float bounceEquivalent = 0.2f;

        public bool straightPassThrough = false;

        public int maxPenetratingCount = -1;
    }
}

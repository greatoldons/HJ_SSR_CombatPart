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

        public float PenetratingPowerBase => def.projectile.GetArmorPenetration(weaponDamageMultiplier) * PenetrationFloorPercentage;
        // 获取武器的伤害倍数（从 Thing 获取 StatDefOf.RangedWeapon_DamageMultiplier）
    private float GetWeaponDamageMultiplier => equipment?.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) ?? 1f;
        public override float ArmorPenetration => Extension == null ? def.projectile.GetArmorPenetration(weaponDamageMultiplier) : def.projectile.GetArmorPenetration(weaponDamageMultiplier) * (penetratingPower / MaxPenetratingPower);

        public bool ShouldTerminate;

        Vector3 initialVector;

        IntVec3 cachedPosition;

        Vector3 trueOrigin;

        ModExtension_PenetratingProjectile Extension => def.GetModExtension<ModExtension_PenetratingProjectile>();

        public override int DamageAmount => ShouldTerminate ? def.projectile.GetDamageAmount(StopperDamageMulti) : def.projectile.GetDamageAmount(overpenDamageMulti);
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
                penetratingPower = def.projectile.GetArmorPenetration(weaponDamageMultiplier);
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

        //public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        //{
        //    if (equipment is Building_TurretGun turret)
        //    {
        //        equipment = turret.gun;
        //        attackedThings.Add(turret);
        //    }
        //    base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
        //    //initialVector = usedTarget.CenterVector3 - origin;
        //    //initialVector.y = 0;

        //    initialVector = (usedTarget.CenterVector3 - origin).normalized;

        //    trueOrigin = origin;

        //    PenetratingCount = maxPenetratingCount;
        //}
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
        }

        protected override void Tick()
        {
            if ((penetratingPower <= PenetratingPowerBase && maxPenetratingCount <0)|| PenetratingCount == 0|| ticksToImpact <= 0 || Map == null)
            {
                //Log.Message($"Destroy penertP{penetratingPower}  <= {PenetratingPowerBase}  or ticksToImpact = {ticksToImpact}");
                Destroy();
                return;
            }
            if (Map != null)
            {
                base.Tick();
            }
            // Log.Message($"SSR_Combat_Now_Position:{Position}");
            // 位置更新 + 计算拦截
            if (Position != cachedPosition)
            {
                CheckForFreeIntercept(Position);
                cachedPosition = Position;
                //Log.Message($"SSR_Combat_Position:{Position}");
            }
            ticksToImpact--;
            //Log.Message($"SSR_Combat_tickToImpact:{ticksToImpact}");
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

        List<Thing> thingList = new List<Thing>();
        /// <summary>
        /// 计算子弹是否有击中，是否可以被拦截
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        private bool CheckForFreeIntercept(IntVec3 c)
        {
            if (destination.ToIntVec3() == c)
            {
                return false;
            }
            thingList = c.GetThingList(base.Map);
            for (int i = 0; i < thingList.Count; i++)
            {
                Thing thing = thingList[i];
                if (!CanHit(thing))
                {
                    //Log.Message("SSR_Combat_CantHit");
                    continue;
                }
                bool flag2 = false;

                // 开着的门处理下
                if (thing.def.Fillage == FillCategory.Full)
                {
                    if (!(thing is Building_Door building_Door) || !building_Door.Open)
                    {
                        Impact(thing);
                        return true;
                    }
                    flag2 = true;
                }
                float num2 = 0f;
                if (thing is Building)
                {
                    //Log.Message($"SSR_Combat_building {thing.def.defName} num {num2}");
                    if (launcher != null && thing.Faction != null && launcher.Faction != null && !thing.Faction.HostileTo(launcher.Faction))
                    {
                        //Log.Message($"same Faction");
                        if (thing.def.defName == "Sandbags")
                        {
                            //Log.Message("一伙的沙袋");
                            num2 = 0f;
                        }
                        else
                        {
                            num2 = 0.5f;
                        }

                    }
                    else
                    {
                        num2 = 0.5f;
                    }
                }
                else if (thing is Pawn pawn)
                {
                    num2 = 0.4f * Mathf.Clamp(pawn.BodySize, 0.1f, 2f);
                    if (pawn.GetPosture() != 0)
                    {
                        num2 *= 0.1f;
                    }
                    if (launcher != null && pawn.Faction != null && launcher.Faction != null && !pawn.Faction.HostileTo(launcher.Faction))
                    {
                        num2 = 0f;
                        //if(true)
                        //{
                        //    num2 = 0f;
                        //}
                        //else
                        //{
                        //    num2 *= Find.Storyteller.difficulty.friendlyFireChanceFactor * VerbUtility.InterceptChanceFactorFromDistance(origin, c);
                        //}
                    }
                }
                else if (thing.def.fillPercent > 0.2f)
                {
                    num2 = (flag2 ? 0.05f : ((!DestinationCell.AdjacentTo8Way(c)) ? (thing.def.fillPercent * 0.15f) : (thing.def.fillPercent * 1f)));
                    //Log.Message($"SSR_Combat_HitThing FillPercent {thing.def.fillPercent} num {num2}");
                }
                if (num2 > 1E-05f)
                    {
                        if (Rand.Chance(num2) || straightPassThrough)
                        {
                            Impact(thing);
                            return true;
                        }
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
            //Log.Message($"SSR_Combat Impact Something Position :{Position}");
            if (blockedByShield)
            {
                Destroy();
                return;
            }
            if (attackedThings.Contains(hitThing) || (penetratingPower <= PenetratingPowerBase && maxPenetratingCount < 0) || PenetratingCount == 0)
            {
                landed = false;
                return;
            }
            canDestroyNow = false;
            float removeValue = 0;
            if (hitThing is Building)
            {
                //Log.Message($"SSR_Combat heat building");
                Effecter effect = RimWorld.EffecterDefOf.DamageDiminished_Metal.Spawn();
                effect.Trigger(hitThing, hitThing);
                //effect.Trigger(hitThing, hitThing);
                if (hitThing.def.useHitPoints)
                {
                    removeValue = hitThing.HitPoints * BuildingEquivalentMulti;
                }
                else
                {
                    penetratingPower = 0;
                }
            }
            if (hitThing is Pawn pawn)
            {
                removeValue = GetPawnArmor(pawn) * 2f * ArmorEquivalentMulti + (float)(Math.Sqrt(pawn.BodySize) * pawn.BodySize) * BodysizeEquivalentMulti;
                EffecterDef damageEffecter = pawn.RaceProps.FleshType.damageEffecter;
                if (damageEffecter != null)
                {
                    if (pawn.health.woundedEffecter != null && pawn.health.woundedEffecter.def != damageEffecter)
                    {
                        pawn.health.woundedEffecter.Cleanup();
                    }
                    pawn.health.woundedEffecter = damageEffecter.Spawn();
                    pawn.health.woundedEffecter.Trigger(pawn, launcher ?? pawn);
                    pawn.health.woundedEffecter.Trigger(pawn, launcher ?? pawn);
                    pawn.health.woundedEffecter.Trigger(pawn, launcher ?? pawn);
                }
            }
            if (hitThing is Plant plant)
            {
                //Log.Message($"SSR_Combat heat Plant");
                if ((plant.def.ingestible != null && plant.def.ingestible.foodType == FoodTypeFlags.Tree) || plant.def.defName == "BurnedTree")
                {
                    removeValue = TreeEquivalent;
                    Effecter effecter = EffecterDefOf.Effecter_SSR_PGHitTrunk.Spawn();
                    TargetInfo targetInfo = new TargetInfo(plant.Position, Map, false);
                    effecter.Trigger(targetInfo, targetInfo);
                    effecter.Cleanup();
                    if (!plant.LeaflessNow && plant.def.defName != "Plant_SaguaroCactus")
                    {
                        Effecter effecter2 = EffecterDefOf.Effecter_SSR_PGHitTree.Spawn();
                        effecter2.Trigger(targetInfo, targetInfo);
                        effecter2.Cleanup();
                    }
                }
            }
            if (hitThing != null && IsChunk(hitThing.def.thingCategories))
            {
                //Log.Message($"SSR_Combat Chunk");
                removeValue = ChunkEquivalent;
            }
            if (hitThing != null)
            {
                //Log.Message($"SSR_Combat Just Add");
                attackedThings.Add(hitThing);
            }
            if (penetratingPower - PenetratingPowerBase <= removeValue)
            {
                ShouldTerminate = true;
            }
            //Log.Message($"SSR_Combat GoImpact");
            //base.Impact(hitThing);
            OriginalImpact(hitThing);
            if (def.projectile.explosionRadius > 0)
            {
                Explode();
            }
            canDestroyNow = true;
            landed = false;
            if (hitThing == null)
            {
                if (Rand.Chance(ArmorPenetration / def.projectile.GetArmorPenetration(weaponDamageMultiplier)))
                {
                    removeValue = BounceEquivalent;
                }
                else
                {
                    penetratingPower = 0;
                }
            }
            if(PenetratingCount < 0)
            {
                penetratingPower -= removeValue;
            }
            else
            {
                PenetratingCount = (PenetratingCount > 0) ? --PenetratingCount : PenetratingCount;
            }
            fuseActivated = true;

            float remainingRange = equipmentDef.Verbs[0].range + ExtraRange - (ExactPosition - trueOrigin).magnitude;
            //if (remainingRange <= 0 || penetratingPower <= PenetratingPowerBase || PenetratingCount ==0)
            //Log.Message($"PenetratingCount:{PenetratingCount}");
            if (remainingRange <= 0 || (penetratingPower <= PenetratingPowerBase && maxPenetratingCount < 0) || PenetratingCount == 0)
            {
                canDestroyNow = true;
                Destroy();
                return;
            }
            //Retarget_Direct();
        }
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (!canDestroyNow)
            {
                return;
            }
            base.Destroy(mode);
        }

        /// <summary>
        /// 弹道击中后重新计算
        /// 
        /// </summary>
        public void Retarget()
        {
            float remainingRange = 55;
            if (equipmentDef != null && equipmentDef.Verbs.Any())
            {
                remainingRange = equipmentDef.Verbs[0].range - (ExactPosition - trueOrigin).magnitude + ExtraRange;
            }
            if (remainingRange <= 0)
            {
                canDestroyNow = true;
                Destroy();
                return;
            }
            
            if (remainingRange > MaxSearchRadius) remainingRange = MaxSearchRadius;
            int num = GenRadial.NumCellsInRadius(remainingRange);
            int tries = 20 + (int)remainingRange;
            IntVec3 retargetCell = IntVec3.Invalid;
            List<Thing> affectedTargets = new List<Thing>();

            //20 times of trying random
            if(straightPassThrough)
            {
                retargetCell = Position + initialVector.ToIntVec3();
                //Log.Message("Direct PassThrough");
            }
            else
            {
                while (tries > 0)
                {
                    int i = Rand.Range(1, num);
                    if (Math.Abs(Vector3.Angle(initialVector, GenRadial.RadialPattern[i].ToVector3().Yto0())) < PostPenetrationDeviationAngle)
                    {
                        retargetCell = Position + GenRadial.RadialPattern[i];
                        break;
                    }
                    tries--;
                }
            }


            //Fall back enum check
            if (!retargetCell.IsValid)
            {
                List<IntVec3> affectedCells = new List<IntVec3>();
                for (int i = 1; i < num; i++)
                {
                    if (Math.Abs(Vector3.Angle(initialVector, GenRadial.RadialPattern[i].ToVector3().Yto0())) < PostPenetrationDeviationAngle)
                    {
                        affectedCells.Add(Position + GenRadial.RadialPattern[i]);
                    }
                }
                if (affectedCells.Any())
                {
                    if (DebugSettings.godMode)
                    {
                        foreach (var c in affectedCells)
                        {
                            Map.debugDrawer.FlashCell(c);
                        }
                    }
                    retargetCell = affectedCells[Rand.Range(0, affectedCells.Count - 1)];
                }
            }
            if (!retargetCell.IsValid)
            {
                canDestroyNow = true;
                Destroy();
                return;
            }
            else if (Map != null && Position.InBounds(Map))
            {
                origin = ExactPosition;
                if (retargetCell.InBounds(Map)) affectedTargets = retargetCell.GetThingList(Map).ToList();
                Thing target = null;
                if (DebugSettings.godMode) Map.debugDrawer.FlashCell(retargetCell);
                if (affectedTargets.Count > 0)
                {
                    target = affectedTargets[Rand.Range(0, affectedTargets.Count - 1)];
                }
                usedTarget = target == null ? retargetCell : new LocalTargetInfo(target);
                destination = usedTarget.Cell.ToVector3Shifted() + Gen.RandomHorizontalVector(0.3f);
                ticksToImpact = Mathf.CeilToInt(StartingTicksToImpact);
                if (ticksToImpact < 1)
                {
                    ticksToImpact = 1;
                }
            }
        }
       
        public void Retarget_Direct()
        {
            // 计算剩余射程（初始射程 + 额外射程 - 已飞行距离）
            float remainingRange = equipmentDef.Verbs[0].range + ExtraRange - (ExactPosition - trueOrigin).magnitude;
            //if (remainingRange <= 0 || penetratingPower <= PenetratingPowerBase || PenetratingCount ==0)
            if (remainingRange <= 0 || (penetratingPower <= PenetratingPowerBase && maxPenetratingCount < 0) || PenetratingCount == 0)
            {
                canDestroyNow = true;
                Destroy();
                return;
            }
            // 沿初始方向延伸目标点（不改变方向）
            Vector3 newDestination = ExactPosition + initialVector.normalized * remainingRange;
            // 修正目标点在地图边界内
            IntVec3 newDestCell = newDestination.ToIntVec3();
            if (!newDestCell.InBounds(Map))
            {
                newDestCell = newDestCell.ClampInsideMap(Map);
                newDestination = newDestCell.ToVector3Shifted();
            }
            // 更新目标位置和飞行时间
            destination = newDestination;
            ticksToImpact = Mathf.CeilToInt(remainingRange / def.projectile.SpeedTilesPerTick);
            if (ticksToImpact < 1) ticksToImpact = 1;

            // 调试显示路径（可选）
            //if (DebugSettings.godMode)
            //{
            //    Map.debugDrawer.FlashLine(ExactPosition.ToIntVec3(), destination.ToIntVec3(), duration: 100, SimpleColor.Red);
            //}
        }
        
        private static float GetPawnArmor(Pawn pawn)
        {
            float statValue = pawn.GetStatValue(RimWorld.StatDefOf.ArmorRating_Sharp, true);
            float num = statValue;
            List<Apparel> list = pawn.apparel?.WornApparel ?? Enumerable.Empty<Apparel>().ToList();
            foreach (Apparel apparel2 in list)
            {
                num += apparel2.GetStatValue(RimWorld.StatDefOf.ArmorRating_Sharp, true) * apparel2.def.apparel.HumanBodyCoverage;
            }
            if (num > 0.0001f)
            {
                foreach (BodyPartRecord bodyPartRecord in pawn.RaceProps.body.AllParts)
                {
                    BodyPartRecord bodyPartRecord2 = bodyPartRecord;
                    float num2 = statValue;
                    if (bodyPartRecord2.depth == BodyPartDepth.Outside && (bodyPartRecord2.coverage >= 0.1 || bodyPartRecord2.def == BodyPartDefOf.Eye || bodyPartRecord2.def == BodyPartDefOf.Torso))
                    {
                        foreach (Apparel apparel3 in list)
                        {
                            if (apparel3.def.apparel.CoversBodyPart(bodyPartRecord2))
                            {
                                num2 += apparel3.GetStatValue(RimWorld.StatDefOf.ArmorRating_Sharp, true);
                            }
                        }
                        return num2;
                    }
                }
            }
            return num;
        }
        public static bool IsChunk(List<ThingCategoryDef> thingCategory)
        {
            foreach (ThingCategoryDef x in thingCategory ?? Enumerable.Empty<ThingCategoryDef>())
            {
                if (IsChunk(x)) return true;
            }
            return false;
        }
        public static bool IsChunk(ThingCategoryDef thingCategory)
        {
            if (thingCategory == ThingCategoryDefOf.Chunks || ThingCategoryDefOf.Chunks.childCategories.Contains(thingCategory)) return true;
            return false;
        }

        protected virtual void Explode()
        {
            Map map = base.Map;
            if (def.projectile.explosionEffect != null)
            {
                Effecter effecter = def.projectile.explosionEffect.Spawn();
                effecter.Trigger(new TargetInfo(base.Position, map), new TargetInfo(base.Position, map));
                effecter.Cleanup();
            }
            GenExplosion.DoExplosion(base.Position, map, def.projectile.explosionRadius, def.projectile.damageDef, launcher, DamageAmount, ArmorPenetration, def.projectile.soundExplode, equipmentDef, def, intendedTarget.Thing, def.projectile.postExplosionSpawnThingDef, postExplosionSpawnThingDefWater: def.projectile.postExplosionSpawnThingDefWater, postExplosionSpawnChance: def.projectile.postExplosionSpawnChance, postExplosionSpawnThingCount: def.projectile.postExplosionSpawnThingCount, postExplosionGasType: def.projectile.postExplosionGasType, preExplosionSpawnThingDef: def.projectile.preExplosionSpawnThingDef, preExplosionSpawnChance: def.projectile.preExplosionSpawnChance, preExplosionSpawnThingCount: def.projectile.preExplosionSpawnThingCount, applyDamageToExplosionCellsNeighbors: def.projectile.applyDamageToExplosionCellsNeighbors, chanceToStartFire: def.projectile.explosionChanceToStartFire, damageFalloff: def.projectile.explosionDamageFalloff, direction: origin.AngleToFlat(destination), ignoredThings: null, affectedAngle: null, doVisualEffects: true, propagationSpeed: def.projectile.damageDef.expolosionPropagationSpeed, excludeRadius: 0f, doSoundEffects: true, screenShakeFactor: def.projectile.screenShakeFactor);
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

        public void OriginalImpact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = base.Map;
            IntVec3 position = base.Position;
            base.Impact(hitThing, blockedByShield);
            BattleLogEntry_RangedImpact battleLogEntry_RangedImpact = new BattleLogEntry_RangedImpact(launcher, hitThing, intendedTarget.Thing, equipmentDef, def, targetCoverDef);
            Find.BattleLog.Add(battleLogEntry_RangedImpact);
            OriginalNotifyImpact(hitThing, map, position);
            if (hitThing != null)
            {
                Pawn pawn;
                bool instigatorGuilty = (pawn = launcher as Pawn) == null || !pawn.Drafted;
                DamageInfo dinfo = new DamageInfo(def.projectile.damageDef, DamageAmount, ArmorPenetration, ExactRotation.eulerAngles.y, launcher, null, equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, intendedTarget.Thing, instigatorGuilty);
                dinfo.SetWeaponQuality(equipmentQuality);
                hitThing.TakeDamage(dinfo).AssociateWithLog(battleLogEntry_RangedImpact);
                Pawn pawn2 = hitThing as Pawn;
                pawn2?.stances?.stagger.Notify_BulletImpact(this);
                if (def.projectile.extraDamages != null)
                {
                    foreach (ExtraDamage extraDamage in def.projectile.extraDamages)
                    {
                        if (Rand.Chance(extraDamage.chance))
                        {
                            DamageInfo dinfo2 = new DamageInfo(extraDamage.def, extraDamage.amount, extraDamage.AdjustedArmorPenetration(), ExactRotation.eulerAngles.y, launcher, null, equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, intendedTarget.Thing, instigatorGuilty);
                            hitThing.TakeDamage(dinfo2).AssociateWithLog(battleLogEntry_RangedImpact);
                        }
                    }
                }

                if (Rand.Chance(def.projectile.bulletChanceToStartFire) && (pawn2 == null || Rand.Chance(FireUtility.ChanceToAttachFireFromEvent(pawn2))))
                {
                    hitThing.TryAttachFire(def.projectile.bulletFireSizeRange.RandomInRange, launcher);
                }

                return;
            }

            if (!blockedByShield)
            {
                SoundDefOf.BulletImpact_Ground.PlayOneShot(new TargetInfo(base.Position, map));
                if (base.Position.GetTerrain(map).takeSplashes)
                {
                    FleckMaker.WaterSplash(ExactPosition, map, Mathf.Sqrt(DamageAmount) * 1f, 4f);
                }
                else
                {
                    FleckMaker.Static(ExactPosition, map, FleckDefOf.ShotHit_Dirt);
                }
            }

            if (Rand.Chance(def.projectile.bulletChanceToStartFire))
            {
                FireUtility.TryStartFireIn(base.Position, map, def.projectile.bulletFireSizeRange.RandomInRange, launcher);
            }
        }
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

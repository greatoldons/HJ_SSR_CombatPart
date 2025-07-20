using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace HJ_SSR.Weapons
{
    public class Verb_ShootCustom : Verb_ShootAllowAllCast
    {
        public DefModExtension_ShootUsingRandomProjectileBase DataRandProj
        {
            get
            {
                return EquipmentCompSource.parent.def.GetModExtension<DefModExtension_ShootUsingRandomProjectileBase>();
            }
        }
        ModExtension_Verb_Shotgun DataShotgun => EquipmentSource.def.GetModExtension<ModExtension_Verb_Shotgun>();
        ModExtension_VerbNotUnderRoof DataNotUnderRoof => EquipmentSource.def.GetModExtension<ModExtension_VerbNotUnderRoof>();
        DefModExtension_ShootUsingMechBattery DataMechBattery => EquipmentSource.def.GetModExtension<DefModExtension_ShootUsingMechBattery>();
        ModExtension_RandomBurstBreak randomBurstBreak => EquipmentSource.def.GetModExtension<ModExtension_RandomBurstBreak>();
        ModExtension_DropItemWhenFire DataDropItem => EquipmentSource.def.GetModExtension<ModExtension_DropItemWhenFire>();
        ModExtension_OneUse DataOneUse => EquipmentSource.def.GetModExtension<ModExtension_OneUse>();
        ModExtension_ProjOriginOffset DataOriginOffset => EquipmentSource.def.GetModExtension<ModExtension_ProjOriginOffset>();

        ModExtension_MultiShot DataMultiShot => EquipmentSource.def.GetModExtension<ModExtension_MultiShot>();
        CompSecondaryVerb compSecondaryVerb => EquipmentSource.TryGetComp<CompSecondaryVerb>();


        int currenBurstRandomIndex;

        private Vector3 lastScatteredTargetPos = Vector3.zero;

        private bool isFirstShot = true;
        public override void WarmupComplete()
        {
            RandomizeProjectile();
            base.WarmupComplete();
            RandomizeBurstCount();
        }

        public override bool Available()
        {
            if (verbProps.consumeFuelPerBurst > 0f)
            {
                CompRefuelable compRefuelable = caster.TryGetComp<CompRefuelable>();
                if (compRefuelable != null && compRefuelable.Fuel < verbProps.consumeFuelPerBurst)
                {
                    return false;
                }
            }
            return AvailableNotUnderRoof() && AvailableMechBattery() && base.Available();
        }

        public override void Notify_EquipmentLost()
        {
            base.Notify_EquipmentLost();
            if (state == VerbState.Bursting && burstShotsLeft < verbProps.burstShotCount)
            {
                TryCastShotOneUse();
            }
        }

        protected override bool TryCastShot()
        {
            if (DataRandProj != null && DataRandProj.randomWithinBurst)
            {
                RandomizeProjectile();
            }
            var casterdp = Caster.DrawPos;
            //if (base.TryCastShot())
            //{
            //    TryCastShotMultiShot();
            //    TryCastShotMechBattery();
            //    TryCastShotShotgun();
            //    TryCastShotRandomBurstBreak();
            //    TryCastShotDropItemWhenFire();
            //    TryCastShotOneUse();
            //    return true;
            //}
            if(base.TryCastShot())
            {
                //TryCastShotShotgun();
                TryCastShotShotgun_new();
            }
            return false;
        }

        public bool AvailableNotUnderRoof()
        {
            return !(DataNotUnderRoof != null && Caster.Position.Roofed(Caster.Map) && (compSecondaryVerb == null || (compSecondaryVerb.IsSecondaryVerbSelected && DataNotUnderRoof.appliesInSecondaryMode) || (!compSecondaryVerb.IsSecondaryVerbSelected && DataNotUnderRoof.appliesInPrimaryMode)));
        }

        public bool AvailableMechBattery()
        {
            Need_MechEnergy battery = CasterPawn?.needs.TryGetNeed<Need_MechEnergy>();
            if (battery != null)
            {
                return battery.CurLevel > DataMechBattery.energyConsumption;
            }
            return true;
        }

        public void RandomizeProjectile()
        {
            if (DataRandProj != null)
            {
                verbProps.defaultProjectile = DataRandProj.GetProjectile();
            }
        }

        public void RandomizeBurstCount()
        {
            if (randomBurstBreak != null)
            {
                burstShotsLeft += randomBurstBreak.randomBurst.RandomInRange;
            }
        }

        void TryCastShotMechBattery()
        {
            if (ModLister.BiotechInstalled && DataMechBattery != null)
            {
                Need_MechEnergy battery = CasterPawn?.needs.TryGetNeed<Need_MechEnergy>();
                if (battery != null)
                {
                    battery.CurLevel -= DataMechBattery.energyConsumption;
                }
            }
        }

        void TryCastShotShotgun()
        {
            if (DataShotgun != null)
            {
                //if (DataShotgun.ShotgunPellets > 1)
                //{
                //    for (int i = 1; i < DataShotgun.ShotgunPellets; i++)
                //    {
                //        base.TryCastShot();
                //    }
                //}

                // 主弹丸散射 散射的时候会有弹丸同一角度飞出，视觉效果来说像是开了两枪，sb
                if (DataShotgun.ShotgunPellets > 1)
                {
                    isFirstShot = true;
                    for (int i = 1; i < DataShotgun.ShotgunPellets; i++)
                    {

                        // 应用散射偏移
                        //LocalTargetInfo scatteredTarget = GetScatteredTarget(
                        //    caster.DrawPos,
                        //    currentTarget.Cell.ToVector3(),
                        //    DataShotgun.spreadAngle,
                        //    DataShotgun.minSpreadDistance
                        //);
                        // 获取新目标位置
                        Vector3 newPos = GetScatteredTarget(
                            caster.DrawPos,
                            currentTarget.Cell.ToVector3(),
                            DataShotgun.spreadAngle,
                            DataShotgun.minSpreadDistance
                        ).CenterVector3;
                        // 避免与上次位置相同
                        if (!isFirstShot && Vector3.Distance(lastScatteredTargetPos, newPos) < 0.1f)
                        {
                            // 应用随机偏移（0.5-1.5格）
                            float offset = Rand.Range(0.5f, 1.5f);
                            newPos += new Vector3(
                                Rand.Range(-offset, offset),
                                0f,
                                Rand.Range(-offset, offset)
                            );

                            // 确保不超出地图边界
                            IntVec3 cell = newPos.ToIntVec3();
                            if (!cell.InBounds(caster.Map))
                            {
                                cell = cell.ClampInsideMap(caster.Map);
                                newPos = cell.ToVector3Shifted();
                            }
                        }
                        // Log.Message($"shot lastPosition :{lastScatteredTargetPos}  newPosition {newPos}");
                        // 更新缓存
                        lastScatteredTargetPos = newPos;
                        isFirstShot = false;

                        // 创建目标对象
                        LocalTargetInfo scatteredTarget = new LocalTargetInfo(newPos.ToIntVec3());

                        // 临时修改目标并发射
                        LocalTargetInfo original = currentTarget;
                        currentTarget = scatteredTarget;
                        bool result = base.TryCastShot();
                        // Log.Message($"HJ_SSR shotReult{result}");
                        currentTarget = original;
                        //if (lastTarget != null && lastTarget.CenterVector3 == scatteredTarget.CenterVector3)
                        //{  
                        //    Log.Message($"shot lastPosition :{lastTarget}  newPosition {scatteredTarget}");
                        //    scatteredTarget.CenterVector3.Set(scatteredTarget.Cell.x + 1, scatteredTarget.Cell.y, scatteredTarget.Cell.z + 1);
                        //    Log.Message($"shot newPosition {scatteredTarget}");
                        //}
                        //lastTarget = scatteredTarget;
                        //// 临时修改目标并发射
                        //LocalTargetInfo original = currentTarget;
                        //currentTarget = scatteredTarget;
                        //base.TryCastShot();
                        //currentTarget = original;
                    }
                    //放在这，让只播一次
                    verbProps.soundCast?.PlayOneShot(new TargetInfo(caster.Position, caster.MapHeld));
                    verbProps.soundCastTail?.PlayOneShotOnCamera(caster.Map);
                    //if (verbProps.soundCastTail != null)
                    //{
                    //    // 
                    //    verbProps.soundCastTail.PlayOneShotOnCamera(caster.Map);
                    //}

                }
                if (DataShotgun.extraProjectile != null && DataShotgun.extraProjectileCount > 0)
                {
                    ThingDef originalProjectile = verbProps.defaultProjectile;
                    verbProps.defaultProjectile = DataShotgun.extraProjectile;
                    for (int i = 0; i < DataShotgun.extraProjectileCount; i++)
                    {
                        base.TryCastShot();
                    }
                    verbProps.defaultProjectile = originalProjectile;
                }
            }
        }

        void TryCastShotShotgun_new()
        {
            if (DataShotgun == null || DataShotgun.ShotgunPellets <= 1)
                return;

            // 1. 计算原始方向向量
            Vector3 origin = caster.DrawPos;
            Vector3 targetCenter = currentTarget.CenterVector3;
            Vector3 rawDirection = (targetCenter - origin).normalized;

            // 2. 计算射程一半距离的新目标点
            float halfDistance = verbProps.range / 2f;
            Vector3 midPoint = origin + rawDirection * halfDistance;

            // 3. 计算垂直方向向量 (Y轴)
            Vector3 perpendicular = new Vector3(-rawDirection.z, 0, rawDirection.x).normalized;

            // 4. 计算弹丸间距
            int pelletCount = DataShotgun.ShotgunPellets - 1; // 已发射第一发
            float spacing = Mathf.Min(1.5f, verbProps.range / pelletCount); // 最大间距1.5格



            // 6. 生成弹丸目标位置
            for (int i = 0; i < pelletCount; i++)
            {
                // 计算偏移量 (对称分布)
                float offset = (i - pelletCount / 2f + 0.5f) * spacing;
                // Log.Message($"HJ_SSR Offset:{offset}");
                // 计算最终目标位置
                Vector3 pelletTarget = midPoint + perpendicular * offset;

                // 确保位置有效
                IntVec3 targetCell = pelletTarget.ToIntVec3();
                if (!targetCell.InBounds(caster.Map))
                {
                    targetCell = targetCell.ClampInsideMap(caster.Map);
                    pelletTarget = targetCell.ToVector3Shifted();
      
                }
                // Log.Message($"HJ_SSR targetCell :{targetCell}");

                // 创建目标并发射
                LocalTargetInfo scatteredTarget = new LocalTargetInfo(targetCell);
                LocalTargetInfo original = currentTarget;
                currentTarget = scatteredTarget;
                bool result = base.TryCastShot();
                // Log.Message($"HJ_SSR shotNEWResult{result}");
                currentTarget = original;

                // 调试可视化
                if (DebugSettings.godMode)
                {
                    Debug.DrawLine(origin, pelletTarget, Color.yellow, 1.5f);
                    Debug.DrawLine(midPoint, pelletTarget, Color.blue, 1.5f);
                }
            }
            // 5. 播放开火音效
            verbProps.soundCast?.PlayOneShot(new TargetInfo(caster.Position, caster.MapHeld));
            verbProps.soundCastTail?.PlayOneShotOnCamera(caster.Map);
        }

        void TryCastShotShotgun_Debug()
        {
            if (DataShotgun == null || DataShotgun.ShotgunPellets <= 1)
                return;

            // 1. 计算原始方向向量
            Vector3 origin = caster.DrawPos;
            Vector3 targetCenter = currentTarget.CenterVector3;
            Vector3 rawDirection = (targetCenter - origin).normalized;

            // 2. 计算射程一半距离的新目标点
            float halfDistance = verbProps.range / 2f;
            Vector3 midPoint = origin + rawDirection * halfDistance;

            // 3. 计算垂直方向向量 (Y轴)
            Vector3 perpendicular = new Vector3(-rawDirection.z, 0, rawDirection.x).normalized;

            // 4. 计算弹丸间距
            int pelletCount = DataShotgun.ShotgunPellets - 1; // 已发射第一发
            float spacing = Mathf.Min(1.5f, verbProps.range / pelletCount); // 最大间距1.5格



            // 6. 生成弹丸目标位置
            for (int i = 0; i < pelletCount; i++)
            {
                // 计算偏移量 (对称分布)
                float offset = (i - pelletCount / 2f + 0.5f) * spacing;
                // Log.Message($"HJ_SSR Offset:{offset}");
                // 计算最终目标位置
                Vector3 pelletTarget = midPoint + perpendicular * offset;

                // 确保位置有效
                IntVec3 targetCell = pelletTarget.ToIntVec3();
                if (!targetCell.InBounds(caster.Map))
                {
                    targetCell = targetCell.ClampInsideMap(caster.Map);
                    pelletTarget = targetCell.ToVector3Shifted();

                }
                // Log.Message($"HJ_SSR targetCell :{targetCell}");

                // 创建目标并发射
                LocalTargetInfo scatteredTarget = new LocalTargetInfo(targetCell);
                LocalTargetInfo original = currentTarget;
                currentTarget = scatteredTarget;
                bool result = base.TryCastShot();
                // Log.Message($"HJ_SSR shotNEWResult{result}");
                currentTarget = original;

                // 调试可视化
                if (DebugSettings.godMode)
                {
                    Debug.DrawLine(origin, pelletTarget, Color.yellow, 1.5f);
                    Debug.DrawLine(midPoint, pelletTarget, Color.blue, 1.5f);
                }
            }
            // 5. 播放开火音效
            verbProps.soundCast?.PlayOneShot(new TargetInfo(caster.Position, caster.MapHeld));
            verbProps.soundCastTail?.PlayOneShotOnCamera(caster.Map);
        }


        void TryCastShotMultiShot()
        {
            if (DataMultiShot != null && DataMultiShot.shotCount > 1)
            {
                float magnitude = Projectile.projectile.SpeedTilesPerTick / DataMultiShot.shotCount;
                for (int i = 1; i < DataMultiShot.shotCount; i++)
                {
                    //Honestly I'd rather not having to do this

                    ShootLine resultingLine;
                    TryFindShootLineFromTo(caster.Position, currentTarget, out resultingLine);
                    if (base.EquipmentSource != null)
                    {
                        base.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
                        base.EquipmentSource.GetComp<CompApparelReloadable>()?.UsedOnce();
                        base.EquipmentSource.GetComp<CompEquippableAbilityReloadable>()?.UsedOnce();
                    }
                    lastShotTick = Find.TickManager.TicksGame;
                    Thing manningPawn = caster;
                    Thing equipmentSource = base.EquipmentSource;
                    CompMannable compMannable = caster.TryGetComp<CompMannable>();
                    if (compMannable != null && compMannable.ManningPawn != null)
                    {
                        manningPawn = compMannable.ManningPawn;
                        equipmentSource = caster;
                    }
                    Vector3 origin = caster.DrawPos;
                    Projectile projectile2 = (Projectile)GenSpawn.Spawn(Projectile, resultingLine.Source, caster.Map);
                    if (verbProps.ForcedMissRadius > 0.5f)
                    {
                        float num = verbProps.ForcedMissRadius;
                        if (manningPawn != null && manningPawn is Pawn pawn) num *= verbProps.GetForceMissFactorFor(equipmentSource, pawn);
                        float num2 = VerbUtility.CalculateAdjustedForcedMiss(num, currentTarget.Cell - caster.Position);
                        if (num2 > 0.5f)
                        {
                            IntVec3 forcedMissTarget = GetForcedMissTarget(num2);
                            if (forcedMissTarget != currentTarget.Cell)
                            {
                                ProjectileHitFlags projectileHitFlags = ProjectileHitFlags.NonTargetWorld;
                                if (Rand.Chance(0.5f)) projectileHitFlags = ProjectileHitFlags.All;
                                if (!canHitNonTargetPawnsNow) projectileHitFlags &= ~ProjectileHitFlags.NonTargetPawns;

                                LaunchProjectileWithOffset(projectile2, magnitude * i, manningPawn, origin, forcedMissTarget, currentTarget, projectileHitFlags, preventFriendlyFire, equipmentSource);
                                return;
                            }
                        }
                    }
                    ShotReport shotReport = ShotReport.HitReportFor(caster, this, currentTarget);
                    Thing randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
                    ThingDef targetCoverDef = randomCoverToMissInto?.def;

                    if (verbProps.canGoWild && !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture))
                    {
                        resultingLine.ChangeDestToMissWild_NewTemp(shotReport.AimOnTargetChance_StandardTarget, Projectile.projectile.flyOverhead, caster.Map);
                        ProjectileHitFlags projectileHitFlags2 = ProjectileHitFlags.NonTargetWorld;
                        if (Rand.Chance(0.5f) && canHitNonTargetPawnsNow) projectileHitFlags2 |= ProjectileHitFlags.NonTargetPawns;

                        LaunchProjectileWithOffset(projectile2, magnitude * i, manningPawn, origin, resultingLine.Dest, currentTarget, projectileHitFlags2, preventFriendlyFire, equipmentSource, targetCoverDef);
                        return;
                    }

                    if (currentTarget.Thing != null && currentTarget.Thing.def.CanBenefitFromCover && !Rand.Chance(shotReport.PassCoverChance))
                    {
                        ProjectileHitFlags projectileHitFlags3 = ProjectileHitFlags.NonTargetWorld;
                        if (canHitNonTargetPawnsNow)
                        {
                            projectileHitFlags3 |= ProjectileHitFlags.NonTargetPawns;
                        }
                        LaunchProjectileWithOffset(projectile2, magnitude * i, manningPawn, origin, randomCoverToMissInto, currentTarget, projectileHitFlags3, preventFriendlyFire, equipmentSource, targetCoverDef);
                        return;
                    }

                    ProjectileHitFlags projectileHitFlags4 = ProjectileHitFlags.IntendedTarget;
                    if (canHitNonTargetPawnsNow) projectileHitFlags4 |= ProjectileHitFlags.NonTargetPawns;
                    if (!currentTarget.HasThing || currentTarget.Thing.def.Fillage == FillCategory.Full) projectileHitFlags4 |= ProjectileHitFlags.NonTargetWorld;
                    if (currentTarget.Thing != null)
                    {
                        LaunchProjectileWithOffset(projectile2, magnitude * i, manningPawn, origin, currentTarget, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource);
                    }
                    else
                    {
                        LaunchProjectileWithOffset(projectile2, magnitude * i, manningPawn, origin, resultingLine.Dest, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource);
                    }
                }
            }
        }

        protected virtual void LaunchProjectileWithOffset(Projectile Proj, float magnitude, Thing lcr, Vector3 origin, LocalTargetInfo uc, LocalTargetInfo ic, ProjectileHitFlags hitFlags, bool pff = false, Thing eq = null, ThingDef cov = null)
        {
            Vector3 vector = new Vector3();
            if (uc.HasThing)
            {
                vector = uc.Thing.TrueCenter() - origin;
            }
            else
            {
                vector = uc.Cell.ToVector3Shifted() - origin;
            }
            vector.Normalize();
            vector *= magnitude;
            Proj.Launch(lcr, origin + vector, uc, currentTarget, hitFlags, pff, eq, cov);
            if (verbProps.consumeFuelPerShot > 0f)
            {
                caster.TryGetComp<CompRefuelable>()?.ConsumeFuel(verbProps.consumeFuelPerShot);
            }
            burstShotsLeft--;
            TryCastShotMechBattery();
            TryCastShotRandomBurstBreak();
            TryCastShotDropItemWhenFire();
        }

        void TryCastShotRandomBurstBreak()
        {
            if (randomBurstBreak != null && Rand.Chance(randomBurstBreak.chance))
            {
                burstShotsLeft = 1;
            }
        }

        void TryCastShotDropItemWhenFire()
        {
            if (DataDropItem != null)
            {
                Thing thing = ThingMaker.MakeThing(DataDropItem.Thingdef, null);
                if (CasterIsPawn && CasterPawn.Faction.IsPlayer && !DataDropItem.alwaysOnGround)
                {
                    CasterPawn.inventory.innerContainer.TryAdd(thing);
                }
                else
                {
                    thing.SetForbidden(true, false);
                    GenPlace.TryPlaceThing(thing, caster.InteractionCell == null ? caster.Position : caster.InteractionCell, caster.Map, ThingPlaceMode.Near, out _, null, null, default);
                }
            }
        }

        void TryCastShotOneUse()
        {
            if (burstShotsLeft <= 1 && DataOneUse != null)
            {
                if (base.EquipmentSource != null && !base.EquipmentSource.Destroyed)
                {
                    base.EquipmentSource.Destroy();
                }

                if (CasterIsPawn && !CasterPawn.IsPlayerControlled)
                {
                    Thing thing = GenClosest.ClosestThingReachable(CasterPawn.Position, CasterPawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Weapon), PathEndMode.OnCell, TraverseParms.For(CasterPawn), 8f, (Thing x) => CasterPawn.CanReserve(x) && !x.IsBurning() && !(x.def.IsRangedWeapon && CasterPawn.WorkTagIsDisabled(WorkTags.Shooting)), null, 0, 15);
                    if (thing != null)
                    {
                        CasterPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(RimWorld.JobDefOf.Equip, thing));
                    }
                }
            }
        }

        // 计算散射位置
        private LocalTargetInfo GetScatteredTarget(Vector3 origin, Vector3 target, float maxAngle, float minDistance)
        {
            // 计算原始方向向量
            Vector3 direction = (target - origin).normalized;
            float distance = Mathf.Max(Vector3.Distance(origin, target), minDistance);

            // 随机散射角度（-maxAngle/2 到 +maxAngle/2）
            float angle = Rand.Range(-maxAngle / 2f, maxAngle / 2f);

            // 旋转方向向量
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 scatteredDir = rotation * direction;

            // 计算新目标位置
            Vector3 scatteredPos = origin + scatteredDir * distance;


            // 查找实际目标（可能命中其他物体）
            return GetActualTargetAt(scatteredPos);
        }

        // 获取指定位置的实际目标
        private LocalTargetInfo GetActualTargetAt(Vector3 position)
        {
            IntVec3 cell = position.ToIntVec3();
            if (!cell.InBounds(caster.Map))
                return new LocalTargetInfo(cell);

            // 优先选择该位置的生物
            List<Thing> things = cell.GetThingList(caster.Map);
            Thing targetThing = things.FirstOrDefault(t =>
                t is Pawn pawn && pawn.RaceProps.IsFlesh && !pawn.Downed
            );

            return targetThing != null ?
                new LocalTargetInfo(targetThing) :
                new LocalTargetInfo(cell);
        }
    }
}

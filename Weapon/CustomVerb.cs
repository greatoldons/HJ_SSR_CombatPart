using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using Verse.AI;
using UnityEngine;

namespace HJ_SSR.Weapons
{
    public class Verb_ShootAllowAllCast : Verb_Shoot
    {
        private static List<IntVec3> tempLeanShootSources = new List<IntVec3>();
        private static List<IntVec3> tempDestList = new List<IntVec3>();

        protected override bool TryCastShot()
        {
            // Log.Message("customTryCastSHot");
            if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
            {
                //bool _r = currentTarget.HasThing; // Log.Message($"HJ_SSR HasThing {_r}");
                //if (_r) {Log.Message($"HJ_SSR  Map {currentTarget.Thing.Map != caster.Map}"); } ;
                
                //return false; go on
            }

            ThingDef projectile = Projectile;
            if (projectile == null)
            {
                // Log.Message("projectile == null");
                return false;
            }

            ShootLine resultingLine;
            bool flag = TryFindShootLineFromTo(caster.Position, currentTarget, out resultingLine);
            if(!flag)
            {
                ShootLine _ResultingLine = new ShootLine(caster.Position, currentTarget.Cell);
                resultingLine = _ResultingLine;
            }
            
            if (verbProps.stopBurstWithoutLos && !flag)
            {
                // Log.Message($"verbProps.stopBurstWithoutLos {verbProps.stopBurstWithoutLos} && {!flag}  But Still Shot");
                //return false;
            }

            if (base.EquipmentSource != null)
            {
                // Log.Message($"base.EquipmentSource != null");
                base.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
                base.EquipmentSource.GetComp<CompApparelVerbOwner_Charged>()?.UsedOnce();
            }

            lastShotTick = Find.TickManager.TicksGame;
            Thing manningPawn = caster;
            Thing equipmentSource = base.EquipmentSource;
            CompMannable compMannable = caster.TryGetComp<CompMannable>();
            if (compMannable?.ManningPawn != null)
            {
                // Log.Message($"compMannable?.ManningPawn != null");
                manningPawn = compMannable.ManningPawn;
                equipmentSource = caster;
            }

            Vector3 drawPos = caster.DrawPos;
            Projectile projectile2 = (Projectile)GenSpawn.Spawn(projectile, resultingLine.Source, caster.Map);
            if (verbProps.ForcedMissRadius > 0.5f)
            {
                // Log.Message($"verbProps.ForcedMissRadius > 0.5f");
                float num = verbProps.ForcedMissRadius;
                Pawn pawn;
                if ((pawn = manningPawn as Pawn) != null)
                {
                    num *= verbProps.GetForceMissFactorFor(equipmentSource, pawn);
                }

                float num2 = VerbUtility.CalculateAdjustedForcedMiss(num, currentTarget.Cell - caster.Position);
                if (num2 > 0.5f)
                {
                    // Log.Message($"num2 > 0.5f");
                    IntVec3 forcedMissTarget = GetForcedMissTarget(num2);
                    if (forcedMissTarget != currentTarget.Cell)
                    {
                        // Log.Message($"forcedMissTarget != currentTarget.Cell");
                        //ThrowDebugText("ToRadius");
                        //ThrowDebugText("Rad\nDest", forcedMissTarget);
                        ProjectileHitFlags projectileHitFlags = ProjectileHitFlags.NonTargetWorld;
                        if (Rand.Chance(0.5f))
                        {
                            // Log.Message($"Rand.Chance(0.5f)");
                            projectileHitFlags = ProjectileHitFlags.All;
                        }

                        if (!canHitNonTargetPawnsNow)
                        {
                            // Log.Message($"!canHitNonTargetPawnsNow");
                            projectileHitFlags &= ~ProjectileHitFlags.NonTargetPawns;
                        }

                        projectile2.Launch(manningPawn, drawPos, forcedMissTarget, currentTarget, projectileHitFlags, preventFriendlyFire, equipmentSource);
                        return true;
                    }
                }
            }
            // 脱靶的内置判断
            ShotReport shotReport = ShotReport.HitReportFor(caster, this, currentTarget);
            Thing randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
            ThingDef targetCoverDef = randomCoverToMissInto?.def;
            if (verbProps.canGoWild && !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture))
            {
                // Log.Message($"verbProps.canGoWild && !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture)");
                bool flyOverhead = projectile2?.def?.projectile != null && projectile2.def.projectile.flyOverhead;
                //resultingLine.ChangeDestToMissWild_NewTemp(shotReport.AimOnTargetChance_StandardTarget, flyOverhead, caster.Map);
                //ThrowDebugText("ToWild" + (canHitNonTargetPawnsNow ? "\nchntp" : ""));
                //ThrowDebugText("Wild\nDest", resultingLine.Dest);
                ProjectileHitFlags projectileHitFlags2 = ProjectileHitFlags.NonTargetWorld;
                if (Rand.Chance(0.5f) && canHitNonTargetPawnsNow)
                {
                    // Log.Message("Rand.Chance(0.5f) && canHitNonTargetPawnsNow");
                    projectileHitFlags2 |= ProjectileHitFlags.NonTargetPawns;
                }

                projectile2.Launch(manningPawn, drawPos, resultingLine.Dest, currentTarget, projectileHitFlags2, preventFriendlyFire, equipmentSource, targetCoverDef);
                return true;
            }

            if (currentTarget.Thing != null && currentTarget.Thing.def.CanBenefitFromCover && !Rand.Chance(shotReport.PassCoverChance))
            {
                // Log.Message("currentTarget.Thing != null && currentTarget.Thing.def.CanBenefitFromCover && !Rand.Chance(shotReport.PassCoverChance)");
                //ThrowDebugText("ToCover" + (canHitNonTargetPawnsNow ? "\nchntp" : ""));
                //ThrowDebugText("Cover\nDest", randomCoverToMissInto.Position);
                ProjectileHitFlags projectileHitFlags3 = ProjectileHitFlags.NonTargetWorld;
                if (canHitNonTargetPawnsNow)
                {
                    // Log.Message("canHitNonTargetPawnsNow");
                    projectileHitFlags3 |= ProjectileHitFlags.NonTargetPawns;
                }

                projectile2.Launch(manningPawn, drawPos, randomCoverToMissInto, currentTarget, projectileHitFlags3, preventFriendlyFire, equipmentSource, targetCoverDef);
                return true;
            }

            ProjectileHitFlags projectileHitFlags4 = ProjectileHitFlags.IntendedTarget;
            if (canHitNonTargetPawnsNow)
            {
                // Log.Message("canHitNonTargetPawnsNow2");
                projectileHitFlags4 |= ProjectileHitFlags.NonTargetPawns;
            }

            if (!currentTarget.HasThing || currentTarget.Thing.def.Fillage == FillCategory.Full)
            {
                // Log.Message("!currentTarget.HasThing || currentTarget.Thing.def.Fillage == FillCategory.Full");
                projectileHitFlags4 |= ProjectileHitFlags.NonTargetWorld;
            }

            //ThrowDebugText("ToHit" + (canHitNonTargetPawnsNow ? "\nchntp" : ""));
            if (currentTarget.Thing != null)
            {
                // Log.Message("currentTarget.Thing != null");
                projectile2.Launch(manningPawn, drawPos, currentTarget, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource, targetCoverDef);
                //ThrowDebugText("Hit\nDest", currentTarget.Cell);
            }
            else
            {
                projectile2.Launch(manningPawn, drawPos, resultingLine.Dest, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource, targetCoverDef);
                //ThrowDebugText("Hit\nDest", resultingLine.Dest);
            }

            return true;
        }
        public bool CustomTryFindShootLineFromTo(IntVec3 root, LocalTargetInfo targ, out ShootLine resultingLine, bool ignoreRange = false)
        {
            if (targ.HasThing && targ.Thing.Map != caster.Map)
            {
                resultingLine = default(ShootLine);
                return false;
            }

            if (verbProps.IsMeleeAttack || EffectiveRange <= 1.42f)
            {
                resultingLine = new ShootLine(root, targ.Cell);
                return ReachabilityImmediate.CanReachImmediate(root, targ, caster.Map, PathEndMode.Touch, null);
            }

            CellRect occupiedRect = (targ.HasThing ? targ.Thing.OccupiedRect() : CellRect.SingleCell(targ.Cell));
            if (!ignoreRange && OutOfRange(root, targ, occupiedRect))
            {
                resultingLine = new ShootLine(root, targ.Cell);
                return false;
            }

            if (!verbProps.requireLineOfSight)
            {
                resultingLine = new ShootLine(root, targ.Cell);
                return true;
            }

            IntVec3 goodDest;
            if (CasterIsPawn)
            {
                if (CanHitFromCellIgnoringRange(root, targ, out goodDest))
                {
                    resultingLine = new ShootLine(root, goodDest);
                    return true;
                }

                ShootLeanUtility.LeanShootingSourcesFromTo(root, occupiedRect.ClosestCellTo(root), caster.Map, tempLeanShootSources);
                for (int i = 0; i < tempLeanShootSources.Count; i++)
                {
                    IntVec3 intVec = tempLeanShootSources[i];
                    if (CanHitFromCellIgnoringRange(intVec, targ, out goodDest))
                    {
                        resultingLine = new ShootLine(intVec, goodDest);
                        return true;
                    }
                }
            }
            else
            {
                foreach (IntVec3 item in caster.OccupiedRect())
                {
                    if (CanHitFromCellIgnoringRange(item, targ, out goodDest))
                    {
                        resultingLine = new ShootLine(item, goodDest);
                        return true;
                    }
                }
            }

            resultingLine = new ShootLine(root, targ.Cell);
            return false;
        }

        private bool CanHitFromCellIgnoringRange(IntVec3 sourceCell, LocalTargetInfo targ, out IntVec3 goodDest)
        {
            if (targ.Thing != null)
            {
                if (targ.Thing.Map != caster.Map)
                {
                    goodDest = IntVec3.Invalid;
                    return false;
                }

                ShootLeanUtility.CalcShootableCellsOf(tempDestList, targ.Thing, sourceCell);
                for (int i = 0; i < tempDestList.Count; i++)
                {
                    if (CanHitCellFromCellIgnoringRange(sourceCell, tempDestList[i], targ.Thing.def.Fillage == FillCategory.Full))
                    {
                        goodDest = tempDestList[i];
                        return true;
                    }
                }
            }
            else if (CanHitCellFromCellIgnoringRange(sourceCell, targ.Cell))
            {
                goodDest = targ.Cell;
                return true;
            }

            goodDest = IntVec3.Invalid;
            return false;
        }

        private bool CanHitCellFromCellIgnoringRange(IntVec3 sourceSq, IntVec3 targetLoc, bool includeCorners = false)
        {
            if (verbProps.mustCastOnOpenGround && (!targetLoc.Standable(caster.Map) || caster.Map.thingGrid.CellContains(targetLoc, ThingCategory.Pawn)))
            {
                return false;
            }

            if (verbProps.requireLineOfSight)
            {
                if (!includeCorners)
                {
                    if (!GenSight.LineOfSight(sourceSq, targetLoc, caster.Map, skipFirstCell: true))
                    {
                        return false;
                    }
                }
                else if (!GenSight.LineOfSightToEdges(sourceSq, targetLoc, caster.Map, skipFirstCell: true))
                {
                    return false;
                }
            }

            return true;
        }


    }

    
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace HJ_SSR.Weapons
{

    public class CompEquippedGizmo : ThingComp
    {
        public virtual IEnumerable<Gizmo> CompGetGizmosEquipped()
        {
            yield return null;
        }
    }
    public abstract class DefModExtension_ShootUsingRandomProjectileBase : DefModExtension
    {
        public abstract ThingDef GetProjectile();


        public bool randomWithinBurst = false;
    }

    public class ModExtension_RandomBurstBreak : DefModExtension
    {
        public float chance = 0.08f;
        public IntRange randomBurst = new IntRange(0, 0);
    }

    public class ModExtension_Verb_Shotgun : DefModExtension
    {
        public int ShotgunPellets = 1;
        public ThingDef extraProjectile;
        public int extraProjectileCount = 1;

        // 新增散射参数
        public float spreadAngle = 10f;         // 默认散射角度 (度)
        public float minSpreadDistance = 1f;    // 最小散射距离 (格)
    }

    public class ModExtension_DropItemWhenFire : DefModExtension
    {
        public ThingDef Thingdef;
        public bool alwaysOnGround;
    }

    public class ModExtension_MultiShot : DefModExtension
    {
        public int shotCount;
    }

    /// <summary>
    /// 一次性
    /// </summary>
    public class ModExtension_OneUse : DefModExtension
    {
        public bool tryFindNewWeapon;
        public bool generateSidearm;
    }


    public class ModExtension_ProjOriginOffset : DefModExtension
    {
        public List<Vector2> offsets = new List<Vector2>();

        public Vector2 GetOffsetFor(int index)
        {
            if (offsets.NullOrEmpty()) return Vector2.zero;
            int i = index % offsets.Count;
            return offsets[i];
        }
    }
    public class DefModExtension_ShootUsingMechBattery : DefModExtension
    {
        public float energyConsumption = 0.001f;
    }
}

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace HJ_SSR.Weapons
{
    [DefOf]
    public static class EffecterDefOf
    {
        static EffecterDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(EffecterDefOf));
        }
        public static EffecterDef Effecter_SSR_PGHitTrunk;
        public static EffecterDef Effecter_SSR_PGHitTree;
    }
}

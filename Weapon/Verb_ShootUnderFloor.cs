using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace HJ_SSR.Weapons
{
    public class ModExtension_VerbNotUnderRoof : DefModExtension
    {
        public bool appliesInPrimaryMode = true;
        public bool appliesInSecondaryMode = true;

        public bool oneUseTryFindNewWeapon;
        public bool generateSidearm;
    }
}

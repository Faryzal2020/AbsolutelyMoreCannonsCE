using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Main mod class for Turret Barrel Animation system.
    /// </summary>
    public class TurretBarrelAnimationMod : Mod
    {
        public TurretBarrelAnimationMod(ModContentPack content) : base(content)
        {
            // Harmony patches are initialized via [StaticConstructorOnStartup] attribute
            Log.Message("Turret Barrel Animation mod loaded successfully.");
        }
    }
}

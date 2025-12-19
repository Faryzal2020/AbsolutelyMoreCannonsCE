using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Main mod class for Absolutely More Cannons - CE Version.
    /// Provides settings UI for debug logging configuration.
    /// </summary>
    public class TurretBarrelAnimationMod : Mod
    {
        public static AMCSettings settings;

        public TurretBarrelAnimationMod(ModContentPack content) : base(content)
        {
            // Initialize settings
            settings = GetSettings<AMCSettings>();
            
            // Harmony patches are initialized via [StaticConstructorOnStartup] attribute
            if (settings.logStartup)
            {
                Log.Message("[AMC] Absolutely More Cannons - CE Version loaded successfully.");
            }
        }

        /// <summary>
        /// The mod's settings window name
        /// </summary>
        public override string SettingsCategory()
        {
            return "Absolutely More Cannons";
        }

        /// <summary>
        /// Draw the settings window
        /// </summary>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            settings.DoSettingsWindowContents(inRect);
            base.DoSettingsWindowContents(inRect);
        }
    }
}

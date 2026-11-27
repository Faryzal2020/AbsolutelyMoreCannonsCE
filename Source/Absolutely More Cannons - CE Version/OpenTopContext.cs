using System;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Short-lived flags that tell damage patches where a hit came from.
    /// InBlast is set around DamageWorker.ExplosionDamageThing (blast damage, not a projectile impact).
    /// </summary>
    internal static class OpenTopContext
    {
        [ThreadStatic] public static bool InBlast;
    }
}

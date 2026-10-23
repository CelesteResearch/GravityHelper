using Celeste.Scene.Entities;
using MonoMod.Utils;

namespace Celeste.Mods.GravityHelper.Runtime
{
    public static class GravityEntityInversion
    {
        public static bool ShouldInvert(SceneEntityState entity) =>
            DynamicData.For(entity).Data.TryGetValue("GravityHelper_Inverted", out var value) && (bool)value;
    }
}

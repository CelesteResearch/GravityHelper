// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using Celeste.Core.Reflection.Runtime;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using MonoMod.Utils;

namespace Celeste.Mods.GravityHelper.Extensions
{
    [RuntimeTypeAlias("Celeste.Mod.GravityHelper.Extensions.EntityExtensions")]
    public static class EntityExtensions
    {
        public static void SetShouldInvert(this SceneEntityState entity, bool invert)
            => DynamicData.For(entity).Data["GravityHelper_Inverted"] = invert;

        public static bool ShouldInvert(this SceneEntityState entity)
            => GravityEntityInversion.ShouldInvert(entity);

        public static bool ShouldInvertChecked(this SceneEntityState entity)
            => entity is RoomPlayerSession player
                ? GravityRuntimeContext.For(player.EngineContext).PlayerComponent?.ShouldInvertChecked ?? false
                : entity.Get<GravityComponent>()?.ShouldInvertChecked ?? false;

        public static bool SetGravity(this SceneEntityState entity, GravityType gravityType,
            float momentumMultiplier = 1f)
            => entity?.Get<GravityComponent>()?.SetGravity(gravityType, momentumMultiplier) == true;

        public static GravityType GetGravity(this SceneEntityState entity)
            => entity?.Get<GravityComponent>()?.CurrentGravity ?? GravityType.Normal;
    }
}

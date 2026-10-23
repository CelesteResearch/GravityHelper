using Celeste.Mods.GravityHelper.Entities.Controllers;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Triggers;
using Celeste.Scene.Tracking;
using Celeste.Physics.Collision;
using Celeste.Simulation;

namespace Celeste.Mods.GravityHelper.Runtime
{
    public static class GravityEntityQueries
    {
        public static bool DashToToggle(RoomPlayerSession player) =>
            GravityControllerQueries.GetActive<BehaviorGravityController>(player.Scene)?.DashToToggle ?? false;

        public static bool IsGravityForced(RoomPlayerSession player, GravityType current)
        {
            if (!((ISourceScene)player.Scene).SourceScene.Entities.TryGetTrackedEntities(typeof(GravityTrigger),
                    out var list)) return false;
            foreach (var item in list)
                if (item is GravityTrigger t && t.AffectsPlayer && t.GravityType == current &&
                    SourceEntityCollision.Check(player, t))
                    return true;
            return false;
        }

        public static SpawnGravityTrigger FirstSpawn(RoomPlayerSession player)
        {
            if (!((ISourceScene)player.Scene).SourceScene.Entities.TryGetTrackedEntities(typeof(SpawnGravityTrigger),
                    out var list)) return null;
            foreach (var item in list)
                if (item is SpawnGravityTrigger t && SourceEntityCollision.Check(player, t))
                    return t;
            return null;
        }
    }
}

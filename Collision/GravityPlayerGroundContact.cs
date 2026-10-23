// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System.Numerics;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Vertical;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.GravityHelper.Collision
{
    internal static class GravityPlayerGroundContact
    {
        internal static SourceJumpThruEntityState FirstJumpThru(RoomPlayerSession player, Vector2 at, bool inverted,
            IGravityJumpThruContactQueries queries)
            => queries.First(player, at, inverted, true);

        internal static SourcePlatformEntityState FirstPlatform(RoomPlayerSession player, bool inverted,
            IGravityJumpThruContactQueries queries)
        {
            Vector2 at = player.Position + (inverted ? -Vector2.UnitY : Vector2.UnitY);
            return SourceTrackedEntityCollision.FirstAt<SourcePlatformEntityState>(
                       ((ISourceScene)player.Scene).SourceScene.Entities, "Celeste.Solid", player, at)
                   ?? FirstJumpThru(player, at, inverted, queries);
        }

        internal static bool RefillGround(RoomPlayerSession player, bool inverted,
            IGravityJumpThruContactQueries queries)
        {
            Vector2 at = player.Position + (inverted ? -Vector2.UnitY : Vector2.UnitY);
            return SourceTrackedEntityCollision.CheckAtExcept(((ISourceScene)player.Scene).SourceScene.Entities,
                       "Celeste.Solid", "Celeste.NegaBlock", player, at) ||
                   FirstJumpThru(player, at, inverted, queries) != null;
        }
    }
}

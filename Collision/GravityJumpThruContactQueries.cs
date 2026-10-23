using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Mods.GravityHelper.Entities.Platforms;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.GravityHelper.Collision
{
    public sealed class
        GravityJumpThruContactQueries : Celeste.Mods.GravityHelper.Runtime.IGravityJumpThruContactQueries
    {
        private Type maxUpsideDownType;

        public GravityJumpThruContactQueries(Type maxUpsideDownType)
        {
            this.maxUpsideDownType = maxUpsideDownType;
        }

        public void RefreshOptionalTypes(Type maxUpsideDownType)
        {
            this.maxUpsideDownType = maxUpsideDownType;
        }

        public bool IsUpsideDown(Celeste.Physics.Collision.Riding.ISourceJumpThruCollider platform)
            => platform is UpsideDownJumpThru || platform.GetType() == maxUpsideDownType;

        public SourceJumpThruEntityState First(SceneEntityState entity, Vector2 at, bool inverted,
            bool outside = false)
        {
            return inverted ? FirstUpsideDown(entity, at, outside) : FirstNormal(entity, at, outside);
        }

        public SourceJumpThruEntityState FirstCurrent(SceneEntityState entity, bool inverted)
            => inverted
                ? FirstUpsideDownCore(entity, entity.Position, false, true)
                : FirstNormalCore(entity, entity.Position, false, true);

        public SourceJumpThruEntityState FirstUpsideDown(SceneEntityState entity, Vector2 at, bool outside = false)
            => FirstUpsideDownCore(entity, at, outside, false);

        private SourceJumpThruEntityState FirstUpsideDownCore(SceneEntityState entity, Vector2 at, bool outside,
            bool current)
        {
            SceneEntityRegistry tracker = ((ISourceScene)entity.Scene).SourceScene.Entities;
            if (tracker.TryGetTrackedEntities(typeof(UpsideDownJumpThru), out List<object> gravityPlatforms))
            {
                SourceJumpThruEntityState result = FirstInBucket(entity, at, gravityPlatforms, outside, false, current);
                if (result != null) return result;
            }

            if (maxUpsideDownType != null &&
                tracker.TryGetTrackedEntities(maxUpsideDownType, out List<object> maxPlatforms))
                return FirstInBucket(entity, at, maxPlatforms, outside, false, current);
            return null;
        }

        public SourceJumpThruEntityState FirstNormal(SceneEntityState entity, Vector2 at, bool outside = false)
            => FirstNormalCore(entity, at, outside, false);

        private SourceJumpThruEntityState FirstNormalCore(SceneEntityState entity, Vector2 at, bool outside,
            bool current)
        {
            SceneEntityRegistry tracker = ((ISourceScene)entity.Scene).SourceScene.Entities;
            if (outside)
                return FirstInBucket(entity, at, tracker.GetTrackedEntities("Celeste.JumpThru"), true, true, false);
            if (!tracker.TryGetTrackedEntities(typeof(SourceJumpThruEntityState), out List<object> platforms))
                return null;
            return FirstInBucket(entity, at, platforms, false, true, current);
        }

        private SourceJumpThruEntityState FirstInBucket(SceneEntityState entity, Vector2 at, List<object> platforms,
            bool outside, bool excludeUpsideDown, bool current)
        {
            SourceJumpThruEntityState first = null;
            foreach (object item in platforms)
            {
                var platform = (SourceJumpThruEntityState)item;
                if (outside && excludeUpsideDown &&
                    (platform.GetType() == maxUpsideDownType || platform is UpsideDownJumpThru)) continue;
                bool collides = (!outside || !SourceEntityCollision.Check(entity, platform)) &&
                                (current
                                    ? SourceEntityCollision.Check(entity, platform)
                                    : SourceEntityCollision.CheckAt(entity, platform, at));
                if (!collides || excludeUpsideDown && platform.GetType() == maxUpsideDownType) continue;
                if (!excludeUpsideDown || outside) return platform;
                first ??= platform;
            }

            return first;
        }
    }
}

// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Linq.Expressions;
using Celeste.Everest.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;

namespace Celeste.Mods.GravityHelper.Runtime
{
    internal sealed class GravityOptionalEntityCallbacks
    {
        private readonly Type inversionBlock;
        private readonly Type vvvvvvTrigger;
        private readonly Func<SceneEntityState, RoomPlayerSession, bool> tryHandle;
        private readonly Func<SceneEntityState, bool> triggerEnabled;

        public GravityOptionalEntityCallbacks(ISourceEngineContext engine)
        {
            var types = ((ISourceRuntimeTypeDiscoveryEnvironment)engine).RuntimeTypeDiscovery;
            inversionBlock = types.ResolveRuntimeTypeAlias("Celeste.Mod.GravityHelper.Entities.InversionBlock");
            vvvvvvTrigger = types.ResolveRuntimeTypeAlias("Celeste.Mod.GravityHelper.Triggers.VvvvvvTrigger");
            if (inversionBlock != null)
            {
                var entity = Expression.Parameter(typeof(SceneEntityState));
                var player = Expression.Parameter(typeof(RoomPlayerSession));
                tryHandle = Expression.Lambda<Func<SceneEntityState, RoomPlayerSession, bool>>(
                    Expression.Call(Expression.Convert(entity, inversionBlock),
                        inversionBlock.GetMethod("TryHandlePlayer", [typeof(RoomPlayerSession)]), player), entity,
                    player).Compile();
            }

            if (vvvvvvTrigger != null)
            {
                var entity = Expression.Parameter(typeof(SceneEntityState));
                triggerEnabled = Expression.Lambda<Func<SceneEntityState, bool>>(
                    Expression.PropertyOrField(Expression.Convert(entity, vvvvvvTrigger), "Enable"), entity).Compile();
            }
        }

        public bool HandleInversionBlocks(RoomPlayerSession player)
        {
            if (inversionBlock == null) return false;
            foreach (SceneEntityState entity in ((ISourceScene)player.Scene).SourceScene.Entities
                     .GetEntitiesTrackIfNeeded(inversionBlock))
                if (tryHandle(entity, player))
                    return true;
            return false;
        }

        public bool VvvvvvAtSpawn(RoomPlayerSession player)
        {
            if (vvvvvvTrigger == null) return false;
            foreach (SceneEntityState entity in ((ISourceScene)player.Scene).SourceScene.Entities
                     .GetEntitiesTrackIfNeeded(vvvvvvTrigger))
                if (SourceEntityCollision.Check(player, entity))
                    return triggerEnabled(entity);
            return false;
        }
    }
}

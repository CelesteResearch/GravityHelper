using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.GravityHelper.Metadata;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Simulation;

namespace Celeste.Mods.GravityHelper.Entities.Controllers
{
    public static class GravityControllerQueries
    {
        public static T GetActive<T>(object scene, bool allowEphemeral = false) where T : BaseGravityController
        {
            if (scene is not ISourceScene source) return null;
            if (!source.SourceScene.Entities.TryGetTrackedEntities(typeof(T), out var list) ||
                list.Count == 0) return null;
            T global = null;
            foreach (var item in list)
            {
                if (item is not T controller) continue;
                if (controller.Persistent && (!controller.Ephemeral || allowEphemeral)) global = controller;
                if (scene is OriginalLevelSceneRoot level && level.IsInBounds(controller.Position)) return controller;
            }

            return global;
        }

        public static T GetPersistent<T>(OriginalLevelSceneRoot scene, bool createIfRequired = false)
            where T : BaseGravityController
        {
            var found = scene.Scheduler.Entities.TryGetTrackedEntities(typeof(T), out var list);
            if (!found && !createIfRequired) return null;
            if (found)
                foreach (var item in list)
                    if (item is T { Persistent: true } persistent)
                        return persistent;
            if (createIfRequired || (list?.Count ?? 0) > 0)
            {
                var controller = (T)Activator.CreateInstance(typeof(T), scene.EngineContext);
                scene.Scheduler.QueueSourceEntity(controller);
                scene.Scheduler.CommitQueuedEntityLists();
                return controller;
            }

            return null;
        }
    }
}

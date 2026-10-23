// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Linq;
using Celeste.Core.Maps;
using Celeste.Everest.Runtime.Levels.Loading.Entities;
using Celeste.Game.Content.Maps;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Mods.GravityHelper.Entities.Controllers;
using Celeste.Mods.GravityHelper.Gravity;

namespace Celeste.Mods.GravityHelper.Hooks.Levels
{
    public sealed partial class GravityLevelLifecycleHooks
    {
        private OriginalLevelEntityData[] PreloadControllers(OriginalLevelSceneRoot self)
        {
            var map = (OriginalChapterMapData)self.Session.MapData;
            var entities = map?.Levels?.SelectMany(room => room.Entities) ?? [];
            var controllers = entities
                .Where(data => data.Name.StartsWith("GravityHelper") && data.Name.Contains("Controller")).ToArray();
            foreach (var group in controllers.GroupBy(data => data.Name))
            {
                int persistentCount = group.Count(data => data.Bool("persistent"));
                if (persistentCount > 1)
                    System.Diagnostics.Trace.TraceWarning(
                        $"Warning: Found {persistentCount} persistent controllers of type {group.Key}");
                foreach (var roomGroup in group.GroupBy(data => data.Level.Name))
                {
                    int count = roomGroup.Count();
                    if (count > 1)
                        System.Diagnostics.Trace.TraceWarning(
                            $"Warning: Found {count} controllers of type {group.Key} in room {roomGroup.Key}");
                }
            }

            foreach (var data in controllers)
            {
// Explicit presence controls preload even when its value is false.
                if (!data.Has("persistent")) continue;
                self.Session.DoNotLoad.Add(new MapEntityId(data.Level.Name, data.ID));
                ((IEverestLevelCustomEntityLoaderEnvironment)runtime.Engine).CustomEntities
                    .LoadCustomEntity(data, self);
            }

            return controllers;
        }

        private void ApplyControllers(OriginalLevelSceneRoot self, OriginalLevelEntityData[] controllers)
        {
            var map = (OriginalChapterMapData)self.Session.MapData;
            var triggers = map?.Levels?.SelectMany(room => room.Triggers) ?? [];
            bool hasVvvvvvTriggers = triggers.Any(data => data.Name == "GravityHelper/VvvvvvTrigger");
            bool hasCassetteControllers =
                controllers.Any(data => data.Name == "GravityHelper/CassetteGravityController");
            GravityControllerQueries.GetPersistent<BehaviorGravityController>(self)?.Transitioned();
            GravityControllerQueries.GetPersistent<SoundGravityController>(self)?.Transitioned();
            GravityControllerQueries.GetPersistent<VisualGravityController>(self)?.Transitioned();
            GravityControllerQueries.GetPersistent<CassetteGravityController>(self, hasCassetteControllers)
                ?.Transitioned();
            var vvvvvv = GravityControllerQueries.GetPersistent<VvvvvvGravityController>(self,
                hasVvvvvvTriggers || runtime.Settings.VvvvvvMode != VvvvvvSetting.Default);
            if (vvvvvv != null && vvvvvv.Ephemeral && hasVvvvvvTriggers) vvvvvv.Mode = VvvvvvMode.TriggerBased;
            vvvvvv?.Transitioned();
        }
    }
}

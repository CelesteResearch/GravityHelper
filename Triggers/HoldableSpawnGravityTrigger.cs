// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Mods.GravityHelper.Metadata;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;

namespace Celeste.Mods.GravityHelper.Triggers
{
    [CustomEntity("GravityHelper/HoldableSpawnGravityTrigger = Create")]
    [RuntimeTypeAlias("Celeste.Mod.GravityHelper.Triggers.HoldableSpawnGravityTrigger")]
    [SceneTracked]
    public class HoldableSpawnGravityTrigger : SceneEntityState
    {
        private readonly VersionInfo modVersion, pluginVersion;

        public static HoldableSpawnGravityTrigger Create(Celeste.Game.Execution.Levels.OriginalLevelSceneRoot level,
            Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData room, Vector2 offset,
            Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        public HoldableSpawnGravityTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            modVersion = data.ModVersion();
            pluginVersion = data.PluginVersion();
            Collider = new HitboxColliderGeometry(data.Width, data.Height);
            Visible = Active = false;
        }
    }
}

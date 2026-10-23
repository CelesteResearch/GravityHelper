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

namespace Celeste.Mods.GravityHelper.Triggers
{
    [Celeste.Everest.Modules.Entities.Metadata.CustomEntity("GravityHelper/SpawnGravityTrigger = Create")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.GravityHelper.Triggers.SpawnGravityTrigger")]
    [SceneTracked]
    public class SpawnGravityTrigger : SceneEntityState
    {
        public static SpawnGravityTrigger Create(Celeste.Game.Execution.Levels.OriginalLevelSceneRoot level,
            Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData room, Vector2 offset,
            Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        public GravityType GravityType { get; }
        public bool FireOnBubbleReturn { get; }

        // ReSharper disable NotAccessedField.Local
        private readonly VersionInfo _modVersion;
        private readonly VersionInfo _pluginVersion;
        // ReSharper restore NotAccessedField.Local

        public SpawnGravityTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            _modVersion = data.ModVersion();
            _pluginVersion = data.PluginVersion();

            GravityType = (GravityType)data.Int("gravityType");
            FireOnBubbleReturn = data.Bool("fireOnBubbleReturn", true);
            Collider = new Celeste.Physics.Collision.Shapes.HitboxColliderGeometry(data.Width, data.Height);
            Visible = Active = false;
        }
    }
}

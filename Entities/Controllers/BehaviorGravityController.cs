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
    [Celeste.Everest.Modules.Entities.Metadata.CustomEntity("GravityHelper/BehaviorGravityController = Create")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.GravityHelper.Entities.Controllers.BehaviorGravityController")]
    [SceneTracked]
    public class BehaviorGravityController : BaseGravityController<BehaviorGravityController>
    {
        public static BehaviorGravityController Create(Celeste.Game.Execution.Levels.OriginalLevelSceneRoot level,
            Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData room, Vector2 offset,
            Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        public const float DEFAULT_HOLDABLE_RESET_TIME = 2f;
        public const float DEFAULT_SPRING_COOLDOWN_V1 = 0.1f;
        public const float DEFAULT_SPRING_COOLDOWN_V2 = 0f;
        public const float DEFAULT_SWITCH_COOLDOWN = 1f;

        public float HoldableResetTime { get; } = DEFAULT_HOLDABLE_RESET_TIME;
        public float SpringCooldown { get; } = DEFAULT_SPRING_COOLDOWN_V2;
        public float SwitchCooldown { get; } = DEFAULT_SWITCH_COOLDOWN;
        public bool SwitchOnHoldables { get; } = true;
        public bool DashToToggle { get; }
        public string ToggleFlag { get; }

        // ReSharper disable NotAccessedField.Local
        private readonly ISourceEngineContext engine;
        private readonly VersionInfo _modVersion;
        private readonly VersionInfo _pluginVersion;
        // ReSharper restore NotAccessedField.Local

        // ReSharper disable once UnusedMember.Global
        public BehaviorGravityController(ISourceEngineContext engine) : base(engine)
        {
            this.engine = engine;
            // ephemeral controller
        }

        // ReSharper disable once UnusedMember.Global
        public BehaviorGravityController(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(engine, data, offset)
        {
            this.engine = engine;
            _modVersion = data.ModVersion();
            _pluginVersion = data.PluginVersion();

            var defaultSpringCooldown =
                _pluginVersion.Major >= 2 ? DEFAULT_SPRING_COOLDOWN_V2 : DEFAULT_SPRING_COOLDOWN_V1;

            HoldableResetTime = Math.Max(data.Float("holdableResetTime", DEFAULT_HOLDABLE_RESET_TIME), 0f);
            SpringCooldown = Math.Max(data.Float("springCooldown", defaultSpringCooldown), 0f);
            SwitchCooldown = Math.Max(data.Float("switchCooldown", DEFAULT_SWITCH_COOLDOWN), 0f);
            SwitchOnHoldables = data.Bool("switchOnHoldables", true);
            DashToToggle = data.Bool("dashToToggle");
            ToggleFlag = data.Attr("toggleFlag");
        }

        public override void Update()
        {
            base.Update();
            if (!Persistent || (Scene as OriginalLevelSceneRoot) is not { } level) return;

            if (!string.IsNullOrWhiteSpace(ToggleFlag) && level.Session.GetFlag(ToggleFlag))
            {
                level.Session.SetFlag(ToggleFlag, false);
                GravityRuntimeContext.For(engine).PlayerComponent?.SetGravity(GravityType.Toggle);
            }
        }
    }
}

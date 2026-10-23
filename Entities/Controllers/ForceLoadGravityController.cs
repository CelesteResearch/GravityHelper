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
    [SceneTracked]
    [Celeste.Everest.Modules.Entities.Metadata.CustomEntity("GravityHelper/ForceLoadGravityController = Create")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.GravityHelper.Entities.Controllers.ForceLoadGravityController")]
    public class ForceLoadGravityController : BaseGravityController<ForceLoadGravityController>
    {
        public static ForceLoadGravityController Create(Celeste.Game.Execution.Levels.OriginalLevelSceneRoot level,
            Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData room, Vector2 offset,
            Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        public ForceLoadGravityController(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(engine,
            data,
            offset)
        {
        }
    }
}

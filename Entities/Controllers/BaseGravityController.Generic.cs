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
    public abstract class BaseGravityController<TController> : BaseGravityController
        where TController : BaseGravityController
    {
        protected BaseGravityController(ISourceEngineContext engine) : base(engine)
        {
        }

        protected BaseGravityController(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(engine, data, offset)
        {
        }

        public TController ActiveController =>
            GravityControllerQueries.GetActive<TController>(Scene) ?? this as TController;
    }
}

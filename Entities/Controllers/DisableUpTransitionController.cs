using Celeste.Scene.Tracking;
// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Scene.Entities;
using Celeste.Core.Components;
using Celeste.Scene.Tracking.Attributes;

namespace Celeste.Mods.GravityHelper.Entities.Controllers
{
    [SceneTracked]
    [CustomEntity("GravityHelper/DisableUpTransitionController")]
    public sealed class DisableUpTransitionController : SceneEntityState
    {
        public DisableUpTransitionController(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
        }
    }
}

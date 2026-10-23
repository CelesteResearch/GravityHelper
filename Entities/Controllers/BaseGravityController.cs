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
    public abstract class BaseGravityController : SceneEntityState
    {
        protected readonly VersionInfo ModVersion;
        protected readonly VersionInfo PluginVersion;

        /// <summary>
        /// Whether or not this is the master controller that should handle everything for this type.
        /// Get the persistent controller with Scene.GetPersistentController if you want to call a method.
        /// Get the active controller with Scene.GetActiveController if you want to read a property.
        /// </summary>
        public bool Persistent { get; }

        public bool Ephemeral { get; }

        protected BaseGravityController(ISourceEngineContext engine)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            ModVersion = new VersionInfo(0, 1);
            PluginVersion = new VersionInfo(0, 1);

            Visible = Collidable = false;
            Ephemeral = Active = Persistent = true;

            AddTag(((Celeste.Game.Execution.Startup.IOriginalGameTagsEnvironment)engine).Tags.Global.Value);

            Add(new RoomTransitionListener
            {
                OnOutBegin = Transitioned,
            });
        }

        protected BaseGravityController(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            ModVersion = data.ModVersion();
            PluginVersion = data.PluginVersion();
            Position = ((Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData)data).Level.Position +
                       new Vector2(16, 16);

            Visible = Collidable = false;
            Active = Persistent = data.Bool("persistent", true);

            AddTag(((Celeste.Game.Execution.Startup.IOriginalGameTagsEnvironment)engine).Tags.Global.Value);

            if (Persistent)
            {
                // note that we must use OnOutBegin since OnInBegin will only be called on
                // entities that are part of the newly loaded room
                Add(new RoomTransitionListener
                {
                    OnOutBegin = Transitioned,
                });
            }
        }

        public virtual void Transitioned()
        {
        }
    }
}

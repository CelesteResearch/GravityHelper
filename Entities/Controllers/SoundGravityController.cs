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
    [Celeste.Everest.Modules.Entities.Metadata.CustomEntity("GravityHelper/SoundGravityController = Create")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.GravityHelper.Entities.Controllers.SoundGravityController")]
    [SceneTracked]
    public class SoundGravityController : BaseGravityController<SoundGravityController>
    {
        public static SoundGravityController Create(Celeste.Game.Execution.Levels.OriginalLevelSceneRoot level,
            Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData room, Vector2 offset,
            Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        private readonly ISourceEngineContext engine;
        public const string DEFAULT_NORMAL_SOUND = "event:/ui/game/lookout_off";
        public const string DEFAULT_INVERTED_SOUND = "event:/ui/game/lookout_on";
        public const string DEFAULT_TOGGLE_SOUND = "";

        public string NormalSound { get; } = DEFAULT_NORMAL_SOUND;
        public string InvertedSound { get; } = DEFAULT_INVERTED_SOUND;
        public string ToggleSound { get; } = DEFAULT_TOGGLE_SOUND;
        public string LineSound { get; } = "event:/gravityhelper/gravity_line";
        public string InversionBlockSound { get; } = "event:/char/badeline/disappear";
        public string MusicParam { get; } = string.Empty;
        public string SingleUseFieldSound { get; } = "event:/new_content/game/10_farewell/glider_emancipate";

        // ReSharper disable once UnusedMember.Global
        public SoundGravityController(ISourceEngineContext engine) : base(engine)
        {
            this.engine = engine;
            // ephemeral controller
        }

        // ReSharper disable once UnusedMember.Global
        public SoundGravityController(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(engine, data, offset)
        {
            this.engine = engine;
            NormalSound = data.Attr("normalSound", NormalSound);
            InvertedSound = data.Attr("invertedSound", InvertedSound);
            ToggleSound = data.Attr("toggleSound", ToggleSound);
            LineSound = data.Attr("lineSound", LineSound);
            InversionBlockSound = data.Attr("inversionBlockSound", InversionBlockSound);
            MusicParam = data.Attr("musicParam", MusicParam);
            SingleUseFieldSound = data.Attr("singleUseFieldSound", SingleUseFieldSound);

            if (Persistent)
            {
                Add(new PlayerGravityListener(GravityRuntimeContext.For(engine))
                {
                    GravityChanged = (_, args) =>
                    {
                        var active = ActiveController;
                        setParam(active.MusicParam);
                    },
                });
            }
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            if (!Persistent) return;

            setParam(ActiveController.MusicParam);
        }

        private void setParam(string param)
        {
            if (!string.IsNullOrEmpty(param))
                ((Celeste.Game.Execution.Processes.IOriginalGameplayProcess)engine).AudioSession.SetMusicParam(param,
                    GravityRuntimeContext.For(engine).PlayerComponent?.ShouldInvert == true ? 1 : 0);
        }
    }
}

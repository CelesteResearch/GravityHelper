using System;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Platforms.Cassette;
using Celeste.Mods.GravityHelper.Components.Cassette;
using Celeste.Mods.GravityHelper.Entities.Indicators;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Metadata;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;

namespace Celeste.Mods.GravityHelper.Entities.Controllers
{
    [CustomEntity("GravityHelper/CassetteGravityController = Create")]
    [SceneTracked]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.GravityHelper.Entities.Controllers.CassetteGravityController")]
    public class CassetteGravityController : BaseGravityController<CassetteGravityController>
    {
        private readonly GravityRuntimeContext runtime;
        private readonly CassetteListener _cassetteListener;
        public GravityType[] CassetteSequence { get; }
        public float MomentumMultiplier { get; }
        public bool InstantFlip { get; }

        public static CassetteGravityController Create(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        public CassetteGravityController(ISourceEngineContext engine) : base(engine)
        {
            runtime = GravityRuntimeContext.For(engine);
            CassetteSequence = Array.Empty<GravityType>();
            MomentumMultiplier = 1f;
        }

        public CassetteGravityController(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(engine,
            data,
            offset)
        {
            runtime = GravityRuntimeContext.For(engine);
            CassetteSequence = data.Attr("cassetteSequence").Replace("|", ",").Split(',').Select(s =>
            {
                if (!int.TryParse(s, out var value)) return default;
                var t = (GravityType)value;
                return t >= GravityType.None && t <= GravityType.Toggle ? t : default;
            }).ToArray();
            MomentumMultiplier = data.Float("momentumMultiplier", 1f);
            InstantFlip = data.Bool("instantFlip");
            if (CassetteSequence.Length > 0)
                Add(_cassetteListener = new CassetteListener
                    { Enabled = false, WillToggle = WillToggle, DidBecomeActive = DidBecomeActive });
        }

        private void WillToggle(int index, bool activate)
        {
            if (index >= CassetteSequence.Length || !activate) return;
            var type = CassetteSequence[index];
            if (Scene is not ISourceScene scene) return;
            OriginalCassetteBlockManagerState manager = null;
            var all = scene.SourceScene.Entities.Entities;
            for (int i = 0; i < all.Count; i++)
                if (all[i] is OriginalCassetteBlockManagerState m)
                {
                    manager = m;
                    break;
                }

            float tempo = manager != null ? GravityCassetteFields.Tempo(manager) : 1f;
            if (scene.SourceScene.Entities.TryGetTrackedEntities(typeof(GravityIndicator), out var indicators))
                for (int i = 0; i < indicators.Count; i++)
                {
                    var indicator = (GravityIndicator)indicators[i];
                    if (!indicator.SyncToPlayer)
                    {
                        indicator.TurnTarget = type;
                        indicator.TurnTime = tempo * 10f / 60f;
                    }
                }
        }

        private void DidBecomeActive(int index)
        {
            if (index >= CassetteSequence.Length) return;
            var type = CassetteSequence[index];
            if (type != GravityType.None && runtime.PlayerComponent is { } component &&
                component.Entity is RoomPlayerSession player &&
                !GravityControllerPlayerFacts.IsIntroState(player))
                component.SetGravity(type, MomentumMultiplier, instant: InstantFlip);
        }

        public override void Transitioned()
        {
            if (!Persistent || Scene is not OriginalLevelSceneRoot level) return;
            var active = ActiveController;
            if (level.Scheduler.Entities.TryGetTrackedEntities(typeof(CassetteGravityController), out var controllers))
                for (int i = 0; i < controllers.Count; i++)
                {
                    var c = (CassetteGravityController)controllers[i];
                    if (c._cassetteListener != null) c._cassetteListener.Enabled = c == active;
                }

            if (active?._cassetteListener == null) return;
            level.CassetteBlockBeats = Math.Max(level.CassetteBlockBeats, active.CassetteSequence.Length);
            level.HasCassetteBlocks = true;
            CassetteComponent.EnsureManager(level);
        }
    }
}

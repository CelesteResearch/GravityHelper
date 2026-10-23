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
using Celeste.Simulation.Gameplay.Triggers.Foundation;
using Celeste.Mods.GravityHelper.Entities.Controllers;

namespace Celeste.Mods.GravityHelper.Triggers
{
    [Celeste.Everest.Modules.Entities.Metadata.CustomEntity("GravityHelper/GravityTrigger = Create")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.GravityHelper.Triggers.GravityTrigger")]
    [SceneTracked(true)]
    public class GravityTrigger : SourceTriggerEntityState
    {
        public static GravityTrigger Create(Celeste.Game.Execution.Levels.OriginalLevelSceneRoot level,
            Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData room, Vector2 offset,
            Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        protected readonly GravityRuntimeContext runtime;
        private const float audio_muffle_seconds = 0.2f;

        public bool AffectsPlayer { get; }
        public bool AffectsHoldableActors { get; }
        public bool AffectsOtherActors { get; }
        public string EnableFlag { get; }
        public bool InvertEnableFlag { get; }
        public string VisibleFlag { get; }
        public bool InvertVisibleFlag { get; }

        public GravityType GravityType
        {
            get => _gravityType;
            protected set
            {
                _gravityType = value;
                if (Get<GravityTriggerComponent>() is { } gravityTriggerComponent)
                    gravityTriggerComponent.GravityType = value;
            }
        }

        public GravityType ExitGravityType { get; }
        public float MomentumMultiplier { get; }
        public virtual bool ShouldAffectPlayer => true;
        protected bool AffectsNothing => !AffectsPlayer && !AffectsHoldableActors && !AffectsOtherActors;

        // ReSharper disable NotAccessedField.Local
        private readonly VersionInfo _modVersion;
        private readonly VersionInfo _pluginVersion;
        // ReSharper restore NotAccessedField.Local

        private readonly bool _defaultToController;
        private string _normalSound;
        private string _invertedSound;
        private string _toggleSound;
        private readonly string _forceSound;
        private string _exitSound;
        private float _audioMuffleSecondsRemaining;
        private GravityType _gravityType;

        private string sound => !string.IsNullOrWhiteSpace(_forceSound)
            ? _forceSound
            : GravityType switch
            {
                GravityType.Normal => _normalSound,
                GravityType.Inverted => _invertedSound,
                GravityType.Toggle => _toggleSound,
                _ => string.Empty,
            };

        public GravityTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
                data.Width, data.Height,
                ((Celeste.Core.Mathematics.Extrema.ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            runtime = GravityRuntimeContext.For(engine);
            _modVersion = data.ModVersion();
            _pluginVersion = data.PluginVersion();

            AffectsPlayer = data.Bool("affectsPlayer", true);
            AffectsHoldableActors = data.Bool("affectsHoldableActors");
            AffectsOtherActors = data.Bool("affectsOtherActors");
            GravityType = (GravityType)data.Int("gravityType");
            ExitGravityType = (GravityType)data.Int("exitGravityType", (int)GravityType.None);
            MomentumMultiplier = data.Float("momentumMultiplier", 1f);
            EnableFlag = data.Attr("enableFlag")?.Trim();
            InvertEnableFlag = data.Bool("invertEnableFlag");
            VisibleFlag = data.Attr("visibleFlag")?.Trim();
            InvertVisibleFlag = data.Bool("invertVisibleFlag");

            _defaultToController = data.Bool("defaultToController", true);
            _forceSound = data.Attr("sound", string.Empty);

            TriggeredEntityTypes types = TriggeredEntityTypes.None;
            if (AffectsHoldableActors) types |= TriggeredEntityTypes.HoldableActors;
            if (AffectsOtherActors) types |= TriggeredEntityTypes.NonHoldableActors;

            if (GravityType != GravityType.None && (AffectsHoldableActors || AffectsOtherActors))
            {
                Add(new GravityTriggerComponent(types)
                {
                    GravityType = GravityType,
                    MomentumMultiplier = MomentumMultiplier,
                });
            }
        }

        public override void Added(object scene)
        {
            base.Added(scene);

            UpdateFlags(true);

            if (_defaultToController && GravityControllerQueries.GetActive<SoundGravityController>(Scene) is
                    { } soundController)
            {
                if (GravityType == GravityType.Normal)
                    _normalSound = soundController.NormalSound;
                else if (GravityType == GravityType.Inverted)
                    _invertedSound = soundController.InvertedSound;
                else if (GravityType == GravityType.Toggle)
                    _toggleSound = soundController.ToggleSound;

                if (ExitGravityType == GravityType.Normal)
                    _exitSound = soundController.NormalSound;
                else if (ExitGravityType == GravityType.Inverted)
                    _exitSound = soundController.InvertedSound;
                else if (ExitGravityType == GravityType.Toggle)
                    _exitSound = soundController.ToggleSound;
            }
        }

        public override void Update()
        {
            UpdateFlags();

            base.Update();

            if (_audioMuffleSecondsRemaining > 0)
                _audioMuffleSecondsRemaining -= runtime.Engine.DeltaTime;
        }

        protected bool CheckEnableFlag() =>
            (string.IsNullOrWhiteSpace(EnableFlag) || ((OriginalLevelSceneRoot)Scene).Session.GetFlag(EnableFlag)) ^
            InvertEnableFlag;

        protected virtual bool CheckVisibleFlag() => false;

        protected void UpdateFlags(bool ignoreEvents = false)
        {
            // if we're not using flags at all, just bail, since apparently trigger triggers break it
            if (string.IsNullOrWhiteSpace(EnableFlag) && string.IsNullOrWhiteSpace(VisibleFlag))
                return;

            var coll = CheckEnableFlag();
            var vis = CheckVisibleFlag();

            if (ignoreEvents)
            {
                Collidable = coll;
                Visible = vis;
                return;
            }

            if (Collidable != coll)
            {
                Collidable = coll;
                EnabledChanged();
            }

            if (Visible != vis)
            {
                Visible = vis;
                VisibilityChanged();
            }
        }

        protected virtual void VisibilityChanged()
        {
        }

        protected virtual void EnabledChanged()
        {
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);
            HandleOnEnter(player);
        }

        protected virtual void HandleOnEnter(RoomPlayerSession player)
        {
            if (GravityType == GravityType.None || !AffectsPlayer || !ShouldAffectPlayer)
                return;

            if (runtime.PlayerComponent is { } playerComponent)
            {
                var previousGravity = playerComponent.CurrentGravity;
                playerComponent.SetGravity(GravityType, MomentumMultiplier);
                if (!string.IsNullOrWhiteSpace(sound) && playerComponent.CurrentGravity != previousGravity &&
                    _audioMuffleSecondsRemaining <= 0)
                {
                    ((Celeste.Core.Audio.ISourceAudioEnvironment)runtime.Engine).SourceAudio.Play(sound);
                    _audioMuffleSecondsRemaining = audio_muffle_seconds;
                }
            }
        }

        public override void OnStay(RoomPlayerSession player)
        {
            base.OnEnter(player);
            HandleOnStay(player);
        }

        protected virtual void HandleOnStay(RoomPlayerSession player)
        {
            base.OnStay(player);

            if (!AffectsPlayer || !ShouldAffectPlayer ||
                GravityType == GravityType.None ||
                GravityType == GravityType.Toggle ||
                GravityType == runtime.PlayerComponent?.CurrentGravity)
                return;

            runtime.PlayerComponent?.SetGravity(GravityType, MomentumMultiplier);
        }

        public override void OnLeave(RoomPlayerSession player)
        {
            base.OnLeave(player);
            HandleOnLeave(player);
        }

        protected virtual void HandleOnLeave(RoomPlayerSession player)
        {
            if (ExitGravityType == GravityType.None || !AffectsPlayer || !ShouldAffectPlayer)
                return;

            if (runtime.PlayerComponent is { } playerComponent)
            {
                var previousGravity = playerComponent.CurrentGravity;
                playerComponent.SetGravity(ExitGravityType, MomentumMultiplier);
                if (!string.IsNullOrWhiteSpace(_exitSound) && playerComponent.CurrentGravity != previousGravity &&
                    _audioMuffleSecondsRemaining <= 0)
                {
                    ((Celeste.Core.Audio.ISourceAudioEnvironment)runtime.Engine).SourceAudio.Play(_exitSound);
                    _audioMuffleSecondsRemaining = audio_muffle_seconds;
                }
            }
        }
    }
}

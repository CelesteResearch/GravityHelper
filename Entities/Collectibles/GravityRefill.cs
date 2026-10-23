// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Components.Timing;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Effects.Slashes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Rendering;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Components.Accessibility;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Metadata;
using Celeste.Mods.GravityHelper.Presentation;
using Celeste.Mods.GravityHelper.Presentation.Colors;
using Celeste.Mods.GravityHelper.Presentation.Shaders;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Physics.Collision.Shapes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Presentation.Effects.Mirrors;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using Celeste.Simulation.Presentation.ScreenEffects;

namespace Celeste.Mods.GravityHelper.Entities.Collectibles
{
    [RuntimeTypeAlias("Celeste.Mod.GravityHelper.Entities.GravityRefill")]
    [CustomEntity("GravityHelper/GravityRefill = CreateRefill",
        "GravityHelper/GravityRefillWall = CreateWall")]
    public partial class GravityRefill : SceneEntityState
    {
        private readonly GravityRuntimeContext runtime;

        private ISourceEngineContext Engine => runtime.Engine;

        // properties
        public bool OneUse { get; }
        public int Charges { get; }
        public int Dashes { get; }
        public bool RefillsDash { get; }
        public bool RefillsStamina { get; }
        public float RespawnTime { get; }

        // ReSharper disable NotAccessedField.Local
        private readonly VersionInfo _modVersion;
        private readonly VersionInfo _pluginVersion;
        // ReSharper restore NotAccessedField.Local

        // components
        private Sprite _sprite;
        private Sprite _arrows;
        private SpriteImage _outline;
        private WigglerComponent _wiggler;
        private BloomPointState _bloom;
        private VertexLightState _light;
        private SineWaveState _sine;

        private OriginalLevelSceneRoot _level;
        private float _respawnTimeRemaining;
        private float _arrowIntervalOffset;
        private readonly string _textureDirectory;
        private readonly bool _showRipple;

        private bool _emitNormal;
        private bool _isWall;
        private float _wallAlpha = 0.8f;
        private float _spriteAlpha = 1f;
        private WallRenderMode _wallRenderMode;

        private const float bloom_alpha = 0.8f;
        private float bloomAlpha => runtime.Settings.RefillBloom ? bloom_alpha : 0f;

        // if set to true, dashing over the top of a gravity refill
        // while Madeline already has a charge will not refund it
        private bool _legacyRefillBehavior;

        public GravityRefill(ISourceEngineContext engine, Vector2 position, bool twoDashes, bool oneUse)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
        {
            runtime = GravityRuntimeContext.For(engine);
            Charges = twoDashes ? 2 : 1;
            Dashes = twoDashes ? 2 : -1;
            OneUse = oneUse;
            RefillsDash = true;
            RefillsStamina = true;
            RespawnTime = 2.5f;
            _textureDirectory = "";
            _showRipple = true;

            init((int)(position.X + position.Y));
        }

        public static GravityRefill CreateRefill(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData entityData) =>
            new GravityRefill(level.EngineContext, entityData, offset, false);

        public static GravityRefill CreateWall(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData entityData) =>
            new GravityRefill(level.EngineContext, entityData, offset, true);

        public GravityRefill(ISourceEngineContext engine, EntityData data, Vector2 offset, bool wall)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            runtime = GravityRuntimeContext.For(engine);
            _modVersion = data.ModVersion();
            _pluginVersion = data.PluginVersion();

            _isWall = wall;

            Charges = data.Int("charges", 1);
            Dashes = data.Int("dashes", -1);
            OneUse = data.Bool("oneUse");
            RefillsDash = data.Bool("refillsDash", true);
            RefillsStamina = data.Bool("refillsStamina", true);
            RespawnTime = data.Float("respawnTime", 2.5f);
            _wallAlpha = data.Float("wallAlpha", 0.8f);
            _legacyRefillBehavior = data.Bool("legacyRefillBehavior", true);
            _textureDirectory = data.Attr("textureDirectory").Trim();
            _showRipple = data.Bool("showRipple", true);

            init(data.ID, data.Width, data.Height);
        }

        private void init(int randomSeed, int width = 0, int height = 0)
        {
            Collider = _isWall
                ? new HitboxColliderGeometry(width, height)
                : new HitboxColliderGeometry(16f, 16f, -8f, -8f);
            Depth = _isWall ? 100 : -100;

            var path = "objects/GravityHelper/gravityRefill/";
            var outlineName = Dashes == 2 ? "outline_two_dash" : "outline";
            var animationName = !RefillsDash ? "idle_no_dash" : Dashes == 2 ? "idle_two_dash" : "idle";

            Add(new PlayerColliderComponentState(OnPlayer));

            if (!string.IsNullOrWhiteSpace(_textureDirectory))
            {
                path = _textureDirectory;
                Add(_sprite = GravitySpriteTemplates.CreateWithPath(Engine, "gravityRefill", path));
                if (_showRipple)
                    Add(_arrows = GravitySpriteTemplates.CreateWithPath(Engine, "gravityRefillArrows", path));
            }
            else
            {
                Add(_sprite = runtime.GameSprites.Create("gravityRefill"));
                if (_showRipple) Add(_arrows = runtime.GameSprites.Create("gravityRefillArrows"));
            }

            Add(
                _wiggler = ((ISourceWigglerPoolScope)Engine).WigglerPool.Create(1f, 4f,
                    v => _sprite.Scale = Vector2.One * (float)(1.0 + v * 0.2)),
                new MirrorReflectionState());

            if (!_isWall)
            {
                Add(_bloom = new BloomPointState(bloom_alpha, 16f),
                    _light = new VertexLightState(Engine, Vector2.Zero, PackedColor.White, 1f, 16, 48),
                    _sine = new SineWaveState(Engine, 0.6f, 0.0f),
                    _outline = new SpriteImage(
                            ((ISourceGameGraphicsEnvironment)Engine).GameGraphics.GameAtlas[path + outlineName])
                        { Visible = false });

                _outline.CenterOrigin();
            }

            // this uses a random sample but so as to not break existing maps i'm leaving it above the PushRandomDisposable
            _sprite.Play(animationName, true, true);

            var random = ((ISourceRandomProcessEnvironment)Engine).RandomProcess;
            random.PushRandom(randomSeed);
            try
            {
                _sine?.Randomize();
                if (_arrows != null) _arrows.OnFinish = _ => _arrows.Visible = false;
                _arrowIntervalOffset = SourceRandomMath.NextFloat(((ISourceRandomEnvironment)Engine).Random, 2f);
            }
            finally
            {
                random.PopRandom();
            }

            Add(new AccessibilityListener(onAccessibilityChange));
            onAccessibilityChange();
        }

        private void onAccessibilityChange() => updateVisuals();

        public override void Added(object scene)
        {
            base.Added(scene);
            _level = (OriginalLevelSceneRoot)Scene;
        }

        public override void Update()
        {
            base.Update();

            if (_respawnTimeRemaining > 0.0)
            {
                _respawnTimeRemaining -= Engine.DeltaTime;
                if (_respawnTimeRemaining <= 0.0)
                    respawn();
            }
            else if (((ISourceScene)Scene).SourceScene.Clock.OnInterval(0.1f, Engine) && !_isWall)
            {
                var colorScheme = runtime.Settings.GetColorScheme();
                var offset = Vector2.UnitY * (_emitNormal ? 5f : -5f);
                var range = Vector2.One * 4f;
                var direction = Vector2Geometry.Angle(Vector2.UnitY) * (_emitNormal ? 1 : -1);
                var p_glow = _emitNormal
                    ? colorScheme.P_GravityRefill_Glow_Normal
                    : colorScheme.P_GravityRefill_Glow_Inverted;
                _level.ParticlesFG.Emit(p_glow, 1, Position + offset, range, direction);
                _emitNormal = !_emitNormal;
            }

            updateVisuals();
        }

        private void updateVisuals()
        {
            if (_isWall)
            {
                _sprite.Position = Collider.Center;
                if (_arrows != null) _arrows.Position = _sprite.Position;
            }
            else
            {
                _sprite.Position.Y = _bloom.Y = _sine.Value * 2f;
                if (_arrows != null) _arrows.Position.Y = _sprite.Position.Y;
            }

            if (_light != null && _bloom != null)
            {
                _light.Alpha = ScalarMath.Approach(_light.Alpha, _sprite.Visible ? 1f : 0.0f, 4f * Engine.DeltaTime,
                    ((ISourceExtremaEnvironment)Engine).ExtremaArithmetic);
                _bloom.Alpha = _light.Alpha * bloomAlpha;
            }

            if (_arrows != null && Scene != null &&
                ((ISourceScene)Scene).SourceScene.Clock.OnInterval(2f, _arrowIntervalOffset, Engine) &&
                _sprite.Visible &&
                (!_isWall || _wallRenderMode == WallRenderMode.Filled))
            {
                var arrowName = Dashes == 2 ? "arrows_two_dash" : "arrows";
                _arrows.Play(arrowName, true);
                _arrows.Visible = true;
            }
        }

        private void respawn()
        {
            if (Collidable) return;
            Collidable = true;

            _wallRenderMode = WallRenderMode.Filled;

            _sprite.Visible = true;
            _arrows?.Stop();

            if (_outline != null)
                _outline.Visible = false;

            Depth = _isWall ? 100 : -100;

            _wiggler?.Start();
            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play("event:/game/general/diamond_return", Center);

            var colorScheme = runtime.Settings.GetColorScheme();
            _level.ParticlesFG.Emit(colorScheme.P_GravityRefill_Regen, 16, Center, Vector2.One * 2f);
        }


        private void OnPlayer(RoomPlayerSession player)
        {
            if (player.Get<PlayerGravityComponent>() is not { } playerGravityComponent) return;

            var dashes = Dashes < 0 ? player.Motor.BeforeState.MaxDashes : Dashes;
            bool canUse = RefillsDash && player.Data.Dashes < dashes ||
                          RefillsStamina && player.Data.Stamina < 20 ||
                          playerGravityComponent.GravityCharges < Charges;

            if (!canUse) return;

            if (RefillsDash && Dashes < 0)
                player.RefillDash();
            else if (RefillsDash && player.Data.Dashes < Dashes)
                player.Data.Dashes = Dashes;

            if (RefillsStamina) player.RefillStamina();
            playerGravityComponent.RefillGravityCharges(Charges, !_legacyRefillBehavior);

            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play("event:/game/general/diamond_touch", Position);
            ((IOriginalGameInputProcessEnvironment)Engine).InputSource.Rumble(RumbleStrength.Medium,
                RumbleLength.Medium);
            Collidable = false;
            Add(new SimulationCoroutine(Engine, refillRoutine(player)));
            _respawnTimeRemaining = RespawnTime;
            _wallRenderMode = WallRenderMode.Flash;
        }

        private IEnumerator refillRoutine(RoomPlayerSession player)
        {
            GravityRefill refill = this;
            ((ISourceFreezeEnvironment)Engine).Freeze(0.05f);
            yield return null;

            _wallRenderMode = WallRenderMode.Returning;
            refill._level.Shake();
            if (refill._arrows != null) refill._arrows.Visible = false;
            refill._sprite.Visible = _isWall;
            if (!refill.OneUse && refill._outline != null)
                refill._outline.Visible = true;
            refill.Depth = 8999;
            yield return 0.05f;

            if (!_isWall)
            {
                float direction = Vector2Geometry.Angle(player.Speed);
                var colorScheme = runtime.Settings.GetColorScheme();
                refill._level.ParticlesFG.Emit(colorScheme.P_GravityRefill_Shatter, 5, refill.Position,
                    Vector2.One * 4f,
                    direction - (float)Math.PI / 2f);
                refill._level.ParticlesFG.Emit(colorScheme.P_GravityRefill_Shatter, 5, refill.Position,
                    Vector2.One * 4f,
                    direction + (float)Math.PI / 2f);
                OriginalSlashFxState.Burst(Engine, refill.Position, direction);
            }

            if (refill.OneUse)
                refill.RemoveSelf();
        }
    }
}

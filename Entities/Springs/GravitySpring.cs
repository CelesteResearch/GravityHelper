using System;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Interaction.Springs;
using Celeste.Graphics.Animation;
using Celeste.Mods.GravityHelper.Components.Accessibility;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Metadata;
using Celeste.Mods.GravityHelper.Presentation;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Components.Oscillation;
using Celeste.Simulation.Gameplay.Hazards.Puffer;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Gameplay.Interaction.Springs;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Physics.Collision;

namespace Celeste.Mods.GravityHelper.Entities.Springs
{
    [CustomEntity("GravityHelper/GravitySpring = LoadSpring", "GravityHelper/GravitySpringFloor = LoadFloor",
        "GravityHelper/GravitySpringCeiling = LoadCeiling", "GravityHelper/GravitySpringWallLeft = LoadWallLeft",
        "GravityHelper/GravitySpringWallRight = LoadWallRight")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.GravityHelper.Entities.GravitySpring")]
    public partial class GravitySpring : OriginalSpringState
    {
        internal readonly ISourceEngineContext Engine;
        internal readonly GravityRuntimeContext Runtime;
        private readonly Sprite sprite;
        private readonly WigglerComponent wiggler;
        private readonly SourceStaticMoverState staticMover;
        private Sprite _overlaySprite;
        private float _cooldownRemaining;
        private float _gravityCooldown;
        internal readonly bool _showIndicator;
        internal readonly bool _largeIndicator;
        internal readonly int _indicatorOffset;
        internal readonly string _indicatorTexture;
        internal Vector2 _indicatorShakeOffset;
        private readonly bool _defaultToController;
        private readonly bool _drawOutline;
        private readonly bool _showOverlay;
        private readonly string _refillSound;

        // ReSharper disable NotAccessedField.Local
        private readonly VersionInfo _modVersion;

        private readonly VersionInfo _pluginVersion;
        // ReSharper restore NotAccessedField.Local

        private GravitySpringIndicatorRenderer _indicatorRenderer;
        public bool PlayerCanUse { get; }
        public new GravitySpringOrientation Orientation { get; }
        public GravityType GravityType { get; }
        public int RefillDashCount { get; }
        public bool RefillStamina { get; }

        public static bool RequiresHooks(EntityData data)
            => EntityAttributeReader.Enum<GravityType>(data.Values, "gravityType") is GravityType.Inverted
                or GravityType.Toggle;

        public static GravitySpring LoadSpring(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
            => new(level.EngineContext, data, offset,
                EntityAttributeReader.Enum<GravitySpringOrientation>(data.Values, "orientation"));

        public static GravitySpring LoadFloor(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
            => new(level.EngineContext, data, offset, GravitySpringOrientation.Floor);

        public static GravitySpring LoadCeiling(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset,
            OriginalLevelEntityData data)
            => new(level.EngineContext, data, offset, GravitySpringOrientation.Ceiling);

        public static GravitySpring LoadWallLeft(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset,
            OriginalLevelEntityData data)
            => new(level.EngineContext, data, offset, GravitySpringOrientation.WallLeft);

        public static GravitySpring LoadWallRight(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset,
            OriginalLevelEntityData data)
            => new(level.EngineContext, data, offset, GravitySpringOrientation.WallRight);

        public GravitySpring(ISourceEngineContext engine, EntityData data, Vector2 offset,
            GravitySpringOrientation orientation)
            : base(data.Position + offset, (SpringOrientation)((int)orientation % 3), data.Bool("playerCanUse", true),
                (IOriginalSpringProcess)engine)
        {
            Engine = engine;
            Runtime = GravityRuntimeContext.For(engine);
            _modVersion = data.ModVersion();
            _pluginVersion = data.PluginVersion();
            PlayerCanUse = data.Bool("playerCanUse", true);
            GravityType = EntityAttributeReader.Enum<GravityType>(data.Values, "gravityType");
            RefillDashCount = data.Int("refillDashCount", -1);
            RefillStamina = data.Bool("refillStamina", true);
            _defaultToController = data.Bool("defaultToController");
            _gravityCooldown = data.Float("gravityCooldown", _pluginVersion.Major >= 2 ? 0f : .1f);
            if (data.Values != null && data.Values.ContainsKey("cooldown")) _gravityCooldown = data.Float("cooldown");
            _showIndicator = data.Bool("showIndicator");
            _largeIndicator = data.Bool("largeIndicator");
            _indicatorOffset = data.Int("indicatorOffset", 8);
            _indicatorTexture = data.Attr("indicatorTexture").Trim();
            _drawOutline = data.Bool("drawOutline", true);
            _showOverlay = data.Bool("showOverlay");
            _refillSound = data.Attr("refillSound");
            Orientation = orientation;
            sprite = OwnedComponents.Get<Sprite>();
            wiggler = OwnedComponents.Get<WigglerComponent>();
            staticMover = OwnedComponents.Get<SourceStaticMoverState>();
            var spriteName = data.Attr("spriteName").Trim();
            if (string.IsNullOrWhiteSpace(spriteName)) spriteName = "gravitySpring";
            var overlayName = data.Attr("overlaySpriteName").Trim();
            if (string.IsNullOrWhiteSpace(overlayName)) overlayName = "gravitySpringOverlay";
            var path = data.Attr("textureDirectory").Trim();
            if (Runtime.GameSprites.Has(spriteName))
            {
                if (string.IsNullOrWhiteSpace(path)) Runtime.GameSprites.CreateOn(sprite, spriteName);
                else GravitySpriteTemplates.CreateOnWithPath(engine, sprite, spriteName, path);
                sprite.Play(GetAnimId("idle"));
            }
            else LogInvalidSprite(spriteName, false);

            if (Runtime.GameSprites.Has(overlayName))
            {
                _overlaySprite = string.IsNullOrWhiteSpace(path)
                    ? Runtime.GameSprites.Create(overlayName)
                    : GravitySpriteTemplates.CreateWithPath(engine, overlayName, path);
                OwnedComponents.Add(_overlaySprite);
            }
            else LogInvalidSprite(overlayName, true);

            var overlayAnim = GetOverlayAnimId("idle");
            if (_overlaySprite != null)
            {
                _overlaySprite.Visible = _showOverlay && string.IsNullOrWhiteSpace(overlayAnim);
                if (_overlaySprite.Visible) PlayOverlay(overlayAnim);
            }

            staticMover.OnEnable = OnEnable;
            staticMover.OnDisable = OnDisable;
            staticMover.OnShake = amount => _indicatorShakeOffset += amount;
            OwnedComponents.Get<PlayerColliderComponentState>().OnCollide = OnCollide;
            OwnedComponents.Get<HoldableColliderState>().OnCollide = OnHoldable;
            var puffer = OwnedComponents.Get<PufferColliderState>();
            puffer.OnCollide = OnPuffer;
            switch (orientation)
            {
                case GravitySpringOrientation.Floor: sprite.Rotation = 0f; break;
                case GravitySpringOrientation.WallLeft: sprite.Rotation = (float)(Math.PI / 2f); break;
                case GravitySpringOrientation.WallRight: sprite.Rotation = (float)(-Math.PI / 2f); break;
                case GravitySpringOrientation.Ceiling:
                    sprite.Rotation = (float)Math.PI;
                    Collider.Top += 6f;
                    puffer.Collider.Top += 6f;
                    staticMover.SolidChecker =
                        solid => SourceEntityCollision.CheckAt(this, solid, Position - Vector2.UnitY);
                    staticMover.JumpThruChecker = platform =>
                        SourceEntityCollision.CheckAt(this, platform, Position - Vector2.UnitY);
                    break;
            }

            if (_overlaySprite != null) _overlaySprite.Rotation = sprite.Rotation;
            OwnedComponents.Add(new AccessibilityListener(OnAccessibilityChange));
        }
    }
}

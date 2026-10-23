using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Mods.GravityHelper.Metadata;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Riding;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Gameplay.Actors.Foundation;

namespace Celeste.Mods.GravityHelper.Entities.Platforms
{
    [Celeste.Everest.Modules.Entities.Metadata.CustomEntity("GravityHelper/UpsideDownJumpThru = Create")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.GravityHelper.Entities.UpsideDownJumpThru")]
    [SceneTracked]
    public class UpsideDownJumpThru : SourceJumpThruEntityState,
        Celeste.Physics.Collision.Vertical.IVerticalOneWayCollider
    {
        public static UpsideDownJumpThru Create(Celeste.Game.Execution.Levels.OriginalLevelSceneRoot level,
            Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData room, Vector2 offset,
            Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData data) =>
            new(level.EngineContext, data, offset, level.SolidWorldFactory);

        // ReSharper disable NotAccessedField.Local
        private readonly VersionInfo _modVersion;
        private readonly VersionInfo _pluginVersion;
        // ReSharper restore NotAccessedField.Local

        public bool AllowPositiveVerticalMovement => true;
        private System.IDisposable verticalRegistration;

        public override void Added(object scene)
        {
            base.Added(scene);
            verticalRegistration =
                Celeste.Physics.Collision.Vertical.VerticalCollisionRegistration.Register(Mover.World, this);
        }

        private bool CheckSolid(Vector2 position) => Celeste.Scene.Entities.SourceTrackedEntityCollision.CheckAtExcept(
            ((ISourceScene)Scene).SourceScene.Entities, "Celeste.Solid", "Celeste.SwapBlock", "Celeste.ExitBlock", this,
            position);

        private readonly int _columns;
        private readonly string _overrideTexture;
        private readonly int _overrideSoundIndex;
        private readonly bool _attached;
        private readonly bool _triggerStaticMovers;
        private readonly bool _invisible;
        private readonly string _textureDirectory;

        private Vector2 _shakeOffset;
        private SourcePlatformEntityState _attachedPlatform;
        private SourceStaticMoverState _staticMover;

        public UpsideDownJumpThru(ISourceSolidWorld world, Vector2 position, int width, string overrideTexture,
            int overrideSoundIndex = -1,
            bool safe = true, bool attached = false, bool triggerStaticMovers = true, bool invisible = false,
            string textureDirectory = null)
            : base(position, width, safe, world)
        {
            _modVersion = default;
            _pluginVersion = default;

            _columns = width / 8;
            _overrideTexture = overrideTexture;
            _overrideSoundIndex = overrideSoundIndex;
            _attached = attached;
            _triggerStaticMovers = triggerStaticMovers;
            _invisible = invisible;
            _textureDirectory = textureDirectory;

            init();
        }

        public UpsideDownJumpThru(ISourceEngineContext engine, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds)
            : base(data.Position + offset, data.Width, true, worlds.CreateJumpThru(data.Position + offset, true))
        {
            _modVersion = data.ModVersion();
            _pluginVersion = data.PluginVersion();

            _columns = data.Width / 8;
            _overrideTexture = data.Attr("texture", "default");
            _overrideSoundIndex = data.Int("surfaceIndex", -1);
            _attached = data.Bool("attached", false);
            _triggerStaticMovers = data.Bool("triggerStaticMovers", true);
            _invisible = data.Bool("invisible", false);
            _textureDirectory = data.Attr("textureDirectory").Trim();

            init();
        }

        private void init()
        {
            if (_attached)
            {
                List<SourceActorEntityState> sharedRiders = [];
                Add(_staticMover = new SourceStaticMoverState
                {
                    SolidChecker = solid =>
                        solid is not Celeste.Game.Gameplay.Platforms.FloatySpaceBlocks
                            .FloatySpaceBlockState && // moon blocks handle attached jumpthrus automatically
                        (SourceEntityCollision.CheckAt(this, solid, Position - Vector2.UnitX) ||
                         SourceEntityCollision.CheckAt(this, solid, Position + Vector2.UnitX)),
                    OnMove = amount =>
                    {
                        sharedRiders.Clear();
                        // get all the actors that are riding both the jumpthru and the attached solid, and make them ignore jumpthrus
                        if (_attachedPlatform is SourceSolidEntityState solid)
                        {
                            foreach (SourceActorEntityState actor in ((ISourceScene)Scene).SourceScene.Entities
                                     .GetTrackedEntities(typeof(SourceActorEntityState)))
                            {
                                if (actor.IsRiding(this) && actor.IsRiding(solid) && !actor.IgnoreJumpThrus &&
                                    !actor.TreatNaive)
                                {
                                    actor.IgnoreJumpThrus = true;
                                    actor.TreatNaive = true;
                                    sharedRiders.Add(actor);
                                }
                            }
                        }

                        // move the jumpthru and any riders that aren't also riding the attached solid
                        MoveH(amount.X);
                        MoveV(amount.Y);

                        // reset ignore jumpthrus if we must
                        foreach (var rider in sharedRiders)
                        {
                            rider.IgnoreJumpThrus = false;
                            rider.TreatNaive = false;
                        }

                        sharedRiders.Clear();
                    },
                    OnShake = amount =>
                    {
                        ShakeStaticMovers(amount);
                        _shakeOffset += amount;
                    },
                    OnAttach = p =>
                    {
                        _attachedPlatform = (SourcePlatformEntityState)p;
                        Depth = p.Depth + 1;
                    },
                    OnEnable = () =>
                    {
                        EnableStaticMovers();
                        Active = Visible = Collidable = true;
                    },
                    OnDisable = () =>
                    {
                        DisableStaticMovers();
                        Active = Visible = Collidable = false;
                    }
                });
            }

            Depth = -60;
            Collider.Top = 3;
        }

        public override void Render(Celeste.Core.Rendering.ISourceRenderContext context)
        {
            Position += _shakeOffset;
            base.Render(context);
            Position -= _shakeOffset;
        }

        public override void Update()
        {
            base.Update();

            if (_attachedPlatform != null && _attached && _triggerStaticMovers && HasPlayerRider())
                triggerPlatform();
        }

        public override void Removed(object scene)
        {
            verticalRegistration?.Dispose();
            verticalRegistration = null;
            base.Removed(scene);
            _attachedPlatform = null;
        }

        public override void Awake(object scene)
        {
            string str = ((OriginalLevelSceneRoot)scene).AreaData.Jumpthru;
            if (!string.IsNullOrEmpty(_overrideTexture) && !_overrideTexture.Equals("default"))
                str = _overrideTexture;

            SurfaceSoundIndex = _overrideSoundIndex > 0
                ? _overrideSoundIndex
                : str.ToLower() switch
                {
                    "dream" => 32,
                    "temple" => 8,
                    "templeb" => 8,
                    "core" => 3,
                    _ => 5,
                };

            var random = ((Celeste.Core.Randomness.ISourceRandomProcessEnvironment)SourceWorld.EngineContext)
                .RandomProcess;
            random.PushRandom(((OriginalLevelSceneRoot)scene).AreaData.ID);
            try
            {
                // "invisible" determines whether we add image components to the entity
                // this allows us to leave Visible = true so that subclasses can inherit the shake offset functionality
                if (!_invisible)
                {
                    var basePath = "objects/jumpthru/";
                    if (!string.IsNullOrWhiteSpace(_textureDirectory))
                        basePath = _textureDirectory.TrimEnd('/', ' ') + "/";

                    var mtexture =
                        ((Celeste.Graphics.Content.ISourceGameGraphicsEnvironment)SourceWorld.EngineContext)
                        .GameGraphics
                        .GameAtlas[basePath + str];
                    int textureWidthInTiles = mtexture.FrameWidth / 8;
                    for (int i = 0; i < _columns; ++i)
                    {
                        int xOffset;
                        int yOffset;
                        if (i == 0)
                        {
                            xOffset = 0;
                            yOffset = CheckSolid(Position + new Vector2(-1f, 0f)) ? 0 : 1;
                        }
                        else if (i == _columns - 1)
                        {
                            xOffset = textureWidthInTiles - 1;
                            yOffset = CheckSolid(Position + new Vector2(1f, 0f)) ? 0 : 1;
                        }
                        else
                        {
                            xOffset = 1 + random.Random.Next(textureWidthInTiles - 2);
                            yOffset = Celeste.Core.Randomness.RandomSelection.Choose(random.Random, 0, 1);
                        }

                        Add(new SpriteImage(mtexture.GetSubtexture(xOffset * 8, yOffset * 8, 8, 8))
                        {
                            Position = new Vector2(i * 8, 8),
                            Scale = { Y = -1 },
                        });
                    }
                }
            }
            finally
            {
                random.PopRandom();
            }

            foreach (SourceStaticMoverState mover in ((ISourceScene)scene).SourceScene.Components.GetTrackedComponents(
                         "Celeste.StaticMover"))
            {
                if (mover.IsRiding(this) && mover.Platform == null)
                {
                    staticMovers.Add(mover);
                    mover.Platform = this;
                    mover.OnAttach?.Invoke(this);
                }
            }
        }


        public override void OnStaticMoverTrigger(SourceStaticMoverState sm) => triggerPlatform();

        private void triggerPlatform() => _attachedPlatform?.OnStaticMoverTrigger(_staticMover);
    }
}

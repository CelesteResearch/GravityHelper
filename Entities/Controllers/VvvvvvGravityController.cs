// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Core.Scheduling;
using Celeste.Core.Audio;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Mods.GravityHelper.Metadata;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Inventory;
using Celeste.Scene.Tracking.Attributes;
using System.Numerics;


namespace Celeste.Mods.GravityHelper.Entities.Controllers
{
    [CustomEntity("GravityHelper/VvvvvvGravityController = Create")]
    [SceneTracked]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.GravityHelper.Entities.Controllers.VvvvvvGravityController")]
    public class VvvvvvGravityController : BaseGravityController<VvvvvvGravityController>
    {
        private readonly GravityRuntimeContext runtime;
        private readonly GravityExtendedJumps extendedJumps;

        public static VvvvvvGravityController Create(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        public VvvvvvMode Mode { get; set; } = VvvvvvMode.Off;
        public string FlipSound { get; } = default_flip_sound;
        public bool DisableGrab { get; } = true;
        public bool DisableDash { get; } = true;
        public bool DisableWallJump { get; } = true;
        public VvvvvvJumpBehavior SolidTilesBehavior { get; } = VvvvvvJumpBehavior.Flip;
        public VvvvvvJumpBehavior OtherPlatformBehavior { get; } = VvvvvvJumpBehavior.Flip;
        public VvvvvvJumpBehavior ExtraJumpsBehavior { get; } = VvvvvvJumpBehavior.Flip;

        public bool IsVvvvvv => runtime.Settings.VvvvvvMode == VvvvvvSetting.Default
            ? Mode == VvvvvvMode.TriggerBased && runtime.Session.VvvvvvTrigger || Mode == VvvvvvMode.On
            : runtime.Settings.VvvvvvMode == VvvvvvSetting.Enabled;

        public bool IsDisableDash => runtime.Settings.VvvvvvAllowDashing == VvvvvvSetting.Default
            ? DisableDash
            : runtime.Settings.VvvvvvAllowDashing != VvvvvvSetting.Enabled;

        private bool _dashDisabled;
        private float _bufferTimeRemaining;

        private const float flip_buffer_seconds = 0.1f;
        private const string default_flip_sound = "event:/gravityhelper/toggle";
        private VvvvvvJumpBehavior _lastJumpBehavior = VvvvvvJumpBehavior.Flip;

        // ReSharper disable once UnusedMember.Global
        public VvvvvvGravityController(ISourceEngineContext engine) : base(engine)
        {
            runtime = GravityRuntimeContext.For(engine);
            extendedJumps = new GravityExtendedJumps(engine);
            // ephemeral controller
        }

        // ReSharper disable once UnusedMember.Global
        public VvvvvvGravityController(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(engine, data, offset)
        {
            runtime = GravityRuntimeContext.For(engine);
            extendedJumps = new GravityExtendedJumps(engine);
            Mode = EntityAttributeReader.Enum(data.Values, "mode", Mode);
            FlipSound = data.Attr("flipSound", FlipSound);
            DisableGrab = data.Bool("disableGrab", DisableGrab);
            DisableDash = data.Bool("disableDash", DisableDash);
            DisableWallJump = data.Bool("disableWallJump", DisableWallJump);
            SolidTilesBehavior = EntityAttributeReader.Enum(data.Values, "solidTilesBehavior", SolidTilesBehavior);
            OtherPlatformBehavior =
                EntityAttributeReader.Enum(data.Values, "otherPlatformBehavior", OtherPlatformBehavior);
            ExtraJumpsBehavior = EntityAttributeReader.Enum(data.Values, "extraJumpsBehavior", ExtraJumpsBehavior);
        }

        public override void Transitioned()
        {
            if (!Persistent) return;

            var active = ActiveController;
            var session = runtime.Session;
            var settings = runtime.Settings;

            session.VvvvvvMode = settings.VvvvvvMode switch
            {
                VvvvvvSetting.Enabled => VvvvvvMode.On,
                VvvvvvSetting.Disabled => VvvvvvMode.Off,
                _ => active.Mode,
            };

            session.DisableGrab = settings.VvvvvvAllowGrabbing switch
            {
                VvvvvvSetting.Enabled => false,
                VvvvvvSetting.Disabled => active.IsVvvvvv,
                _ => active.DisableGrab,
            };
        }

        public void TryFlip(RoomPlayerSession player)
        {
            if (!Persistent) return;

            // bail if no active controller somehow
            if (ActiveController is not { } active || !active.IsVvvvvv) return;

            // set jump buffer, we'll check it afterwards
            if (player.Input.Jump.Pressed)
                _bufferTimeRemaining = flip_buffer_seconds;

            // see if we have any Extended Variant Jumps(TM)
            int extVarJumps = extendedJumps.GetBuffer();

            // on ground or within coyote frames
            var onGroundChecked = player.OnGround();
            var onGround = player.Data.OnGround || player.Data.Motion.JumpGraceTimer > 0;

            // find out what type of ground we're on
            if (onGroundChecked)
            {
                var checkPos = player.Position +
                               ((runtime.PlayerComponent?.ShouldInvert ?? false) ? -Vector2.UnitY : Vector2.UnitY);
                var onSolidTiles = GravityControllerPlayerFacts.CheckPlatform(player, checkPos, true);
                var onOtherPlatform = GravityControllerPlayerFacts.CheckPlatform(player, checkPos, false);
                var solidTilesBehavior = onSolidTiles ? active.SolidTilesBehavior : VvvvvvJumpBehavior.None;
                var otherPlatformBehavior = onOtherPlatform ? active.OtherPlatformBehavior : VvvvvvJumpBehavior.None;
                _lastJumpBehavior = (VvvvvvJumpBehavior)Math.Max((int)solidTilesBehavior, (int)otherPlatformBehavior);
            }
            else if (!onGround)
            {
                _lastJumpBehavior = extVarJumps > 0 ? active.ExtraJumpsBehavior : VvvvvvJumpBehavior.Jump;
            }

            // if we're flipping or if we're not on checked ground and we're disabling walljumps, consume the jumps
            var canUnDuck = player.World.CanUnDuck;
            var consumed = false;
            if (active.DisableWallJump && !onGroundChecked && !onGround && canUnDuck &&
                (player.World.WallJumpCheck(-1) || player.World.WallJumpCheck(1)))
            {
                player.Input.Jump.ConsumePress();
                consumed = true;
            }

            // bail if no jump has been buffered
            if (_bufferTimeRemaining <= 0) return;

            // jump should bail here so that Player.orig_Update can handle it
            if (_lastJumpBehavior == VvvvvvJumpBehavior.Jump) return;

            // we must consume jumps here if not already
            if (!consumed)
                player.Input.Jump.ConsumePress();

            // none should bail here
            if (_lastJumpBehavior == VvvvvvJumpBehavior.None) return;

            // for now we'll only allow normal state
            if (player.Machine.State != 0) return;

            // never allow from dream jumps
            if (player.Data.DreamJump) return;

            // consume an Extended Variant Jump(TM) if we need to
            if (!onGround && extVarJumps > 0)
                extendedJumps.SetBuffer(extVarJumps - 1);
            // otherwise bail if we're not on ground
            else if (!onGround)
                return;

            // reset autoflip
            _bufferTimeRemaining = -1f;

            // flip the player
            runtime.PlayerComponent?.SetGravity(GravityType.Toggle, instant: true);

            // play a sound if we should
            var flipSound = active.FlipSound;
            if (runtime.Settings.VvvvvvFlipSound == VvvvvvSetting.Enabled)
                flipSound = string.IsNullOrWhiteSpace(flipSound) ? default_flip_sound : flipSound;
            else if (runtime.Settings.VvvvvvFlipSound == VvvvvvSetting.Disabled)
                flipSound = string.Empty;
            if (!string.IsNullOrEmpty(flipSound))
                ((ISourceAudioEnvironment)runtime.Engine).SourceAudio.Play(flipSound);
        }

        public override void Update()
        {
            base.Update();
            if (!Persistent) return;

            var active = ActiveController;
            var isVvvvvv = active.IsVvvvvv;
            var disableDash = active.IsDisableDash;

            if (_dashDisabled != (isVvvvvv && disableDash))
            {
                _dashDisabled = isVvvvvv && disableDash;
                updateInventory();
            }

            if (_bufferTimeRemaining > 0)
                _bufferTimeRemaining -= runtime.Engine.DeltaTime;
        }

        private void updateInventory()
        {
            if (Scene is not OriginalLevelSceneRoot level ||
                level.Scheduler.Entities.GetTrackedEntity<RoomPlayerSession>() is not { } player)
                return;

            if (_dashDisabled)
            {
                level.Session.Inventory = new RoomPlayerInventory(0);
                player.Data.Dashes = 0;
            }
            else
            {
                var inv = RoomPlayerInventory.Default;
                var invName = GravityControllerPlayerFacts.InventoryName(runtime.Engine, level);
                if (!string.IsNullOrWhiteSpace(invName) &&
                    typeof(RoomPlayerInventory).GetField(invName, BindingFlags.Public | BindingFlags.Static) is
                        { } fieldInfo)
                    inv = (RoomPlayerInventory)fieldInfo.GetValue(null);

                level.Session.Inventory = inv;
                player.Data.Dashes = player.Motor.BeforeState.MaxDashes;
            }
        }
    }
}

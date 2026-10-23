// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Numerics;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Physics.Collision.Vertical;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Hazards.TriggerSpikes.Components;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.GravityHelper.Components
{
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.GravityHelper.Components.PlayerGravityComponent")]
    public class PlayerGravityComponent : GravityComponent
    {
        public const string PlayerFlag = "GravityHelper_PlayerInverted";
        private readonly GravityRuntimeContext runtime;
        private bool refilledThisFrame;
        public GravityType PreDreamBlockGravityType { get; set; }
        public int GravityCharges { get; set; }
        public new RoomPlayerSession Entity => base.Entity as RoomPlayerSession;

        public PlayerGravityComponent(GravityRuntimeContext runtime) : base(runtime)
        {
            this.runtime = runtime;
            CheckInvert = () => Entity.Machine.State switch
            {
                9 or 22 or 4 or 12 or 14 or 15 => false, // DreamDash, Attract, Boost, IntroWalk/Respawn/WakeUp.
                11 when !Entity.Dummy.Gravity => false,
                _ => true // IntroJump remains invertible for summit launch up transitions.
            };
            UpdateVisuals = UpdatePlayerVisuals;
            UpdatePosition = UpdatePlayerPosition;
            UpdateColliders = UpdatePlayerColliders;
            UpdateSpeed = UpdatePlayerSpeed;
            GetSpeed = () => Entity.Data.Motion.Normal.Speed;
            SetSpeed = value => Entity.Data.Motion.Normal.Speed = value;
            Flag = PlayerFlag;
        }

        private void UpdatePlayerVisuals(GravityChangeArgs args)
        {
            if (!args.Changed || Entity.Scene == null) return;
            Entity.NormalLightOffset = new Vector2(0, args.NewValue == GravityType.Normal ? -8 : 8);
            Entity.DuckingLightOffset = new Vector2(0, args.NewValue == GravityType.Normal ? -3 : 3);
            Entity.Light.Position = Entity.Data.Motion.Ducking ? Entity.DuckingLightOffset : Entity.NormalLightOffset;
            if (Entity.StarFlyBloom is { } bloom)
                bloom.Y = Math.Abs(bloom.Y) * (args.NewValue == GravityType.Inverted ? 1 : -1);
        }

        private void UpdatePlayerPosition(GravityChangeArgs args)
        {
            if (!args.Changed || Entity.Scene == null) return;
            var collider = Entity.Collider ?? Entity.World.Contacts.NormalHitbox;
            Entity.Position = new Vector2(Entity.Position.X, args.NewValue == GravityType.Inverted
                ? collider.AbsoluteTop
                : collider.AbsoluteBottom);
        }

        private void UpdatePlayerColliders(GravityChangeArgs args)
        {
            if (!args.Changed || Entity.Scene == null) return;
            var contacts = Entity.World.Contacts;
            InvertHitbox(contacts.NormalHitbox);
            InvertHitbox(contacts.NormalHurtbox);
            InvertHitbox(contacts.DuckHitbox);
            InvertHitbox(contacts.DuckHurtbox);
            InvertHitbox(contacts.FeatherHitbox);
            InvertHitbox(contacts.FeatherHurtbox);
        }

        private static void InvertHitbox(HitboxColliderGeometry hitbox)
            => hitbox.Position.Y = -hitbox.Position.Y - hitbox.Height;

        private void UpdatePlayerSpeed(GravityChangeArgs args)
        {
            if (!args.Changed || Entity.Scene == null) return;
            if (args.Instant)
            {
                if (Entity.Scene is OriginalLevelSceneRoot level)
                    Entity.Data.Motion.Normal.Speed.Y = Math.Max(Math.Abs(Entity.Data.Motion.Normal.Speed.Y),
                        160f * (level.InSpace ? .6f : 1f));
            }
            else Entity.Data.Motion.Normal.Speed.Y *= -args.MomentumMultiplier;

            Entity.Data.Direction.Y *= -1;
            Entity.Data.Motion.Normal.VariableJumpTimer = 0;
            CheckGround(args.NewValue, out bool onGround, out bool onSafeGround);
            bool oldOnGround = Entity.Data.OnGround;
            if (oldOnGround && !onGround) Entity.Data.Motion.JumpGraceTimer = 0;
            else if (!oldOnGround && onGround) Entity.StartJumpGraceTime();
            Entity.Data.OnGround = onGround;
            Entity.Data.OnSafeGround = onSafeGround;
        }

        private void CheckGround(GravityType type, out bool onGround, out bool onSafeGround)
        {
            Vector2 direction = type == GravityType.Inverted ? -Vector2.UnitY : Vector2.UnitY;
            onGround = onSafeGround = false;
            if (Entity.Machine.State != 9 && Entity.Data.Motion.Normal.Speed.Y >= 0)
            {
                Vector2 at = Entity.Position + direction;
                var solid = Entity.World.Platforms.FirstSolidAt(Entity.Body, at);
                if (solid != null)
                {
                    onGround = true;
                    onSafeGround = solid.Safe;
                }
                else
                {
                    var jumpThru = runtime.JumpThruContacts.First(Entity, at, type == GravityType.Inverted, true);
                    if (jumpThru != null)
                    {
                        onGround = true;
                        onSafeGround = jumpThru.Safe;
                    }
                }
            }

            if (Entity.Machine.State == 3) onSafeGround = true;
            if (onSafeGround)
                foreach (SafeGroundBlockerComponentState blocker in ((ISourceScene)Entity.Scene).SourceScene.Entities
                         .GetComponentsTrackIfNeeded<SafeGroundBlockerComponentState>())
                    if (blocker.Check(Entity))
                    {
                        onSafeGround = false;
                        break;
                    }
        }

        public override void Added(object entity)
        {
            base.Added(entity);
            runtime.PlayerComponent = this;
        }

        public override void Removed(object entity)
        {
            base.Removed(entity);
            if (runtime.PlayerComponent == this) runtime.PlayerComponent = null;
        }

        public override void EntityAdded(object scene)
        {
            base.EntityAdded(scene);
            runtime.PlayerComponent = this;
        }

        public override void EntityRemoved(object scene)
        {
            base.EntityRemoved(scene);
            if (runtime.PlayerComponent == this) runtime.PlayerComponent = null;
        }

        public override void Update()
        {
            refilledThisFrame = false;
            base.Update();
        }

        public void RefillGravityCharges(int charges = 1, bool retainChargeThisFrame = true)
        {
            GravityCharges = charges;
            refilledThisFrame |= retainChargeThisFrame;
        }

        public void ConsumeGravityCharge()
        {
            if (!refilledThisFrame) GravityCharges = Math.Max(GravityCharges - 1, 0);
        }
    }
}

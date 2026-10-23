using Celeste.Mods.GravityHelper.Runtime;
using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Game.Gameplay.Hazards.Puffer;
using Celeste.Game.Gameplay.Holdables.Gliders;
using Celeste.Game.Gameplay.Holdables.TheoCrystal;
using Celeste.Input.Gameplay.Feedback;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Hazards.Puffer;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Presentation.ScreenEffects;

namespace Celeste.Mods.GravityHelper.Entities.Springs
{
    public partial class GravitySpring
    {
        private bool ShouldInvertPlayer => Runtime.PlayerComponent?.ShouldInvert ?? false;

        private void OnCollide(RoomPlayerSession player)
        {
            if (player.Machine.State == 9 || !PlayerCanUse) return;
            var speed = player.Data.Motion.Normal.Speed;
            float realY = ShouldInvertPlayer ? -speed.Y : speed.Y;
            switch (Orientation)
            {
                case GravitySpringOrientation.Floor when realY < 0f:
                case GravitySpringOrientation.Ceiling when realY > 0f:
                case GravitySpringOrientation.WallLeft when speed.X > 240f:
                case GravitySpringOrientation.WallRight when speed.X < -240f:
                    return;
            }

            if (GravityType != GravityType.None && (_cooldownRemaining <= 0f || _gravityCooldown <= 0f))
            {
                Runtime.PlayerComponent?.SetGravity(GravityType);
                _cooldownRemaining = _gravityCooldown;
            }

            CustomBounceAnimate();
            float oldStamina = player.Data.Stamina;
            int oldDashes = player.Data.Dashes;
            switch (Orientation)
            {
                case GravitySpringOrientation.Floor:
                    if (ShouldInvertPlayer) InvertedSuperBounce(player, Top);
                    else player.SuperBounce(Top);
                    break;
                case GravitySpringOrientation.Ceiling:
                    if (!ShouldInvertPlayer) InvertedSuperBounce(player, Bottom);
                    else player.SuperBounce(Bottom);
                    break;
                case GravitySpringOrientation.WallLeft: player.SideBounce(1, Right, CenterY); break;
                case GravitySpringOrientation.WallRight: player.SideBounce(-1, Left, CenterY); break;
            }

            if (!RefillStamina) player.Data.Stamina = oldStamina;
            if (RefillDashCount >= 0)
            {
                player.Data.Dashes = Math.Max(oldDashes, RefillDashCount);
                if (!string.IsNullOrWhiteSpace(_refillSound) && RefillDashCount > player.Motor.BeforeState.MaxDashes &&
                    oldDashes < RefillDashCount)
                    ((Celeste.Core.Audio.ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(_refillSound,
                        Position);
            }
        }

        private void OnHoldable(HoldableState holdable)
        {
            var components = ((ISourceOwnedComponentList)holdable.Entity).OwnedComponents;
            var gravity = components.Get<GravityComponent>();
            var type = gravity?.CurrentGravity ?? GravityType.Normal;
            bool ceiling = type == GravityType.Normal && Orientation == GravitySpringOrientation.Ceiling ||
                           type == GravityType.Inverted && Orientation == GravitySpringOrientation.Floor;
            if (ceiling && !HoldableHitCeilingSpring(holdable) || !ceiling && !holdable.HitSpring(this)) return;
            CustomBounceAnimate();
            gravity?.SetGravity(GravityType);
        }

        private static bool HoldableHitCeilingSpring(HoldableState holdable)
        {
            if (holdable.IsHeld) return false;
            var speed = holdable.GetSpeed();
            if (speed.Y > 0f) return false;
            speed.X *= .5f;
            speed.Y = 160f;
            holdable.SpeedSetter?.Invoke(speed);
            switch (holdable.Entity)
            {
                case TheoCrystalState theo: GravitySpringPrivateAccess.SetTheoNoGravityTimer(theo, .15f); break;
                case OriginalGliderState glider:
                    GravitySpringPrivateAccess.SetGliderNoGravityTimer(glider, .15f);
                    GravitySpringPrivateAccess.GetGliderWiggler(glider).Start();
                    break;
            }

            return true;
        }

        private void OnPuffer(ISourcePufferEntity puffer)
        {
            if (Orientation == GravitySpringOrientation.Ceiling &&
                !PufferHitCeilingSpring((OriginalPufferState)puffer) ||
                Orientation != GravitySpringOrientation.Ceiling && !puffer.HitSpring(this)) return;
            CustomBounceAnimate();
        }

        private bool PufferHitCeilingSpring(OriginalPufferState puffer)
        {
            if (GravitySpringPrivateAccess.GetPufferHitSpeed(puffer).Y > 0f) return false;
            GravitySpringPrivateAccess.GotoPufferHitSpeed(puffer, 224f * Vector2.UnitY);
            puffer.MoveTowardsX(CenterX, 4f);
            GravitySpringPrivateAccess.GetPufferWiggler(puffer).Start();
            GravitySpringPrivateAccess.AlertPuffer(puffer, true, false);
            return true;
        }

        public static void InvertedSuperBounce(RoomPlayerSession player, float fromY)
        {
            var runtime = GravityRuntimeContext.For(player.EngineContext);
            bool inverted = runtime.PlayerComponent?.ShouldInvert ?? false;
            if (player.Machine.State == 4 && player.CurrentBooster != null)
            {
                player.CurrentBooster.PlayerReleased();
                player.CurrentBooster = null;
            }

            var collider = player.Collider;
            player.Collider = player.World.Contacts.NormalHitbox;
            player.MoveV(inverted ? player.Bottom - fromY : fromY - player.Top);
            if (!player.World.NoRefills) player.World.RefillDash();
            player.RefillStamina();
            player.Data.Motion.JumpGraceTimer = 0f;
            player.Data.Motion.Normal.VariableJumpTimer = 0f;
            player.Data.Motion.DashAttackTimer = 0f;
            player.Data.Motion.GliderBoostTimer = 0f;
            player.Data.Motion.WallSlideTimer = 1.2f;
            player.Data.Motion.WallBoostTimer = 0f;
            player.Data.Motion.Normal.VariableJumpSpeed = 0f;
            player.Data.Motion.Launched = false;
            player.Machine.State = 0;
            player.Data.Motion.AutoJump = false;
            player.Data.AutoJumpTimer = 0f;
            player.Data.Motion.Normal.Speed.X = 0f;
            player.Data.Motion.Normal.Speed.Y = 185f;
            (player.Scene as ISourceLevelShakeScene)?.DirectionalShake(inverted ? -Vector2.UnitY : Vector2.UnitY, .1f);
            ((ISourceRumbleEnvironment)player.EngineContext).RumbleSession.Rumble(1, 1);
            player.Sprite.Scale = new Vector2(.5f, 1.5f);
            player.Collider = collider;
        }
    }
}

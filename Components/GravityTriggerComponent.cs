using System.Collections.Generic;
using Celeste.Core.Components;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Physics.Collision;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Holdables;

namespace Celeste.Mods.GravityHelper.Components
{
    internal sealed class GravityTriggerComponent : SourceComponentState
    {
        private readonly List<GravityComponent> tracked = new();
        private readonly List<GravityComponent> remove = new();
        private readonly TriggeredEntityTypes types;
        public GravityType GravityType { get; set; }
        public float MomentumMultiplier { get; set; } = 1f;
        public GravityTriggerComponent(TriggeredEntityTypes types) : base(true, false) => this.types = types;

        public override void Removed(object entity)
        {
            base.Removed(entity);
            tracked.Clear();
        }

        public override void EntityRemoved(object scene)
        {
            base.EntityRemoved(scene);
            tracked.Clear();
        }

        public override void Update()
        {
            base.Update();
            var owner = (SceneEntityState)Entity;
            foreach (var component in tracked)
            {
                if (component.Entity == null) remove.Add(component);
                else if (!SourceEntityCollision.Check(owner, (SceneEntityState)component.Entity) ||
                         (((SceneEntityState)component.Entity).Get<HoldableState>()?.IsHeld ?? false))
                    remove.Add(component);
            }

            foreach (var component in remove) tracked.Remove(component);
            remove.Clear();
            if (!owner.Collidable) return;
            foreach (var raw in ((ISourceScene)Scene).SourceScene.Entities
                     .GetComponentsTrackIfNeeded<GravityComponent>())
            {
                var component = (GravityComponent)raw;
                if (component.Entity is not SceneEntityState entity ||
                    !SourceEntityCollision.Check(owner, entity)) continue;
                if (entity is not SourceActorEntityState && entity is not RoomPlayerSession) continue;
                if (!types.HasFlag(TriggeredEntityTypes.Player) && entity is RoomPlayerSession) continue;
                var holdable = entity.Get<HoldableState>();
                if (holdable != null && !types.HasFlag(TriggeredEntityTypes.HoldableActors)) continue;
                if (holdable?.IsHeld ?? false) continue;
                if (holdable == null && !types.HasFlag(TriggeredEntityTypes.NonHoldableActors)) continue;
                if (!tracked.Contains(component))
                {
                    tracked.Add(component);
                    component.SetGravity(GravityType, MomentumMultiplier);
                }

                if ((GravityType == GravityType.Normal || GravityType == GravityType.Inverted) &&
                    component.CurrentGravity != GravityType) component.SetGravity(GravityType, MomentumMultiplier);
            }
        }
    }
}

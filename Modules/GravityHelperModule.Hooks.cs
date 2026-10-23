// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using Celeste.Everest.Reflection.Runtime;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Collision;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Mods.GravityHelper.Entities.Controllers;
using Celeste.Mods.GravityHelper.Entities.Platforms;
using Celeste.Mods.GravityHelper.Entities.PlayerIndicators;
using Celeste.Mods.GravityHelper.Hooks;
using Celeste.Mods.GravityHelper.Hooks.ThirdParty;
using Celeste.Mods.GravityHelper.Hooks.Levels;
using Celeste.Mods.GravityHelper.Hooks.Platforms;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Game.Execution.Levels;
using MonoMod.Utils;

namespace Celeste.Mods.GravityHelper.Modules
{
    public sealed partial class GravityHelperModule
    {
        private Type maxUpsideDownType;

        private Type ResolveGhostType() => ((ISourceRuntimeTypeDiscoveryEnvironment)SourceEngine).RuntimeTypeDiscovery
            .ResolveRuntimeTypeAlias("Celeste.Mod.CelesteNet.Client.Entities.Ghost");

        private void InstallMapHooks()
        {
            maxUpsideDownType = ((ISourceRuntimeTypeDiscoveryEnvironment)SourceEngine).RuntimeTypeDiscovery
                .ResolveRuntimeTypeAlias("Celeste.Mod.MaxHelpingHand.Entities.UpsideDownJumpThru");
            var optional = new GravityOptionalEntityCallbacks(SourceEngine);
            var contacts = new GravityJumpThruContactQueries(maxUpsideDownType);
            runtime.JumpThruContacts = contacts;
            mapHooks.Add(new GravityCardinalBumperHooks(runtime));
            mapHooks.Add(new GravityActorHooks(SourceEngine, ShouldInvert, ShouldInvertChecked, runtime.Overrides));
            mapHooks.Add(new GravityAngryOshiroHooks(runtime));
            mapHooks.Add(new GravityBadelineBoostHooks(runtime));
            mapHooks.Add(new GravityChaserHooks(runtime));
            mapHooks.Add(new GravityBoosterHooks(runtime));
            mapHooks.Add(new GravityBounceBlockHooks(runtime));
            mapHooks.Add(new GravityBumperHooks(runtime));
            mapHooks.Add(GravityCassetteHooks.Install(SourceEngine));
            mapHooks.Add(new GravityCrushBlockHooks(runtime));
            mapHooks.Add(new GravityDashSwitchHooks(runtime));
            mapHooks.Add(new GravityFireBarrierHooks(runtime));
            mapHooks.Add(new GravityFloatySpaceBlockHooks(runtime));
            mapHooks.Add(new GravityFlyFeatherHooks(runtime));
            mapHooks.Add(new GravityFinalBossHooks(runtime));
            mapHooks.Add(new GravityGliderHooks(runtime));
            mapHooks.Add(new GravityHeartGemHooks(runtime));
            mapHooks.Add(new GravityHoldableHooks(runtime));
            mapHooks.Add(new GravityIceBlockHooks(runtime));
            mapHooks.Add(new GravityPlayerInputHooks(runtime,
                () => runtime.Settings.ControlScheme == ControlSchemeSetting.Absolute,
                () => runtime.Settings.FeatherControlScheme == ControlSchemeSetting.Absolute,
                ProcessVvvvvv, SuppressGrab));
            mapHooks.Add(GravityLevelLifecycleHooks.Install(SourceEngine));
            mapHooks.Add(new GravityLevelBoundsHooks(runtime));
            mapHooks.Add(new GravityLevelTransitionHooks(runtime));
            mapHooks.Add(new GravitySeekerHooks(runtime));
            mapHooks.Add(new GravitySnowballHooks(runtime));
            mapHooks.Add(new GravityMoveBlockHooks(runtime));
            mapHooks.Add(GravityPlatformHooks.Install(SourceEngine));
            mapHooks.Add(new GravityPlayerDeathHooks(runtime));
            mapHooks.Add(new GravityPlayerContactHooks(runtime, contacts));
            mapHooks.Add(new GravityPlayerPresentationHooks(runtime, ResolveGhostType()));
            mapHooks.Add(new GravityPlayerLifecycleHooks(runtime, player =>
                {
                    var trigger = GravityEntityQueries.FirstSpawn(player);
                    return trigger == null ? null : (trigger.GravityType, trigger.FireOnBubbleReturn);
                }, GravityEntityQueries.DashToToggle, GravityEntityQueries.IsGravityForced,
                optional.VvvvvvAtSpawn, AddIndicators, runtime.ChaserHistory.Clear));
            mapHooks.Add(new GravityPlayerNormalHooks(runtime, SuppressGrab));
            mapHooks.Add(new GravityPlayerCollisionHooks(runtime));
            mapHooks.Add(new GravityPlayerFrameHooks(runtime));
            mapHooks.Add(new GravityPlayerAnimationGeometryHooks(runtime));
            mapHooks.Add(new GravityPlayerFeatherHooks(runtime));
            mapHooks.Add(new GravityPlayerCarryHooks(runtime));
            mapHooks.Add(new GravityPlayerSpatialHooks(runtime));
            mapHooks.Add(new GravityPlayerMiscHooks(runtime));

            mapHooks.Add(new GravityPlayerClimbHooks(runtime, optional.HandleInversionBlocks));
            mapHooks.Add(new GravityPlayerSwimHooks(runtime));
            mapHooks.Add(new GravityPlayerDashHooks(runtime, UpsideDownJumpThrus));
            mapHooks.Add(new GravityPlayerJumpLaunchHooks(runtime, optional.HandleInversionBlocks));
            mapHooks.Add(new GravityPlayerDreamHooks(runtime));
            mapHooks.Add(new GravityPlayerTransitionHooks(runtime));
            mapHooks.Add(new GravityPlayerIntroHooks(runtime));
            mapHooks.Add(new GravitySpikesHooks(runtime));
            mapHooks.Add(new GravitySpringHooks(runtime));
            mapHooks.Add(new GravityStarJumpBlockHooks(runtime));
            mapHooks.Add(new GravityTheoCrystalHooks(runtime));
            mapHooks.Add(new GravityTriggerSpikesHooks(runtime));
        }

        private bool ShouldInvert(SceneEntityState entity) => entity is RoomPlayerSession
            ? runtime.PlayerComponent?.ShouldInvert ?? false
            : DynamicData.For(entity).Data.TryGetValue("GravityHelper_Inverted", out var value) && (bool)value;

        private bool ShouldInvertChecked(SceneEntityState entity) => entity is RoomPlayerSession
            ? runtime.PlayerComponent?.ShouldInvertChecked ?? false
            : entity.Get<GravityComponent>()?.ShouldInvertChecked ?? false;

        private void ProcessVvvvvv(RoomPlayerSession player)
        {
            if (player.Scene is OriginalLevelSceneRoot { InCutscene: false, Paused: false } level)
                GravityControllerQueries.GetPersistent<VvvvvvGravityController>(level)?.TryFlip(player);
        }

        private bool SuppressGrab()
        {
            var controller =
                GravityControllerQueries.GetActive<VvvvvvGravityController>(SourceEngine.Frame.Scenes.CurrentScene,
                    true);
            return controller is { IsVvvvvv: true, DisableGrab: true };
        }

        private void AddIndicators(RoomPlayerSession player)
        {
            var entities = ((OriginalLevelSceneRoot)player.Scene).Scheduler;
            entities.QueueSourceEntity(new GravityRefillIndicator(SourceEngine));
            entities.QueueSourceEntity(new GravityShieldIndicator(SourceEngine));
        }

        private IEnumerable<SourceJumpThruEntityState> UpsideDownJumpThrus(RoomPlayerSession player)
        {
            var tracker = ((ISourceScene)player.Scene).SourceScene.Entities;
            foreach (SourceJumpThruEntityState platform in tracker.GetEntitiesTrackIfNeeded<UpsideDownJumpThru>())
                yield return platform;
            if (maxUpsideDownType != null)
                foreach (SourceJumpThruEntityState platform in tracker.GetEntitiesTrackIfNeeded(maxUpsideDownType))
                    yield return platform;
        }
    }
}

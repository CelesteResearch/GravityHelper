// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Reflection;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Reflection.Runtime;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Responses;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks.ThirdParty
{
    public sealed class GravityMhhUpsideDownCornerHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly SourceEverestModulesProcess modules;
        private Hook unloadHook;
        private ILHook collisionHook;
        private MethodInfo collisionMethod;
        private bool disposed;

        public GravityMhhUpsideDownCornerHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            modules = ((ISourceEverestModulesProcessEnvironment)runtime.Engine).ModulesProcess;
            try
            {
                unloadHook = new Hook(
                    typeof(SourceEverestModulesProcess).GetMethod(nameof(SourceEverestModulesProcess.UnloadAssembly)),
                    (Action<Action<SourceEverestModulesProcess, EverestModuleMetadata, Assembly>,
                        SourceEverestModulesProcess, EverestModuleMetadata, Assembly>)UnloadAssembly);
                modules.Events.OnRegisterModule += Registered;
                Refresh();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void Registered(SourceEverestModule module) => Refresh();

        private void Refresh()
        {
            if (disposed) return;
            bool supported = !runtime.ModSupportBlacklist.Contains("MaxHelpingHand") &&
                             modules.Modules.Exists(static module => module.Metadata?.Name == "MaxHelpingHand");
            var ceiling = supported
                ? ((ISourceRuntimeTypeDiscoveryEnvironment)runtime.Engine).RuntimeTypeDiscovery
                .ResolveRuntimeTypeAlias("Celeste.Mod.MaxHelpingHand.Entities.UpsideDownJumpThru")
                : null;
            var type = ceiling?.Assembly.GetType("Celeste.Mods.MaxHelpingHand.Collision.UpsideDownPlayerHooks");
            var method = type?.GetMethod("Vertical", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == collisionMethod) return;
            collisionHook?.Dispose();
            collisionHook = null;
            collisionMethod = null;
            if (method == null) return;
            collisionHook = new ILHook(method, SkipCeilingStop);
            collisionMethod = method;
        }

        private void SkipCeilingStop(ILContext il)
        {
            var original = new ILCursor(il);
            if (!original.TryGotoNext(MoveType.Before,
                    instruction => instruction.MatchLdarg(1),
                    instruction => instruction.MatchLdarg(2),
                    instruction => instruction.MatchLdarg(3),
                    instruction => instruction.MatchLdarg(4),
                    instruction => instruction.MatchCallvirt(
                        "System.Action`3<Celeste.Simulation.Gameplay.Player.Collision.Responses.PlayerCollisionResponse,Celeste.Physics.Collision.PixelCollision,Celeste.Simulation.Gameplay.Platforms.Foundation.SourcePlatformEntityState>",
                        "Invoke")))
                throw new InvalidOperationException("The MHH vertical collision original call was not found.");
            var entry = new ILCursor(il);
            entry.Emit(OpCodes.Ldarg_2);
            entry.EmitDelegate<Func<PlayerCollisionResponse, bool>>(OwnsPlayer);
            entry.Emit(OpCodes.Brtrue, original.Next);
        }

        private bool OwnsPlayer(PlayerCollisionResponse response)
            => ReferenceEquals(response.Player.EngineContext, runtime.Engine);

        private void UnloadAssembly(Action<SourceEverestModulesProcess, EverestModuleMetadata, Assembly> original,
            SourceEverestModulesProcess process, EverestModuleMetadata metadata, Assembly assembly)
        {
            if (!ReferenceEquals(process, modules))
            {
                original(process, metadata, assembly);
                return;
            }

            try
            {
                original(process, metadata, assembly);
            }
            finally
            {
                Refresh();
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            modules.Events.OnRegisterModule -= Registered;
            collisionHook?.Dispose();
            collisionHook = null;
            collisionMethod = null;
            unloadHook?.Dispose();
            unloadHook = null;
        }
    }
}

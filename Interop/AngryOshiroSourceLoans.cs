using System;
using System.Linq.Expressions;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Enemies.AngryOshiro;
using Celeste.Simulation;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Player.Collision.Components;

namespace Celeste.Mods.GravityHelper.Interop
{
    internal static class AngryOshiroSourceLoans
    {
        internal static readonly Func<OriginalAngryOshiroState, ISourceEngineContext> Engine =
            Getter<ISourceEngineContext>("engine");

        internal static readonly Func<OriginalAngryOshiroState, PlayerColliderComponentState> BounceCollider =
            Getter<PlayerColliderComponentState>("bounceCollider");

        internal static readonly Func<OriginalAngryOshiroState, SimulationStateMachine> State =
            Getter<SimulationStateMachine>("state");

        internal static readonly Func<OriginalAngryOshiroState, SoundSourceComponentState> Precharge =
            Getter<SoundSourceComponentState>("prechargeSfx");

        internal static readonly Func<OriginalAngryOshiroState, SoundSourceComponentState> Charge =
            Getter<SoundSourceComponentState>("chargeSfx");

        internal static readonly Func<OriginalAngryOshiroState, bool> Leaving = Getter<bool>("leaving");

        internal static readonly Action<OriginalAngryOshiroState, float> SetCameraOffset =
            Setter<float>("cameraXOffset");

        internal static readonly Action<OriginalAngryOshiroState, bool> SetRespawnAnimation =
            Setter<bool>("doRespawnAnim");

        private static Func<OriginalAngryOshiroState, T> Getter<T>(string name)
        {
            var self = Expression.Parameter(typeof(OriginalAngryOshiroState));
            return Expression.Lambda<Func<OriginalAngryOshiroState, T>>(
                Expression.Field(self,
                    typeof(OriginalAngryOshiroState).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)),
                self).Compile();
        }

        private static Action<OriginalAngryOshiroState, T> Setter<T>(string name)
        {
            var self = Expression.Parameter(typeof(OriginalAngryOshiroState));
            var value = Expression.Parameter(typeof(T));
            return Expression.Lambda<Action<OriginalAngryOshiroState, T>>(
                Expression.Assign(
                    Expression.Field(self,
                        typeof(OriginalAngryOshiroState).GetField(name,
                            BindingFlags.Instance | BindingFlags.NonPublic)), value), self, value).Compile();
        }
    }
}

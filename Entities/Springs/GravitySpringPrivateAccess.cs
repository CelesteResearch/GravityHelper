using System;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Game.Gameplay.Hazards.Puffer;
using Celeste.Game.Gameplay.Holdables.Gliders;
using Celeste.Game.Gameplay.Holdables.TheoCrystal;
using Celeste.Scene.Components.Oscillation;

namespace Celeste.Mods.GravityHelper.Entities.Springs
{
    internal static class GravitySpringPrivateAccess
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        internal static readonly Action<TheoCrystalState, float> SetTheoNoGravityTimer =
            Setter<TheoCrystalState, float>("noGravityTimer");

        internal static readonly Action<OriginalGliderState, float> SetGliderNoGravityTimer =
            Setter<OriginalGliderState, float>("noGravityTimer");

        internal static readonly Func<OriginalGliderState, WigglerComponent> GetGliderWiggler =
            Getter<OriginalGliderState, WigglerComponent>("wiggler");

        internal static readonly Func<OriginalPufferState, Vector2> GetPufferHitSpeed =
            Getter<OriginalPufferState, Vector2>("hitSpeed");

        internal static readonly Func<OriginalPufferState, WigglerComponent> GetPufferWiggler =
            Getter<OriginalPufferState, WigglerComponent>("bounceWiggler");

        internal static readonly Action<OriginalPufferState, Vector2> GotoPufferHitSpeed =
            (Action<OriginalPufferState, Vector2>)typeof(OriginalPufferState).GetMethod("GotoHitSpeed", PrivateInstance)
                .CreateDelegate(typeof(Action<OriginalPufferState, Vector2>));

        internal static readonly Action<OriginalPufferState, bool, bool> AlertPuffer =
            (Action<OriginalPufferState, bool, bool>)typeof(OriginalPufferState).GetMethod("Alert", PrivateInstance)
                .CreateDelegate(typeof(Action<OriginalPufferState, bool, bool>));

        private static Func<T, TValue> Getter<T, TValue>(string name)
        {
            var method = new DynamicMethod(name, typeof(TValue), new[] { typeof(T) }, typeof(T), true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, typeof(T).GetField(name, PrivateInstance));
            il.Emit(OpCodes.Ret);
            return (Func<T, TValue>)method.CreateDelegate(typeof(Func<T, TValue>));
        }

        private static Action<T, TValue> Setter<T, TValue>(string name)
        {
            var method = new DynamicMethod(name, typeof(void), new[] { typeof(T), typeof(TValue) }, typeof(T), true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, typeof(T).GetField(name, PrivateInstance));
            il.Emit(OpCodes.Ret);
            return (Action<T, TValue>)method.CreateDelegate(typeof(Action<T, TValue>));
        }
    }
}

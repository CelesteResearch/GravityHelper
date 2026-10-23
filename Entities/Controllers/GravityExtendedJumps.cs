using System;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Core.Scheduling;

namespace Celeste.Mods.GravityHelper.Entities.Controllers
{
    internal sealed class GravityExtendedJumps
    {
        private readonly object process;
        private readonly Func<object, int> get;
        private readonly Action<object, int, bool> set;

        public GravityExtendedJumps(ISourceEngineContext engine)
        {
            Type type = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType("Celeste.Mods.ExtendedVariants.Runtime.JumpVariants.JumpVariantProcess", false);
                if (type != null) break;
            }

            if (type == null) return;
            process = type.GetMethod("For", BindingFlags.Public | BindingFlags.Static)!.Invoke(null,
                new object[] { engine });
            var getter = new DynamicMethod("GetExtendedJumpBuffer", typeof(int), new[] { typeof(object) },
                typeof(GravityExtendedJumps), true);
            var il = getter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, type);
            il.Emit(OpCodes.Callvirt, type.GetProperty("Buffer")!.GetMethod!);
            il.Emit(OpCodes.Ret);
            get = (Func<object, int>)getter.CreateDelegate(typeof(Func<object, int>));
            var setter = new DynamicMethod("SetExtendedJumpCount", typeof(void),
                new[] { typeof(object), typeof(int), typeof(bool) }, typeof(GravityExtendedJumps), true);
            il = setter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, type);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Callvirt, type.GetMethod("Set")!);
            il.Emit(OpCodes.Ret);
            set = (Action<object, int, bool>)setter.CreateDelegate(typeof(Action<object, int, bool>));
        }

        internal int GetBuffer() => get?.Invoke(process) ?? 0;
        internal void SetBuffer(int count) => set?.Invoke(process, count, false);
    }
}

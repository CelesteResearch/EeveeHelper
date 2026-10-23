using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Celeste.Mods.EeveeHelper.Handlers.Impl
{
    internal sealed class HandlerField<TTarget, TValue>
    {
        internal Func<TTarget, TValue> Read { get; }
        internal Action<TTarget, TValue> Write { get; }

        internal HandlerField(string name)
        {
            var field = typeof(TTarget).GetField(name,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly)
                        ?? throw new MissingFieldException(typeof(TTarget).FullName, name);
            if (field.FieldType != typeof(TValue))
                throw new InvalidOperationException("Handler field type differs: " + field);
            var getter = new DynamicMethod("Read" + name, typeof(TValue), new[] { typeof(TTarget) },
                typeof(HandlerField<TTarget, TValue>), true);
            var il = getter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            Read = (Func<TTarget, TValue>)getter.CreateDelegate(typeof(Func<TTarget, TValue>));
            var setter = new DynamicMethod("Write" + name, typeof(void), new[] { typeof(TTarget), typeof(TValue) },
                typeof(HandlerField<TTarget, TValue>), true);
            il = setter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
            Write = (Action<TTarget, TValue>)setter.CreateDelegate(typeof(Action<TTarget, TValue>));
        }
    }
}

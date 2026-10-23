using System;
using System.Runtime.CompilerServices;
using Celeste.Scene.Entities;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.EeveeHelper.Runtime
{
    internal static class EeveeEntityDataLookup
    {
        private static readonly MethodDetourInfo getValueInfo;
        private static readonly MethodDetourInfo tryGetValueInfo;
        private static readonly bool supported;

        static EeveeEntityDataLookup()
        {
            try
            {
                var table = typeof(ConditionalWeakTable<SceneEntityState, EeveeEntityData>);
                getValueInfo = DetourManager.GetDetourInfo(table.GetMethod("GetValue"));
                tryGetValueInfo = DetourManager.GetDetourInfo(table.GetMethod("TryGetValue"));
                supported = true;
            }
            catch (Exception)
            {
                // Unknown lookup metadata retains the original factory path.
            }
        }

        internal static bool CanUseFastLookup
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get => supported && !getValueInfo.IsDetoured && !tryGetValueInfo.IsDetoured;
        }
    }
}

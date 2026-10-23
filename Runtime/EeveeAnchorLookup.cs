using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Celeste.Mods.EeveeHelper.Components;
using Celeste.Mods.EeveeHelper.Handlers;

namespace Celeste.Mods.EeveeHelper.Runtime
{
    internal static class EeveeAnchorLookup
    {
        private static readonly MonoMod.RuntimeDetour.MethodDetourInfo originalInfo;
        private static readonly MonoMod.RuntimeDetour.MethodDetourInfo getAsInfo;
        private static readonly bool supported;

        static EeveeAnchorLookup()
        {
            try
            {
                originalInfo = MonoMod.RuntimeDetour.DetourManager.GetDetourInfo(
                    typeof(EntityContainerMover).GetMethod(nameof(EntityContainerMover.GetAnchors)));
                getAsInfo = MonoMod.RuntimeDetour.DetourManager.GetDetourInfo(
                    typeof(HandlerUtils).GetMethod(nameof(HandlerUtils.GetAs))
                        .MakeGenericMethod(typeof(IAnchorProvider), typeof(List<string>)));
                supported = true;
            }
            catch (Exception)
            {
                // Unknown metadata retains the original public entry point.
            }
        }

        internal static bool CanUseInlineLookup
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get => supported && !originalInfo.IsDetoured && !getAsInfo.IsDetoured;
        }

        internal static List<string> GetAnchors(EntityContainerMover self, IEntityHandler handler)
        {
            if (!CanUseInlineLookup) return self.GetAnchors(handler);
            List<string> result = null;
            if (handler is IAnchorProvider provider) result = provider.GetAnchors();
            else if (handler.Entity != null && handler.Container != null &&
                     handler.Container.IsFirstHandler(handler) &&
                     !handler.Container.HasHandlerFor<IAnchorProvider>(handler.Entity))
            {
                // GetAs evaluates this argument even though the original fallback ignores it.
                _ = handler.Entity;
                var data = EeveeEntityData.For(self.Engine, handler.Entity);
                result = data.Get<List<string>>("entityContainerAnchors");
            }

            return result ?? new List<string>();
        }
    }
}

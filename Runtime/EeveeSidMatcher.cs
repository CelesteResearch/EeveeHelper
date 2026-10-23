using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.EeveeHelper.Runtime
{
    internal static class EeveeSidMatcher
    {
        private static readonly MethodDetourInfo[] guarded = CreateGuards();

        private static MethodDetourInfo[] CreateGuards()
        {
            try
            {
                var any = typeof(Enumerable).GetMethods().Single(method =>
                    method.Name == nameof(Enumerable.Any) && method.GetParameters().Length == 2);
                return new[] { any.MakeGenericMethod(typeof(string)) }
                    .Concat(typeof(HashSet<string>).GetMethods(BindingFlags.Instance | BindingFlags.Public |
                                                               BindingFlags.NonPublic)
                        .Where(method => method.Name.EndsWith("GetEnumerator", StringComparison.Ordinal)))
                    .Select(DetourManager.GetDetourInfo).ToArray();
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException
                                              or NotSupportedException)
            {
                return Array.Empty<MethodDetourInfo>();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CanUse()
        {
            if (guarded.Length == 0) return false;
            foreach (var method in guarded)
                if (method.IsDetoured)
                    return false;
            return true;
        }

        internal static bool ContainsAny(HashSet<string> sids, Dictionary<string, HashSet<int>> typeList)
        {
            // Keep the original interface-enumerator and LINQ path for subclasses and hooks.
            if (sids == null || typeList == null || sids.GetType() != typeof(HashSet<string>) || !CanUse())
                return sids.Any(typeList.ContainsKey);

            foreach (string sid in sids)
                if (typeList.ContainsKey(sid))
                    return true;
            return false;
        }
    }
}

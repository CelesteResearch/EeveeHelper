using System;

namespace Celeste.Mods.EeveeHelper.Runtime
{
    internal sealed class EeveeMemberAccessor
    {
        public Func<object> Read;
        public Action<object> Write;
    }
}

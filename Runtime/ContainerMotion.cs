using System;
using System.Numerics;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.EeveeHelper.Runtime
{
    public static class ContainerMotion
    {
        public static Vector2 GetPosition(SceneEntityState entity) => entity is SourcePlatformEntityState platform
            ? platform.ExactPosition
            : entity.Position;

        public static Tuple<string, bool> ParseFlagAttr(string flag) => flag.StartsWith("!")
            ? Tuple.Create(flag.Substring(1), true)
            : Tuple.Create(flag, false);
    }
}

using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Scene.Entities;

namespace Celeste.Mods.EeveeHelper.Entities.Modifiers
{
    [CustomEntity(ID)]
    [RuntimeTypeAlias("Celeste.Mod.EeveeHelper.Entities.Modifiers.GlobalModifier")]
    public sealed class GlobalModifier : SceneEntityState
    {
        public const string ID = "EeveeHelper/GlobalModifier";

        public GlobalModifier(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
        }
    }
}

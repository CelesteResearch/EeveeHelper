using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Scene.Entities;
using Celeste.Mods.EeveeHelper.Entities.Modifiers;

namespace Celeste.Mods.EeveeHelper.Runtime.Modifiers
{
    public sealed class CollidableModifierRuntime
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, CollidableModifierRuntime> Owners = new();
        private readonly EeveeContainerRuntime owner;
        public ISourceEngineContext Engine { get; }

        private CollidableModifierRuntime(ISourceEngineContext engine)
        {
            Engine = engine;
            owner = EeveeContainerRuntime.For(engine);
        }

        public static CollidableModifierRuntime For(ISourceEngineContext engine) =>
            Owners.GetValue(engine, owner => new CollidableModifierRuntime(owner));

        public EeveeEntityData Data(SceneEntityState entity) =>
            EeveeEntityData.For(Engine, entity);

        public bool TryGetSolidifier(SceneEntityState entity, out CollidableModifier.Solidifier solidifier)
        {
            solidifier = null;
            return owner.TryGetExistingData(entity, out var existing) &&
                   existing.TryGet("solidModifierSolidifier", out solidifier);
        }
    }
}

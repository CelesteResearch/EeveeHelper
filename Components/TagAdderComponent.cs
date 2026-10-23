using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Scene.Entities;

namespace Celeste.Mods.EeveeHelper.Components
{
    [RuntimeTypeAlias("Celeste.Mod.EeveeHelper.Components.TagAdderComponent")]
    public sealed class TagAdderComponent : SourceComponentState
    {
        public int Tags;
        public TagAdderComponent(int tags) : base(true, true) => Tags = tags;

        public override void Added(object entity)
        {
            base.Added(entity);
            ((SceneEntityState)entity).Tags |= Tags;
        }

        public override void Update()
        {
            base.Update();
            ((SceneEntityState)Entity).Tags |= Tags;
        }
    }
}

using System.Numerics;
using Celeste.Simulation.Presentation.Decals.Entities;
using Celeste.Mods.EeveeHelper.Components;
using Celeste.Scene.Entities;

namespace Celeste.Mods.EeveeHelper.Handlers.Impl
{
    internal class DecalHandler(SceneEntityState entity) : EntityHandler(entity), IMoveable
    {
        public override bool IsInside(EntityContainer container) =>
            container.CheckDecal(Entity as SourceDecalEntityState);

        public bool Move(Vector2 move, Vector2? liftSpeed)
        {
            Entity.Position += move;
            return true;
        }

        public void PreMove()
        {
        }
    }
}

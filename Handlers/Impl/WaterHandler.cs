using System.Numerics;
using Celeste.Game.Gameplay.Environment.Water;
using Celeste.Mods.EeveeHelper.Components;
using Celeste.Scene.Entities;

namespace Celeste.Mods.EeveeHelper.Handlers.Impl
{
    internal class WaterHandler : EntityHandler, IMoveable
    {
        private readonly OriginalWaterState water;
        public WaterHandler(SceneEntityState entity) : base(entity) => water = entity as OriginalWaterState;

        public bool Move(Vector2 move, Vector2? liftSpeed)
        {
            if (!(Container as EntityContainerMover).IgnoreAnchors)
                foreach (var surface in water.Surfaces)
                    surface.Position += move;
            return false;
        }

        public void PreMove()
        {
        }
    }
}

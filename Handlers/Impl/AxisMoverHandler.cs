using System;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Mods.EeveeHelper.Runtime;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.EeveeHelper.Handlers.Impl
{
    internal class AxisMoverHandler : EntityHandler, IMoveable
    {
        private readonly EeveeEntityData entityData;
        private readonly Tuple<string, bool>[] axesHandler;

        public AxisMoverHandler(ISourceEngineContext engine, SceneEntityState entity,
            params Tuple<string, bool>[] singleAxisHandler) : base(entity)
        {
            entityData = EeveeEntityData.For(engine, entity);
            axesHandler = singleAxisHandler;
        }

        public bool Move(Vector2 move, Vector2? liftSpeed)
        {
            if (Entity is SourcePlatformEntityState platform)
            {
                platform.MoveH(move.X);
                platform.MoveV(move.Y);
            }
            else Entity.Position += move;

            foreach (var pair in axesHandler)
                entityData.Set(pair.Item1, entityData.Get<float>(pair.Item1) + (pair.Item2 ? move.Y : move.X));
            return true;
        }

        public void PreMove()
        {
        }
    }
}

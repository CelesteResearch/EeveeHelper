using Celeste.Mods.EeveeHelper.Runtime;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.EeveeHelper.Entities.Modifiers
{
    public partial class CollidableModifier
    {
        [Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
            "Celeste.Mod.EeveeHelper.Entities.Modifiers.CollidableModifier+Solidifier")]
        public sealed class Solidifier : SourceSolidEntityState
        {
            public SceneEntityState Entity;
            public ISourceEngineContext EngineContext => SourceWorld.EngineContext;

            public Solidifier(SceneEntityState entity, ISourceSolidWorldFactory factory)
                : this(entity, ContainerMotion.GetPosition(entity), factory)
            {
            }

            private Solidifier(SceneEntityState entity, Vector2 position, ISourceSolidWorldFactory factory)
                : base(position, 1, 1, false, factory.Create(position, false))
            {
                Collider = entity.Collider.Clone();
                Depth = entity.Depth + 1;
                Entity = entity;
            }

            public override void Update()
            {
                base.Update();
                if (Entity == null || Entity.Scene == null)
                {
                    RemoveSelf();
                    return;
                }

                if (Collider.Size != Entity.Collider.Size) Collider = Entity.Collider.Clone();
                Depth = Entity.Depth + 1;
                if (ExactPosition != ContainerMotion.GetPosition(Entity)) MoveTo(ContainerMotion.GetPosition(Entity));
            }

            public override void MoveHExact(int move)
            {
                bool collidable = Entity.Collidable;
                Entity.Collidable = false;
                bool allowPushing = true;
                var actor = Entity as SourceActorEntityState;
                if (actor != null)
                {
                    allowPushing = actor.AllowPushing;
                    actor.AllowPushing = false;
                }

                base.MoveHExact(move);
                Entity.Collidable = collidable;
                if (actor != null) actor.AllowPushing = allowPushing;
            }

            public override void MoveVExact(int move)
            {
                bool collidable = Entity.Collidable;
                Entity.Collidable = false;
                bool allowPushing = true;
                var actor = Entity as SourceActorEntityState;
                if (actor != null)
                {
                    allowPushing = actor.AllowPushing;
                    actor.AllowPushing = false;
                }

                base.MoveVExact(move);
                Entity.Collidable = collidable;
                if (actor != null) actor.AllowPushing = allowPushing;
            }
        }
    }
}

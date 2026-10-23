using System;
using System.Collections.Generic;
using System.Reflection;
using System.Numerics;
using Celeste.Mods.EeveeHelper.Entities.Containers;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Mods.EeveeHelper.Entities.Modifiers;
using Celeste.Mods.EeveeHelper.Runtime.Modifiers;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.EeveeHelper.Hooks
{
    public sealed class CollidableModifierHooks : IDisposable
    {
        private readonly CollidableModifierRuntime runtime;
        private readonly List<IDisposable> hooks = new();

        public CollidableModifierHooks(CollidableModifierRuntime runtime)
        {
            this.runtime = runtime;
            try
            {
                hooks.Add(new Hook(typeof(SourceEntityCollision).GetMethod(nameof(SourceEntityCollision.Check)),
                    (CollisionHook)Check));
                hooks.Add(new Hook(typeof(SourceActorEntityState).GetMethod(nameof(SourceActorEntityState.MoveHExact)),
                    (MoveHook)MoveH));
                hooks.Add(new Hook(typeof(SourceActorEntityState).GetMethod(nameof(SourceActorEntityState.MoveVExact)),
                    (MoveHook)MoveV));
                hooks.Add(new Hook(
                    typeof(SourceActorEntityState).GetMethod(nameof(SourceActorEntityState.OnGround),
                        new[] { typeof(int) }), (GroundHook)OnGround));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Contains(ISourceEntityCollider a, ISourceEntityCollider b) =>
            a is CollidableModifier.Solidifier solid && ReferenceEquals(solid.Entity, b) &&
            ReferenceEquals(solid.EngineContext, runtime.Engine);

        private bool Check(CollisionOriginal original, ISourceEntityCollider a, ISourceEntityCollider b)
        {
            bool result = original(a, b);
            if (!result) return false;
            return !Contains(a, b) && !Contains(b, a);
        }

        private bool MoveH(MoveOriginal original, SourceActorEntityState self, int amount,
            SourceCollisionHandler collision, ISourceEntityCollider pusher)
        {
            if (!ReferenceEquals(self.ActorEngineContext, runtime.Engine))
                return original(self, amount, collision, pusher);

            bool exists = runtime.TryGetSolidifier(self, out var solidified);
            bool collidable = true;
            if (exists)
            {
                collidable = solidified.Collidable;
                solidified.Collidable = false;
            }

            bool result = self is HoldableContainer container
                ? MoveContainerH(original, container, amount, collision, pusher)
                : original(self, amount, collision, pusher);
            if (exists && solidified != null)
            {
                solidified.Collidable = collidable;
                solidified.MoveTo(self.ExactPosition);
            }

            return result;
        }

        private bool MoveV(MoveOriginal original, SourceActorEntityState self, int amount,
            SourceCollisionHandler collision, ISourceEntityCollider pusher)
        {
            if (!ReferenceEquals(self.ActorEngineContext, runtime.Engine))
                return original(self, amount, collision, pusher);

            bool exists = runtime.TryGetSolidifier(self, out var solidified);
            bool collidable = true;
            if (exists)
            {
                collidable = solidified.Collidable;
                solidified.Collidable = false;
            }

            bool result = self is HoldableContainer container
                ? MoveContainerV(original, container, amount, collision, pusher)
                : original(self, amount, collision, pusher);
            if (exists)
            {
                solidified.Collidable = collidable;
                bool actorCollidable = self.Collidable, pushable = self.AllowPushing;
                self.Collidable = false;
                self.AllowPushing = false;
                solidified.MoveTo(self.ExactPosition);
                self.Collidable = actorCollidable;
                self.AllowPushing = pushable;
            }

            return result;
        }

        private bool OnGround(GroundOriginal original, SourceActorEntityState self, int downCheck)
        {
            if (!ReferenceEquals(self.ActorEngineContext, runtime.Engine)) return original(self, downCheck);

            bool exists = runtime.TryGetSolidifier(self, out var solidified);
            bool collidable = true;
            if (exists)
            {
                collidable = solidified.Collidable;
                solidified.Collidable = false;
            }

            bool result = self is HoldableContainer container
                ? ContainerOnGround(original, container, downCheck)
                : original(self, downCheck);
            if (exists) solidified.Collidable = collidable;
            return result;
        }

        private static bool MoveContainerH(MoveOriginal original, HoldableContainer container, int amount,
            SourceCollisionHandler collision, ISourceEntityCollider pusher)
        {
            bool result = true;
            container._Container.DoMoveAction(() => result = original(container, amount, collision, pusher),
                (entity, move) => entity is SourcePlatformEntityState platform
                    ? new Vector2(platform.LiftSpeed.X + container.Speed.X, platform.LiftSpeed.Y)
                    : move);
            return result;
        }

        private static bool MoveContainerV(MoveOriginal original, HoldableContainer container, int amount,
            SourceCollisionHandler collision, ISourceEntityCollider pusher)
        {
            bool result = true;
            container._Container.DoMoveAction(() => result = original(container, amount, collision, pusher),
                (entity, move) => entity is SourcePlatformEntityState platform
                    ? new Vector2(platform.LiftSpeed.X, platform.LiftSpeed.Y + container.Speed.Y)
                    : move);
            return result;
        }

        private static bool ContainerOnGround(GroundOriginal original, HoldableContainer container, int downCheck)
        {
            bool result = true;
            container._Container.DoIgnoreCollision(() => result = original(container, downCheck));
            return result;
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }

        private delegate bool CollisionOriginal(ISourceEntityCollider a, ISourceEntityCollider b);

        private delegate bool CollisionHook(CollisionOriginal original, ISourceEntityCollider a,
            ISourceEntityCollider b);

        private delegate bool MoveOriginal(SourceActorEntityState self, int amount, SourceCollisionHandler collision,
            ISourceEntityCollider pusher);

        private delegate bool MoveHook(MoveOriginal original, SourceActorEntityState self, int amount,
            SourceCollisionHandler collision, ISourceEntityCollider pusher);

        private delegate bool GroundOriginal(SourceActorEntityState self, int downCheck);

        private delegate bool GroundHook(GroundOriginal original, SourceActorEntityState self, int downCheck);
    }
}

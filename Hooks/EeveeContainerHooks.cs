using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Mods.EeveeHelper.Entities.Containers;
using Celeste.Mods.EeveeHelper.Runtime;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Riding;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Presentation.Decals.Entities;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.EeveeHelper.Hooks
{
    public sealed class EeveeContainerHooks : IDisposable
    {
        private readonly EeveeContainerRuntime runtime;
        private readonly List<IDisposable> hooks = [];

        public EeveeContainerHooks(EeveeContainerRuntime runtime)
        {
            this.runtime = runtime;
            try
            {
                hooks.Add(new Hook(typeof(SourceEntityCollision).GetMethod(nameof(SourceEntityCollision.Check)),
                    (CollisionHook)Check));
                hooks.Add(new Hook(typeof(SourceStaticMoverState).GetMethod(nameof(SourceStaticMoverState.Move)),
                    (StaticMoveHook)StaticMove));
                hooks.Add(new ILHook(typeof(HoldableState).GetMethod(nameof(HoldableState.Release)), Release));
                var solid = typeof(SourceSolidEntityState);
                hooks.Add(new ILHook(solid.GetMethod(nameof(SourceSolidEntityState.MoveHExact)), AdjustLiftSpeed));
                hooks.Add(new ILHook(solid.GetMethod(nameof(SourceSolidEntityState.MoveVExact)), AdjustLiftSpeed));
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                hooks.Add(new ILHook(solid.GetMethod("PushActorH", flags), AdjustLiftSpeed));
                hooks.Add(new ILHook(solid.GetMethod("PushActorV", flags), AdjustLiftSpeed));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(SceneEntityState entity) => entity.Scene is ISourceEngineContextScene scene &&
                                                      ReferenceEquals(scene.EngineContext, runtime.Engine);

        private bool Check(CollisionOriginal original, ISourceEntityCollider a, ISourceEntityCollider b)
        {
            bool result = original(a, b);
            if (!result) return false;
            return !Contains(a, b) && !Contains(b, a);
        }

        private bool Contains(ISourceEntityCollider a, ISourceEntityCollider b) =>
            a is IContainer container && container.Container is { } contents &&
            ReferenceEquals(contents.Engine, runtime.Engine) && !contents.CollideWithContained &&
            b is SceneEntityState entity && contents.ContainsEntity(entity);

        private void StaticMove(StaticMoveOriginal original, SourceStaticMoverState mover, Vector2 amount)
        {
            if (runtime.DecalStaticMoverFix && mover.Platform is SourceSolidEntityState solid && Owns(solid) &&
                mover.Entity is SourceDecalEntityState) return;
            original(mover, amount);
        }

        private void Release(ILContext il)
        {
            var cursor = new ILCursor(il);
            ILLabel end = null;
            if (!cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchBrfalse(out end)))
                throw new InvalidOperationException("Holdable original release squish branch is missing.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<HoldableState, bool>>(holdable => holdable.Entity is HoldableContainer container &&
                                                                       ReferenceEquals(container.ActorEngineContext,
                                                                           runtime.Engine));
            cursor.Emit(OpCodes.Brtrue, end);
        }

        private void AdjustLiftSpeed(ILContext il)
        {
            var cursor = new ILCursor(il);
            int count = 0;
            while (cursor.TryGotoNext(MoveType.Before,
                       instruction => instruction.MatchCallOrCallvirt(typeof(ISourcePlatformActor), "set_LiftSpeed")))
            {
                cursor.Remove();
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<ISourcePlatformActor, Vector2, SourceSolidEntityState>>((actor, value,
                    solid) =>
                {
                    if (!(runtime.LiftSpeedFix && Owns(solid))) actor.LiftSpeed = value;
                });
                count++;
            }

            if (count != 1)
                throw new InvalidOperationException("Expected one original solid actor lift assignment: " +
                                                    il.Method.FullName);
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate bool CollisionOriginal(ISourceEntityCollider a, ISourceEntityCollider b);

        private delegate bool CollisionHook(CollisionOriginal original, ISourceEntityCollider a,
            ISourceEntityCollider b);

        private delegate void StaticMoveOriginal(SourceStaticMoverState mover, Vector2 amount);

        private delegate void StaticMoveHook(StaticMoveOriginal original, SourceStaticMoverState mover, Vector2 amount);
    }
}

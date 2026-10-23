using System;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.EeveeHelper.Components;
using Celeste.Scene.Entities;
using Celeste.Scene.Scheduling;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Celeste.Mods.EeveeHelper.Hooks
{
    public sealed partial class GlobalModifierHooks
    {
        private void LoadMapPhases(ILContext il)
        {
            var cursor = new ILCursor(il);
            int transforms = 0, starts = 0, ends = 0;
            while (cursor.TryGotoNext(MoveType.After, instruction => instruction.OpCode == OpCodes.Ldfld &&
                                                                     instruction.Operand is FieldReference field &&
                                                                     field.DeclaringType.FullName ==
                                                                     typeof(Celeste.Game.Content.Maps.Levels.
                                                                         OriginalLevelRoomData).FullName &&
                                                                     (field.Name == "Entities" ||
                                                                      field.Name == "Triggers")))
            {
                bool triggers = ((FieldReference)cursor.Prev.Operand).Name == "Triggers";
                cursor.Emit(OpCodes.Ldarg_0);
                cursor
                    .EmitDelegate<
                        Func<System.Collections.Generic.List<OriginalLevelEntityData>, OriginalLevelSceneRoot,
                            System.Collections.Generic.List<OriginalLevelEntityData>>>((list, level) =>
                        Transform(list, level, triggers));
                transforms++;
            }

            cursor.Index = 0;
            while (cursor.TryGotoNext(MoveType.After, instruction => IsRecordEnumerator(instruction, "get_Current")))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<OriginalLevelEntityData, OriginalLevelSceneRoot, OriginalLevelEntityData>>((
                    record, level) =>
                {
                    if (Owns(level))
                    {
                        loadingTags = record.Int("globalModifierSpawned", -1);
                        if (loadingTags != -1) firstNewEntity = level.Scheduler.GetToAdd().Count;
                    }

                    return record;
                });
                starts++;
            }

            cursor.Index = 0;
            while (cursor.TryGotoNext(MoveType.Before, instruction => IsRecordEnumerator(instruction, "MoveNext")))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<OriginalLevelSceneRoot>>(level =>
                {
                    if (Owns(level) && loadingTags != -1)
                    {
                        AddTags(level.Scheduler, firstNewEntity, loadingTags);
                        loadingTags = -1;
                        firstNewEntity = 0;
                    }
                });
                cursor.Index++;
                ends++;
            }

            if (transforms != 2 || starts != 2 || ends != 2)
                throw new InvalidOperationException("Expected two original map list, record start and end sites, got " +
                                                    transforms + "/" + starts + "/" + ends);
        }

        private static bool IsRecordEnumerator(Instruction instruction, string name) =>
            (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
            instruction.Operand is MethodReference method && method.Name == name &&
            method.DeclaringType.FullName.Contains(typeof(OriginalLevelEntityData).FullName);

        private static void AddTags(SceneUpdateScheduler scheduler, int first, int tags)
        {
            var queued = scheduler.GetToAdd();
            for (int i = first; i < queued.Count; i++)
                if (queued[i] is SceneEntityState entity)
                    entity.Add(new TagAdderComponent(tags));
        }

        private sealed class AddedLocal
        {
            public SceneEntityState Entity;
        }

        private readonly ConditionalWeakTable<SceneUpdateScheduler, AddedLocal> addedLocals = new();

        private bool Owns(SceneUpdateScheduler scheduler) =>
            scheduler.ActualSceneIdentity is ISourceEngineContextScene scene &&
            ReferenceEquals(scene.EngineContext, engine);

        private void Commit(CommitOriginal original, SceneUpdateScheduler scheduler)
        {
            if (!Owns(scheduler))
            {
                original(scheduler);
                return;
            }

            var local = addedLocals.GetValue(scheduler, static _ => new());
            var previous = local.Entity;
            local.Entity = null;
            try
            {
                original(scheduler);
            }
            finally
            {
                local.Entity = previous;
            }
        }

        private void AddedLifecycle(LifecycleOriginal original, SceneUpdateScheduler scheduler, object value)
        {
            if (!Owns(scheduler))
            {
                original(scheduler, value);
                return;
            }

            if (addedLocals.TryGetValue(scheduler, out var local)) local.Entity = value as SceneEntityState;
            Propagate(original, scheduler, value, value as SceneEntityState);
        }

        private void AwakeLifecycle(LifecycleOriginal original, SceneUpdateScheduler scheduler, object value)
        {
            if (!Owns(scheduler))
            {
                original(scheduler, value);
                return;
            }

            // The installed IL reads its Added-loop local during Awake, including its last assignment.
            addedLocals.TryGetValue(scheduler, out var local);
            Propagate(original, scheduler, value, local?.Entity);
        }

        private static void Propagate(LifecycleOriginal original, SceneUpdateScheduler scheduler, object value,
            SceneEntityState source)
        {
            if (source?.Get<TagAdderComponent>() is not { } component)
            {
                original(scheduler, value);
                return;
            }

            int first = scheduler.GetToAdd().Count;
            original(scheduler, value);
            AddTags(scheduler, first, component.Tags);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Audio;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Entities.Platforms.SwapBlocks;
using Celeste.Game.Entities.Platforms.ZipMovers;
using Celeste.Mods.EeveeHelper.Runtime;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mods.EeveeHelper.Handlers.Impl
{
    public sealed class HandlersMotionHooks : IDisposable
    {
        private static readonly HandlerField<OriginalZipMoverState, ISourceEngineContext> ZipEngine = new("engine");
        private static readonly HandlerField<OriginalZipMoverState, Vector2> ZipStart = new("start");
        private static readonly HandlerField<OriginalSwapBlockState, ISourceEngineContext> SwapEngine = new("engine");
        private static readonly HandlerField<OriginalSwapBlockState, float> SwapLerp = new("lerp");
        private static readonly HandlerField<OriginalSwapBlockState, int> SwapTarget = new("target");

        private static readonly HandlerField<OriginalSwapBlockState, ISourceAudioEventReference> MoveSound =
            new("moveSfx");

        private static readonly HandlerField<OriginalSwapBlockState, ISourceAudioEventReference> ReturnSound =
            new("returnSfx");

        private readonly ISourceEngineContext engine;
        private readonly List<IDisposable> hooks = new();
        private HandlersMotionHooks(ISourceEngineContext engine) => this.engine = engine;

        public static HandlersMotionHooks Install(ISourceEngineContext engine)
        {
            var process = new HandlersMotionHooks(engine);
            try
            {
                process.hooks.Add(new ILHook(
                    typeof(OriginalZipMoverState).GetMethod("Sequence", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetStateMachineTarget(), process.ZipSequence));
                process.hooks.Add(new ILHook(typeof(OriginalSwapBlockState).GetMethod("Update")!, process.SwapUpdate));
                return process;
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }

        private void ZipSequence(ILContext il)
        {
            FieldReference capturedStart = null;
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction =>
                        instruction.MatchCallOrCallvirt<Celeste.Scene.Entities.SceneEntityState>("get_Position"),
                    instruction => instruction.MatchStfld(out capturedStart)))
                throw new InvalidOperationException("ZipMover sequence original start capture is not present.");
            FieldReference self = null;
            foreach (var instruction in il.Body.Instructions)
                if (instruction.OpCode == OpCodes.Ldfld && instruction.Operand is FieldReference field &&
                    field.FieldType.FullName == typeof(OriginalZipMoverState).FullName)
                {
                    self = field;
                    break;
                }

            if (self == null) throw new InvalidOperationException("ZipMover sequence actual owner field is missing.");
            int matches = 0;
            while (cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdfld(capturedStart)))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, self);
                cursor.EmitDelegate<Func<Vector2, OriginalZipMoverState, Vector2>>(ResolveZipStart);
                matches++;
            }

            if (matches != 2)
                throw new InvalidOperationException("ZipMover original captured-start read count differs: " + matches);
        }

        private Vector2 ResolveZipStart(Vector2 original, OriginalZipMoverState entity)
        {
            if (!ReferenceEquals(ZipEngine.Read(entity), engine)) return original;
            var data = EeveeEntityData.For(engine, entity);
            return data.TryGet<bool>("zipMoverNodeHandled", out var handled) && handled
                ? ZipStart.Read(entity)
                : original;
        }

        private void SwapUpdate(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("op_Inequality")))
                throw new InvalidOperationException("SwapBlock original position-change comparison is missing.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, OriginalSwapBlockState, bool>>(ResolveSwapComparison);
        }

        private bool ResolveSwapComparison(bool original, OriginalSwapBlockState entity)
        {
            if (!ReferenceEquals(SwapEngine.Read(entity), engine) || !EeveeEntityData.For(engine, entity)
                    .Extras.ContainsKey(SwapBlockHandler.HandledString)) return original;
            var audio = ((ISourceSpatialAudioEnvironment)engine).SpatialAudio;
            var moveSound = MoveSound.Read(entity);
            var returnSound = ReturnSound.Read(entity);
            audio.Position(moveSound, entity.Center);
            audio.Position(returnSound, entity.Center);
            int target = SwapTarget.Read(entity);
            if (SwapLerp.Read(entity) == target)
            {
                if (target == 0)
                {
                    ((IOriginalGameplayProcess)engine).AudioSession.SetParameter(returnSound, "end", 1f);
                    audio.Play("event:/game/05_mirror_temple/swapblock_return_end", entity.Center);
                }
                else audio.Play("event:/game/05_mirror_temple/swapblock_move_end", entity.Center);
            }

            return false;
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}

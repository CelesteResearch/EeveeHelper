using System;
using System.Reflection;
using Celeste.Core.Rendering;
using Celeste.Core.Rendering.States;
using Celeste.Core.Scheduling;
using Celeste.Game.Content.Maps.Backdrops;
using Celeste.Game.Execution.Graphics;
using Celeste.Graphics.Content;
using Celeste.Simulation.Presentation.Backdrops.Parallax;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.EeveeHelper.Hooks
{
    internal sealed class BackdropBlendHooks : IDisposable
    {
        private static readonly SourceBlendState MultiplyBlend = new()
        {
            ColorBlendFunction = SourceBlendFunction.Add,
            ColorSourceBlend = SourceBlendFactor.DestinationColor,
            ColorDestinationBlend = SourceBlendFactor.Zero
        };

        private static readonly SourceBlendState ReverseSubtractBlend = new()
        {
            ColorSourceBlend = SourceBlendFactor.One,
            ColorDestinationBlend = SourceBlendFactor.One,
            ColorBlendFunction = SourceBlendFunction.Subtract,
            AlphaSourceBlend = SourceBlendFactor.One,
            AlphaDestinationBlend = SourceBlendFactor.One,
            AlphaBlendFunction = SourceBlendFunction.Add
        };

        private static readonly FieldInfo ParserEngine = typeof(MapBackdropParser)
            .GetField("engine", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly ISourceEngineContext engine;
        private readonly ILHook hook;

        internal BackdropBlendHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            hook = new ILHook(typeof(MapBackdropParser).GetMethod(nameof(MapBackdropParser.ParseBackdrop)), Patch);
        }

        private void Patch(ILContext il)
        {
            var cursor = new ILCursor(il);
            int parallax = -1;
            if (!cursor.TryGotoNext(instruction => instruction.MatchNewobj<ParallaxBackdropPresentation>()) ||
                !cursor.TryGotoNext(instruction => instruction.MatchStloc(out parallax)))
                throw new InvalidOperationException("EeveeHelper cannot find the native parallax construction.");
            int mode = -1;
            if (!cursor.TryGotoNext(MoveType.AfterLabel, instruction => instruction.MatchLdloc(out mode),
                    instruction => instruction.MatchLdstr("additive")))
                throw new InvalidOperationException("EeveeHelper cannot find the native parallax blend mode.");
            cursor.Emit(OpCodes.Ldloc, parallax);
            cursor.Emit(OpCodes.Ldloc, mode);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, ParserEngine);
            cursor.EmitDelegate<Action<ParallaxBackdropPresentation, string, ISourceEngineContext>>(Apply);
        }

        private void Apply(ParallaxBackdropPresentation parallax, string mode, ISourceEngineContext actualEngine)
        {
            if (!ReferenceEquals(actualEngine, engine)) return;
            switch (mode)
            {
                case "multiply":
                    parallax.BlendState = MultiplyBlend;
                    break;
                case "subtract":
                    parallax.BlendState = ((OriginalGameGraphicsResources)((ISourceGameGraphicsEnvironment)engine)
                        .GameGraphics).Subtract;
                    break;
                case "reversesubtract":
                    parallax.BlendState = ReverseSubtractBlend;
                    break;
            }
        }

        public void Dispose() => hook.Dispose();
    }
}

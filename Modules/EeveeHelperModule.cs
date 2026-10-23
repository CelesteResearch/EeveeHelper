using System;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Game.Entities.Decoration.Decals;
using Celeste.Game.Entities.Platforms.SwapBlocks;
using Celeste.Game.Entities.Platforms.ZipMovers;
using Celeste.Game.Gameplay.Environment.Water;
using Celeste.Game.Gameplay.Hazards.Spinners.Moving;
using Celeste.Game.Gameplay.Mechanisms.DashSwitches;
using Celeste.Mods.EeveeHelper.Handlers.Impl;
using Celeste.Mods.EeveeHelper.Hooks;
using Celeste.Mods.EeveeHelper.Runtime;

namespace Celeste.Mods.EeveeHelper.Modules
{
    public sealed class EeveeHelperModule : SourceEverestModule
    {
        private EeveeContainerRuntime runtime;
        private EeveeContainerHooks containerHooks;
        private GlobalModifierHooks globalModifierHooks;
        private CollidableModifierHooks collidableModifierHooks;
        private IDisposable handlersMotionHooks;
        private BackdropBlendHooks backdropBlendHooks;

        public EeveeHelperModule(ISourceEngineContext engine) : base(engine)
        {
        }

        public override void Load()
        {
            runtime = EeveeContainerRuntime.For(SourceEngine);
            try
            {
                var handlers = runtime.Handlers;
                handlers.RegisterInherited(typeof(OriginalWaterState), (entity, container) => new WaterHandler(entity));
                handlers.RegisterInherited(typeof(OriginalTrackSpinnerState),
                    (entity, container) => new TrackSpinnerHandler(entity));
                handlers.RegisterInherited(typeof(OriginalRotateSpinnerState),
                    (entity, container) => new RotateSpinnerHandler(entity));
                handlers.RegisterInherited(typeof(OriginalDashSwitchState),
                    (entity, container) =>
                        new AxisMoverHandler(SourceEngine, entity, new Tuple<string, bool>("startY", true)));
                handlers.RegisterInherited(typeof(OriginalZipMoverState),
                    (entity, container) => new ZipMoverNodeHandler(SourceEngine, entity, true),
                    (entity, container) =>
                        ZipMoverNodeHandler.InsideCheck(container, true, entity as OriginalZipMoverState));
                handlers.RegisterInherited(typeof(OriginalZipMoverState),
                    (entity, container) => new ZipMoverNodeHandler(SourceEngine, entity, false),
                    (entity, container) =>
                        ZipMoverNodeHandler.InsideCheck(container, false, entity as OriginalZipMoverState));
                handlers.RegisterInherited(typeof(OriginalSwapBlockState),
                    (entity, container) => new SwapBlockHandler(SourceEngine, entity, true),
                    (entity, container) =>
                        SwapBlockHandler.InsideCheck(container, true, entity as OriginalSwapBlockState));
                handlers.RegisterInherited(typeof(OriginalSwapBlockState),
                    (entity, container) => new SwapBlockHandler(SourceEngine, entity, false),
                    (entity, container) =>
                        SwapBlockHandler.InsideCheck(container, false, entity as OriginalSwapBlockState));
                handlers.RegisterInherited(typeof(OriginalDecalEntity), (entity, container) => new DecalHandler(entity),
                    (entity, container) => container.CheckDecal(entity as OriginalDecalEntity));
                backdropBlendHooks = new BackdropBlendHooks(SourceEngine);
                containerHooks = new EeveeContainerHooks(runtime);
                handlersMotionHooks = HandlersMotionHooks.Install(SourceEngine);
                globalModifierHooks = new GlobalModifierHooks(SourceEngine);
                collidableModifierHooks =
                    new CollidableModifierHooks(Runtime.Modifiers.CollidableModifierRuntime.For(SourceEngine));
            }
            catch
            {
                Unload();
                throw;
            }
        }

        public override void Unload()
        {
            backdropBlendHooks?.Dispose();
            backdropBlendHooks = null;
            collidableModifierHooks?.Dispose();
            collidableModifierHooks = null;
            globalModifierHooks?.Dispose();
            globalModifierHooks = null;
            handlersMotionHooks?.Dispose();
            handlersMotionHooks = null;
            containerHooks?.Dispose();
            containerHooks = null;
            runtime?.Clear();
            runtime = null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Scheduling;
using Celeste.Game.Entities.Platforms.SwapBlocks;
using Celeste.Mods.EeveeHelper.Components;
using Celeste.Mods.EeveeHelper.Runtime;
using Celeste.Scene.Entities;

namespace Celeste.Mods.EeveeHelper.Handlers.Impl
{
    public class SwapBlockHandler : EntityHandler, IMoveable, IAnchorProvider
    {
        public const string HandledString = "EeveeHelper_SwapBlockHandled";
        private static readonly HandlerField<OriginalSwapBlockState, Vector2> Start = new("start");
        private static readonly HandlerField<OriginalSwapBlockState, Vector2> End = new("end");
        private static readonly HandlerField<OriginalSwapBlockState, float> Lerp = new("lerp");
        private static readonly HandlerField<OriginalSwapBlockState, IntRect> MoveRect = new("moveRect");
        private readonly OriginalSwapBlockState swapBlock;
        private readonly ISourceEngineContext engine;
        private readonly bool first;

        public SwapBlockHandler(ISourceEngineContext engine, SceneEntityState entity, bool first) : base(entity)
        {
            this.engine = engine;
            swapBlock = entity as OriginalSwapBlockState;
            this.first = first;
            EeveeEntityData.For(engine, swapBlock).Set(HandledString, true);
        }

        public override bool IsInside(EntityContainer container) => InsideCheck(container, first, swapBlock);

        public override IntRect GetBounds()
        {
            var position = first ? Start.Read(swapBlock) : End.Read(swapBlock);
            return new IntRect((int)position.X, (int)position.Y, 0, 0);
        }

        public bool Move(Vector2 move, Vector2? liftSpeed)
        {
            if (first) Start.Write(swapBlock, SourceVector2Math.Add(Start.Read(swapBlock), move));
            else End.Write(swapBlock, SourceVector2Math.Add(End.Read(swapBlock), move));
            var start = Start.Read(swapBlock);
            var end = End.Read(swapBlock);
            var position = Vector2Interpolation.Lerp(start, end, Lerp.Read(swapBlock),
                ((ISourceScalarEnvironment)engine).ScalarArithmetic);
            swapBlock.MoveTo(position, swapBlock.LiftSpeed);
            if (first)
            {
                var rectangle = MoveRect.Read(swapBlock);
                rectangle.X = Math.Min((int)end.X, (int)start.X);
                rectangle.Y = Math.Min((int)end.Y, (int)start.Y);
                MoveRect.Write(swapBlock, rectangle);
            }

            return true;
        }

        public void PreMove()
        {
        }

        public List<string> GetAnchors() => new();

        public static bool InsideCheck(EntityContainer container, bool first, OriginalSwapBlockState entity)
        {
            var position = first ? Start.Read(entity) : End.Read(entity);
            return position.X >= container.Entity.Left && position.Y >= container.Entity.Top &&
                   position.X + container.Entity.Width <= container.Entity.Right &&
                   position.Y + container.Entity.Height <= container.Entity.Bottom;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Scheduling;
using Celeste.Game.Entities.Platforms.ZipMovers;
using Celeste.Mods.EeveeHelper.Components;
using Celeste.Mods.EeveeHelper.Runtime;
using Celeste.Scene.Entities;

namespace Celeste.Mods.EeveeHelper.Handlers.Impl
{
    public class ZipMoverNodeHandler : EntityHandler, IMoveable, IAnchorProvider
    {
        private static readonly HandlerField<OriginalZipMoverState, Vector2> Start = new("start");
        private static readonly HandlerField<OriginalZipMoverState, Vector2> Target = new("target");
        private static readonly HandlerField<OriginalZipMoverState, float> Percent = new("percent");

        private static readonly HandlerField<OriginalZipMoverState, OriginalZipMoverState.ZipMoverPathRenderer>
            PathRenderer = new("pathRenderer");

        private static readonly HandlerField<OriginalZipMoverState.ZipMoverPathRenderer, Vector2> From = new("from");
        private static readonly HandlerField<OriginalZipMoverState.ZipMoverPathRenderer, Vector2> To = new("to");

        private static readonly HandlerField<OriginalZipMoverState.ZipMoverPathRenderer, Vector2> SparkAdd =
            new("sparkAdd");

        private static readonly HandlerField<OriginalZipMoverState.ZipMoverPathRenderer, float> SparkDirFromA =
            new("sparkDirFromA");

        private static readonly HandlerField<OriginalZipMoverState.ZipMoverPathRenderer, float> SparkDirFromB =
            new("sparkDirFromB");

        private static readonly HandlerField<OriginalZipMoverState.ZipMoverPathRenderer, float> SparkDirToA =
            new("sparkDirToA");

        private static readonly HandlerField<OriginalZipMoverState.ZipMoverPathRenderer, float> SparkDirToB =
            new("sparkDirToB");

        private readonly OriginalZipMoverState zipMover;
        private readonly ISourceEngineContext engine;
        private readonly bool first;

        public ZipMoverNodeHandler(ISourceEngineContext engine, SceneEntityState entity, bool first) : base(entity)
        {
            this.engine = engine;
            zipMover = entity as OriginalZipMoverState;
            this.first = first;
            if (first) EeveeEntityData.For(engine, zipMover).Set("zipMoverNodeHandled", true);
        }

        public override bool IsInside(EntityContainer container) => InsideCheck(container, first, zipMover);

        public override IntRect GetBounds()
        {
            var position = first ? Start.Read(zipMover) : Target.Read(zipMover);
            return new IntRect((int)position.X, (int)position.Y, 0, 0);
        }

        public bool Move(Vector2 move, Vector2? liftSpeed)
        {
            if (first) Start.Write(zipMover, SourceVector2Math.Add(Start.Read(zipMover), move));
            else Target.Write(zipMover, SourceVector2Math.Add(Target.Read(zipMover), move));
            var start = Start.Read(zipMover);
            var target = Target.Read(zipMover);
            var position = Vector2Interpolation.Lerp(start, target, Percent.Read(zipMover),
                ((ISourceScalarEnvironment)engine).ScalarArithmetic);
            zipMover.MoveTo(position, zipMover.LiftSpeed);
            if (PathRenderer.Read(zipMover) != null) UpdatePathRenderer(start, target);
            return true;
        }

        private void UpdatePathRenderer(Vector2 newFrom, Vector2 newTo)
        {
            var path = PathRenderer.Read(zipMover);
            var from = SourceVector2Math.Add(newFrom, new Vector2(Entity.Width / 2f, Entity.Height / 2f));
            var to = SourceVector2Math.Add(newTo, new Vector2(Entity.Width / 2f, Entity.Height / 2f));
            var difference = SourceVector2Math.Subtract(from, to);
            var angle = Vector2Geometry.Angle(difference);
            From.Write(path, from);
            To.Write(path, to);
            SparkAdd.Write(path,
                Vector2Geometry.Perpendicular(
                    ((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(difference, 5f)));
            SparkDirFromA.Write(path, angle + (float)Math.PI / 8f);
            SparkDirFromB.Write(path, angle - (float)Math.PI / 8f);
            SparkDirToA.Write(path, angle + (float)Math.PI - (float)Math.PI / 8f);
            SparkDirToB.Write(path, angle + (float)Math.PI + (float)Math.PI / 8f);
        }

        public void PreMove()
        {
        }

        public List<string> GetAnchors() => new();

        public static bool InsideCheck(EntityContainer container, bool first, OriginalZipMoverState entity)
        {
            var position = first ? Start.Read(entity) : Target.Read(entity);
            return position.X >= container.Entity.Left && position.Y >= container.Entity.Top &&
                   position.X <= container.Entity.Right && position.Y <= container.Entity.Bottom;
        }
    }
}

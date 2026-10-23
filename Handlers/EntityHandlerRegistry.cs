using System;
using System.Collections.Generic;
using Celeste.Mods.EeveeHelper.Components;
using Celeste.Mods.EeveeHelper.Runtime;
using Celeste.Scene.Entities;

namespace Celeste.Mods.EeveeHelper.Handlers
{
    public sealed class EntityHandlerRegistry
    {
        private readonly EeveeContainerRuntime runtime;
        public Dictionary<Type, List<EntityHandlerRegistration>> TypeFactories { get; } = new();
        public List<EntityHandlerRegistration> GeneralFactories { get; } = new();
        internal EntityHandlerRegistry(EeveeContainerRuntime runtime) => this.runtime = runtime;

        public List<IEntityHandler> CreateAll(SceneEntityState entity, EntityContainer container,
            bool forceDefault = false)
        {
            var list = new List<IEntityHandler>();
            if (!forceDefault)
            {
                if (TypeFactories.TryGetValue(entity.GetType(), out var factories))
                    list.AddRange(Select(factories, entity, container));
                list.AddRange(Select(GeneralFactories, entity, container));
            }

            if (list.Count == 0 && EntityHandler.DefaultInsideCheck(entity, container))
                list.Add(new EntityHandler(entity));
            return new List<IEntityHandler>(list);
        }

        private static IEnumerable<IEntityHandler> Select(List<EntityHandlerRegistration> registrations,
            SceneEntityState entity, EntityContainer container)
        {
            // The pinned selector stores IsValid but invokes the constructor without evaluating it.
            foreach (var registration in registrations)
            {
                var handler = registration.Constructor(entity, container);
                if (handler != null) yield return handler;
            }
        }

        public void Register(Type type, EntityHandlerFactory factory, EntityHandlerIsValid valid = null)
        {
            if (!TypeFactories.TryGetValue(type, out var factories)) TypeFactories[type] = factories = new();
            factories.Add(new EntityHandlerRegistration
                { Constructor = factory, IsValid = valid ?? EntityHandler.DefaultInsideCheck });
        }

        public void Register(EntityHandlerFactory factory, EntityHandlerIsValid valid = null) => GeneralFactories.Add(
            new EntityHandlerRegistration
                { Constructor = factory, IsValid = valid ?? EntityHandler.DefaultInsideCheck });

        public void RegisterInherited(Type type, EntityHandlerFactory factory, EntityHandlerIsValid valid = null)
        {
            foreach (var candidate in runtime.GetRuntimeTypes())
                if (type.IsAssignableFrom(candidate))
                    Register(candidate, factory, valid);
        }

        public void Register<T>(EntityHandlerFactory factory, EntityHandlerIsValid valid = null)
            where T : SceneEntityState => Register(typeof(T), factory, valid);

        public void RegisterInherited<T>(EntityHandlerFactory factory, EntityHandlerIsValid valid = null)
            where T : SceneEntityState => RegisterInherited(typeof(T), factory, valid);

        public void Clear()
        {
            TypeFactories.Clear();
            GeneralFactories.Clear();
        }
    }
}

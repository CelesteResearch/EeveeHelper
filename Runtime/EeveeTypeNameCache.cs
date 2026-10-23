using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Celeste.Core.Reflection.Members;
using Celeste.Core.Reflection.Forwarders;
using Celeste.Scene.Tracking;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.EeveeHelper.Runtime
{
    internal sealed class EeveeTypeNameCache : IDisposable
    {
        static readonly FieldInfo sourceField = typeof(SourceMemberCatalog).GetField("actualTypeBindingSource",
            BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly FieldInfo forwardersField =
            typeof(SourceTypeForwarderCatalog).GetField("forwarders", BindingFlags.Instance | BindingFlags.NonPublic);

        readonly MethodDetourInfo[] guarded;
        readonly Dictionary<Type, string> names = new Dictionary<Type, string>();
        readonly int owner = Environment.CurrentManagedThreadId;
        int hooksRevision, observedHooks = -1;
        bool eligible, disposed;
        SourceMemberCatalog catalog;
        long bindingRevision = -1;
        int definitions = -1, forwarderCount = -1;
        ICollection forwarders;

        public EeveeTypeNameCache(MethodBase resolveMethod)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                       BindingFlags.Instance | BindingFlags.DeclaredOnly;
            try
            {
                guarded = new[]
                    {
                        typeof(SourceMemberCatalog), typeof(SceneEntityCatalog), typeof(SourceTypeForwarderCatalog),
                        typeof(SourceTypeDefinition)
                    }
                    .SelectMany(t => t.GetMethods(flags)).Where(m =>
                        !m.ContainsGenericParameters && !m.IsAbstract && m.GetMethodBody() != null)
                    .Cast<MethodBase>().Concat(new MethodBase[]
                        { typeof(object).GetType().GetProperty("Name").GetGetMethod(), resolveMethod })
                    .Distinct().Select(DetourManager.GetDetourInfo).ToArray();
            }
            catch (Exception e)when (e is NotSupportedException || e is ArgumentException ||
                                     e is InvalidOperationException)
            {
                guarded = Array.Empty<MethodDetourInfo>();
                return;
            }

            DetourManager.DetourApplied += Changed;
            DetourManager.DetourUndone += Changed;
            DetourManager.ILHookApplied += Changed;
            DetourManager.ILHookUndone += Changed;
        }

        void Changed(DetourInfo _) => Interlocked.Increment(ref hooksRevision);
        void Changed(ILHookInfo _) => Interlocked.Increment(ref hooksRevision);

        public string Get(Type type, SourceMemberCatalog members, Func<Type, string> resolve)
        {
            if (disposed || Environment.CurrentManagedThreadId != owner || type == null ||
                type.GetType() != typeof(object).GetType()) return resolve(type);
            int revision = Volatile.Read(ref hooksRevision);
            if (revision != observedHooks)
            {
                names.Clear();
                eligible = false;
                try
                {
                    eligible = guarded.Length != 0 && guarded.All(m => !m.IsDetoured);
                }
                catch (Exception e)when (e is InvalidOperationException || e is NotSupportedException ||
                                         e is ArgumentException)
                {
                }

                observedHooks = revision;
            }

            if (!eligible || sourceField == null || forwardersField == null) return resolve(type);
            if (!ReferenceEquals(catalog, members))
            {
                names.Clear();
                catalog = members;
                bindingRevision = -1;
                object source = sourceField.GetValue(members);
                if (source != null && source.GetType() != typeof(SceneEntityCatalog))
                {
                    catalog = null;
                    return resolve(type);
                }

                forwarders = forwardersField.GetValue(members.SourceTypeForwarders) as ICollection;
                if (forwarders == null)
                {
                    catalog = null;
                    return resolve(type);
                }
            }

            long bindings = members.ActualTypeBindingsRevision;
            int defs = members.OriginalTypeDefinitionCount, fw = forwarders.Count;
            // Forwarder publication has its own append-only table, outside the actual binding revision.
            if (bindings != bindingRevision || defs != definitions || fw != forwarderCount)
            {
                object source = sourceField.GetValue(members);
                if (source != null && source.GetType() != typeof(SceneEntityCatalog))
                {
                    names.Clear();
                    catalog = null;
                    return resolve(type);
                }

                names.Clear();
                bindingRevision = bindings;
                definitions = defs;
                forwarderCount = fw;
            }

            if (names.TryGetValue(type, out var result) && revision == Volatile.Read(ref hooksRevision)) return result;
            result = resolve(type);
            if (bindings == members.ActualTypeBindingsRevision && defs == members.OriginalTypeDefinitionCount &&
                fw == forwarders.Count && revision == Volatile.Read(ref hooksRevision))
            {
                if (names.Count >= 4096) names.Clear();
                names[type] = result;
            }

            return result;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            DetourManager.DetourApplied -= Changed;
            DetourManager.DetourUndone -= Changed;
            DetourManager.ILHookApplied -= Changed;
            DetourManager.ILHookUndone -= Changed;
            names.Clear();
            catalog = null;
            forwarders = null;
        }
    }
}

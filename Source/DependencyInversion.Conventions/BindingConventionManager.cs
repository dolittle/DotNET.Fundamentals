﻿/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Dolittle. All rights reserved.
 *  Licensed under the MIT License. See LICENSE in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dolittle.Lifecycle;
using Dolittle.Reflection;
using Dolittle.Types;
using Dolittle.Execution;
using Dolittle.Logging;

namespace Dolittle.DependencyInversion.Conventions
{
    /// <summary>
    /// Represents a <see cref="IBindingConventionManager"/>
    /// </summary>
    [Singleton]
    public class BindingConventionManager : IBindingConventionManager
    {
        readonly ITypeFinder _typeFinder;
        readonly List<Type> _conventions;
        readonly IScheduler _scheduler;
        readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance <see cref="BindingConventionManager"/>
        /// </summary>
        /// <param name="typeFinder"><see cref="ITypeFinder"/> to discover binding conventions with</param>
        /// <param name="scheduler"><see cref="IScheduler"/> used for scheduling work</param>
        /// <param name="logger"><see cref="ILogger"/> used for logging</param>
        public BindingConventionManager(ITypeFinder typeFinder, IScheduler scheduler, ILogger logger)
        {
            _typeFinder = typeFinder;
            _conventions = new List<Type>();
            _scheduler = scheduler;
            _logger = logger;
        }

        /// <inheritdoc/>
        public IBindingCollection DiscoverAndSetupBindings()
        {
            _logger.Information("Discover and setup bindings");
            var bindingCollections = new ConcurrentBag<IBindingCollection>();

            var allTypes = _typeFinder.All;

            _logger.Information("Find all binding conventions");
            var conventionTypes = _typeFinder.FindMultiple<IBindingConvention>();

            _scheduler.PerformForEach(conventionTypes, conventionType => 
            {
                _logger.Information($"Handle convention type {conventionType.AssemblyQualifiedName}");
                ThrowIfBindingConventionIsMissingDefaultConstructor(conventionType);

                var convention = Activator.CreateInstance(conventionType)as IBindingConvention;
                var servicesToResolve = allTypes.Where(service => convention.CanResolve(service));

                var bindings = new ConcurrentBag<Binding>();

                _scheduler.PerformForEach(servicesToResolve, service => 
                {
                    var bindingBuilder = new BindingBuilder(Binding.For(service));
                    convention.Resolve(service, bindingBuilder);
                    bindings.Add(bindingBuilder.Build());
                });

                var bindingCollection = new BindingCollection(bindings);
                bindingCollections.Add(bindingCollection);
            });

            var aggregatedBindingCollection = new BindingCollection(bindingCollections.ToArray());
            return aggregatedBindingCollection;
        }

        static void ThrowIfBindingConventionIsMissingDefaultConstructor(Type bindingProvider)
        {
            if (!bindingProvider.HasDefaultConstructor())throw new BindingConventionMustHaveADefaultConstructor(bindingProvider);
        }
    }
}
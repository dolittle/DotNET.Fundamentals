// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Autofac;
using Machine.Specifications;
using Moq;
using It = Machine.Specifications.It;

namespace Dolittle.DependencyInversion.Autofac.Tenancy.for_InstancesPerTenant
{
    public class when_resolving_callback_binding_with_binding_context : given.no_instances
    {
        static Binding binding;
        static IComponentContext component_context;

        static Type service_type;
        static Type binding_type;
        static Type type;
        static bool callback_called = false;
        static BindingContext binding_context;

        Establish context = () =>
        {
            component_context = Mock.Of<IComponentContext>();
            service_type = typeof(double);
            binding_type = typeof(int);
            type = typeof(string);

            binding = new Binding(
                service_type,
                new Strategies.CallbackWithBindingContext((c) =>
                {
                    callback_called = true;
                    binding_context = c;
                    return new object();
                }), new Scopes.SingletonPerTenant());
            tenant_key_creator.Setup(_ => _.GetKeyFor(binding, type)).Returns("SomeKey");
        };

        Because of = () => instances_per_tenant.Resolve(component_context, binding, type);

        It should_ask_callback_to_create_instance = () => callback_called.ShouldBeTrue();
        It should_pass_binding_context_with_service_type = () => binding_context.Service.ShouldEqual(type);
    }
}
/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Dolittle. All rights reserved.
 *  Licensed under the MIT License. See LICENSE in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System.Collections.Generic;
using Grpc.Core;
using Machine.Specifications;

namespace Dolittle.Grpc.for_Hosts.when_starting
{
    public class with_one_host_enabled : given.one_host_type_with_binder
    {
        static Hosts hosts;

        Establish context = () => 
        {
            configuration.Enabled = true;
            hosts = new Hosts(host_types, type_finder.Object, container.Object, logger);
        };

        Because of = () => hosts.Start();

        It should_bind_services = () => binder.Verify(_ => _.BindServices(), Moq.Times.Once);
        It should_start_host = () => host.Verify(_ => _.Start(identifier, configuration, Moq.It.IsAny<IEnumerable<ServerServiceDefinition>>()), Moq.Times.Once);       
    }    
}
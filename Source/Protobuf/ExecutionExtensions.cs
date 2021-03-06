// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Dolittle.ApplicationModel;
using Dolittle.Execution;
using Dolittle.Tenancy;

namespace Dolittle.Protobuf
{
    /// <summary>
    /// Represents conversion extensions for the common execution types.
    /// </summary>
    public static class ExecutionExtensions
    {
        /// <summary>
        /// Convert a <see cref="ExecutionContext"/> to <see cref="Execution.Contracts.ExecutionContext"/>.
        /// </summary>
        /// <param name="executionContext"><see cref="ExecutionContext"/> to convert from.</param>
        /// <returns>Converted <see cref="Execution.Contracts.ExecutionContext"/>.</returns>
        public static Execution.Contracts.ExecutionContext ToProtobuf(this ExecutionContext executionContext)
        {
            var message = new Execution.Contracts.ExecutionContext
                {
                    MicroserviceId = executionContext.Microservice.ToProtobuf(),
                    TenantId = executionContext.Tenant.ToProtobuf(),
                    CorrelationId = executionContext.CorrelationId.ToProtobuf(),
                    Environment = executionContext.Environment,
                    Version = executionContext.Version.ToProtobuf()
                };
            message.Claims.AddRange(executionContext.Claims.ToProtobuf());

            return message;
        }

        /// <summary>
        /// Convert a <see cref="Execution.Contracts.ExecutionContext"/> to <see cref="ExecutionContext"/>.
        /// </summary>
        /// <param name="executionContext"><see cref="Execution.Contracts.ExecutionContext"/> to convert from.</param>
        /// <returns>Converted <see cref="ExecutionContext"/>.</returns>
        public static ExecutionContext ToExecutionContext(this Execution.Contracts.ExecutionContext executionContext) =>
            new ExecutionContext(
                executionContext.MicroserviceId.To<Microservice>(),
                executionContext.TenantId.To<TenantId>(),
                executionContext.Version.ToVersion(),
                executionContext.Environment,
                executionContext.CorrelationId.To<CorrelationId>(),
                executionContext.Claims.ToClaims(),
                CultureInfo.InvariantCulture);
    }
}
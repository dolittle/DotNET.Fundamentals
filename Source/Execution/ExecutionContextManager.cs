// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Threading;
using Dolittle.Applications;
using Dolittle.Lifecycle;
using Dolittle.Logging;
using Dolittle.Security;
using Dolittle.Tenancy;

namespace Dolittle.Execution
{
    /// <summary>
    /// Represents an implementation of <see cref="IExecutionContextManager"/>.
    /// </summary>
    [Singleton]
    public class ExecutionContextManager : IExecutionContextManager
    {
        static readonly AsyncLocal<ExecutionContext> _executionContext = new AsyncLocal<ExecutionContext>();

        static bool _initialExecutionContextSet = false;

        readonly ILogger _logger;

        Application _application;
        Microservice _microservice;
        Environment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionContextManager"/> class.
        /// </summary>
        /// <param name="logger"><see cref="ILogger"/> for logging.</param>
        public ExecutionContextManager(ILogger logger)
        {
            _logger = logger;
            _application = Application.NotSet;
            _microservice = Microservice.NotSet;
            _environment = Environment.Undetermined;
        }

        /// <inheritdoc/>
        public ExecutionContext Current
        {
            get
            {
                var context = _executionContext.Value;
                if (context == null)
                {
                    throw new ExecutionContextNotSet();
                }

                return context;
            }

            private set
            {
                _executionContext.Value = value;
            }
        }

        /// <summary>
        /// Set the initial <see cref="ExecutionContext"/>.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to use.</param>
        /// <remarks>
        /// This can only be called once per process and is typically called by entrypoints into Dolittle itself.
        /// </remarks>
        /// <returns>An <see cref="ExecutionContext"/> instance.</returns>
        public static ExecutionContext SetInitialExecutionContext(ILogger logger)
        {
            logger.Trace("Setting initial execution context");
            if (_initialExecutionContextSet) throw new InitialExecutionContextHasAlreadyBeenSet();

            _initialExecutionContextSet = true;

            _executionContext.Value = new ExecutionContext(
                Application.NotSet,
                Microservice.NotSet,
                TenantId.System,
                Environment.Undetermined,
                CorrelationId.System,
                Claims.Empty,
                CultureInfo.InvariantCulture);

            return _executionContext.Value;
        }

        /// <inheritdoc/>
        public void SetConstants(
            Application application,
            Microservice microservice,
            Environment environment)
        {
            _application = application;
            _microservice = microservice;
            _environment = environment;
        }

        /// <inheritdoc/>
        public ExecutionContext System(string filePath, int lineNumber, string member) =>
            CurrentFor(TenantId.System, CorrelationId.System, filePath, lineNumber, member);

        /// <inheritdoc/>
        public ExecutionContext System(CorrelationId correlationId, string filePath, int lineNumber, string member) =>
            CurrentFor(TenantId.System, correlationId, filePath, lineNumber, member);

        /// <inheritdoc/>
        public ExecutionContext CurrentFor(TenantId tenant, string filePath, int lineNumber, string member) =>
            CurrentFor(_application, _microservice, tenant, CorrelationId.New(), Claims.Empty, filePath, lineNumber, member);

        /// <inheritdoc/>
        public ExecutionContext CurrentFor(Microservice microservice, TenantId tenant, string filePath, int lineNumber, string member) =>
            CurrentFor(_application, microservice, tenant, CorrelationId.New(), Claims.Empty, filePath, lineNumber, member);

        /// <inheritdoc/>
        public ExecutionContext CurrentFor(Application application, Microservice microservice, TenantId tenant, string filePath, int lineNumber, string member) =>
            CurrentFor(application, microservice, tenant, CorrelationId.New(), Claims.Empty, filePath, lineNumber, member);

        /// <inheritdoc/>
        public ExecutionContext CurrentFor(TenantId tenant, CorrelationId correlationId, string filePath, int lineNumber, string member) =>
            CurrentFor(_application, _microservice, tenant, correlationId, Claims.Empty, filePath, lineNumber, member);

        /// <inheritdoc/>
        public ExecutionContext CurrentFor(Microservice microservice, TenantId tenant, CorrelationId correlationId, string filePath, int lineNumber, string member) =>
            CurrentFor(_application, microservice, tenant, correlationId, Claims.Empty, filePath, lineNumber, member);

        /// <inheritdoc/>
        public ExecutionContext CurrentFor(Application application, Microservice microservice, TenantId tenant, CorrelationId correlationId, string filePath, int lineNumber, string member) =>
            CurrentFor(application, microservice, tenant, correlationId, Claims.Empty, filePath, lineNumber, member);

        /// <inheritdoc/>
        public ExecutionContext CurrentFor(TenantId tenant, CorrelationId correlationId, Claims claims, string filePath, int lineNumber, string member) =>
            CurrentFor(_application, _microservice, tenant, correlationId, claims, filePath, lineNumber, member);

        /// <inheritdoc/>
        public ExecutionContext CurrentFor(Microservice microservice, TenantId tenant, CorrelationId correlationId, Claims claims, string filePath, int lineNumber, string member) =>
            CurrentFor(_application, microservice, tenant, correlationId, claims, filePath, lineNumber, member);

        /// <inheritdoc/>
        public ExecutionContext CurrentFor(Application application, Microservice microservice, TenantId tenant, CorrelationId correlationId, Claims claims, string filePath, int lineNumber, string member)
        {
            var executionContext = new ExecutionContext(
                application,
                microservice,
                tenant,
                _environment,
                correlationId,
                claims,
                CultureInfo.CurrentCulture);

            return CurrentFor(executionContext, filePath, lineNumber, member);
        }

        /// <inheritdoc/>
        public ExecutionContext CurrentFor(
            ExecutionContext context,
            string filePath,
            int lineNumber,
            string member)
        {
            _logger.Trace($"Setting execution context ({context}) - from: ({filePath}, {lineNumber}, {member}) ", filePath, lineNumber, member);
            Current = context;
            return context;
        }
    }
}
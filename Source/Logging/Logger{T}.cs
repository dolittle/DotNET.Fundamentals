// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Dolittle.Logging
{
    /// <summary>
    /// Represents an implementation of <see cref="ILogger"/>.
    /// </summary>
    /// <typeparam name="T">The type that the log messages relate to.</typeparam>
    internal class Logger<T> : Logger, ILogger<T>
    {
    }
}

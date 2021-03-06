﻿// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Machine.Specifications;

namespace Dolittle.Reflection.Specs.for_TypeExtensions
{
    public class when_asking_a_type_without_a_default_constructor_if_it_has_a_default_constructor
    {
        static bool result;

        Because of = () => result = typeof(TypeWithoutDefaultConstructor).HasDefaultConstructor();

        It should_return_false = () => result.ShouldBeFalse();
    }
}

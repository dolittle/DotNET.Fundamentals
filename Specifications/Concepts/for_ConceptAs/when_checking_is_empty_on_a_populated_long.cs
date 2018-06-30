﻿using Dolittle.Concepts;
using Machine.Specifications;

namespace Dolittle.Specs.Concepts.for_ConceptAs
{
    [Subject(typeof(ConceptAs<>))]
    public class when_checking_is_empty_on_a_populated_long : Dolittle.Specs.Concepts.given.concepts
    {
        static bool is_empty;

        Establish context = () => is_empty = value_as_a_long.IsEmpty();

        It should_not_be_empty = () => is_empty.ShouldBeFalse();
    }
}
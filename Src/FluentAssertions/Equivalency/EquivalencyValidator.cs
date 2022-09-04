using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions.Common;
using FluentAssertions.Execution;

namespace FluentAssertions.Equivalency
{
    /// <summary>
    /// Is responsible for validating the equality of one or more properties of a subject with another object.
    /// </summary>
    public class EquivalencyValidator : IEquivalencyValidator
    {
        #region Private Definitions

        private readonly IEquivalencyAssertionOptions config;

        private readonly Dictionary<Type, bool> isComplexTypeMap = new Dictionary<Type, bool>();

        #endregion

        public EquivalencyValidator(IEquivalencyAssertionOptions config)
        {
            this.config = config;
        }

        public void AssertEquality(EquivalencyValidationContext context)
        {
            using (var scope = new AssertionScope())
            {
                scope.AddReportable("configuration", config.ToString());

                scope.BecauseOf(context.Because, context.BecauseArgs);

                AssertEqualityUsing(context);

                if (context.Tracer != null)
                {
                    scope.AddReportable("trace", context.Tracer.ToString());
                }
            }
        }

        public void AssertEqualityUsing(IEquivalencyValidationContext context)
        {
            var depth = context.SelectedMemberPath.Count(chr => chr == '.');
            var shouldRecurse = config.AllowInfiniteRecursion || depth < 10;

            if (!shouldRecurse)
            {
                AssertionScope.Current.FailWith("The maximum recursion depth was reached.  ");
            }
            else
            {
                if (context.SelectedMemberDescription.Length > 0)
                {
                    AssertionScope.Current.Context = context.SelectedMemberDescription;
                }

                AssertionScope.Current.TrackComparands(context.Subject, context.Expectation);

                var objectTracker = AssertionScope.Current.Get<CyclicReferenceDetector>("cyclic_reference_detector");
                if (objectTracker is null)
                {
                    objectTracker = new CyclicReferenceDetector(config.CyclicReferenceHandling);
                    AssertionScope.Current.AddNonReportable("cyclic_reference_detector", objectTracker);
                }

                bool result;
                if (context.Expectation is null)
                {
                    result = false;
                }
                else
                {
                    var type = context.Expectation.GetType();

                    if (!isComplexTypeMap.TryGetValue(type, out result))
                    {
                        result = !type.OverridesEquals();
                        isComplexTypeMap[type] = result;
                    }
                }

                var reference = new ObjectReference(context.Expectation, context.SelectedMemberPath, result);
                if (!objectTracker.IsCyclicReference(reference, context.Because, context.BecauseArgs))
                {
                    var wasHandled = false;

                    foreach (IEquivalencyStep step in AssertionOptions.EquivalencySteps)
                    {
                        if (step.CanHandle(context, config))
                        {
                            if (step.Handle(context, this, config))
                            {
                                wasHandled = true;
                                break;
                            }
                        }
                    }

                    if (!wasHandled)
                    {
                        Execute.Assertion.FailWith("No IEquivalencyStep was found to handle the context. ");
                    }
                }
            }
        }
    }
}

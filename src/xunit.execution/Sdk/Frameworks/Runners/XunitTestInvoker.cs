﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Xunit.Sdk
{
    /// <summary>
    /// The test invoker for xUnit.net v2 tests.
    /// </summary>
    public class XunitTestInvoker : TestInvoker<XunitTestCase>
    {
        readonly IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes;
        readonly Stack<BeforeAfterTestAttribute> beforeAfterAttributesRun = new Stack<BeforeAfterTestAttribute>();

        /// <summary>
        /// Initializes a new instance of the <see cref="XunitTestInvoker"/> class.
        /// </summary>
        /// <param name="testCase">The test case that this invocation belongs to.</param>
        /// <param name="messageBus">The message bus to report run status to.</param>
        /// <param name="testClass">The test class that the test method belongs to.</param>
        /// <param name="constructorArguments">The arguments to be passed to the test class constructor.</param>
        /// <param name="testMethod">The test method that will be invoked.</param>
        /// <param name="testMethodArguments">The arguments to be passed to the test method.</param>
        /// <param name="beforeAfterAttributes">The list of <see cref="BeforeAfterTestAttribute"/>s for this test invocation.</param>
        /// <param name="displayName">The display name for this test invocation.</param>
        /// <param name="aggregator">The exception aggregator used to run code and collection exceptions.</param>
        /// <param name="cancellationTokenSource">The task cancellation token source, used to cancel the test run.</param>
        public XunitTestInvoker(XunitTestCase testCase,
                                IMessageBus messageBus,
                                Type testClass,
                                object[] constructorArguments,
                                MethodInfo testMethod,
                                object[] testMethodArguments,
                                IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes,
                                string displayName,
                                ExceptionAggregator aggregator,
                                CancellationTokenSource cancellationTokenSource)
            : base(testCase, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, displayName, aggregator, cancellationTokenSource)
        {
            this.beforeAfterAttributes = beforeAfterAttributes;
        }

        /// <inheritdoc/>
        protected override void OnTestExecuting()
        {
            foreach (var beforeAfterAttribute in beforeAfterAttributes)
            {
                var attributeName = beforeAfterAttribute.GetType().Name;
                if (!MessageBus.QueueMessage(new BeforeTestStarting(TestCase, DisplayName, attributeName)))
                    CancellationTokenSource.Cancel();
                else
                {
                    try
                    {
                        Timer.Aggregate(() => beforeAfterAttribute.Before(TestMethod));
                        beforeAfterAttributesRun.Push(beforeAfterAttribute);
                    }
                    catch (Exception ex)
                    {
                        Aggregator.Add(ex);
                        break;
                    }
                    finally
                    {
                        if (!MessageBus.QueueMessage(new BeforeTestFinished(TestCase, DisplayName, attributeName)))
                            CancellationTokenSource.Cancel();
                    }
                }

                if (CancellationTokenSource.IsCancellationRequested)
                    break;
            }
        }

        /// <inheritdoc/>
        protected override void OnTestExecuted()
        {
            foreach (var beforeAfterAttribute in beforeAfterAttributesRun)
            {
                var attributeName = beforeAfterAttribute.GetType().Name;
                if (!MessageBus.QueueMessage(new AfterTestStarting(TestCase, DisplayName, attributeName)))
                    CancellationTokenSource.Cancel();

                Aggregator.Run(() => Timer.Aggregate(() => beforeAfterAttribute.After(TestMethod)));

                if (!MessageBus.QueueMessage(new AfterTestFinished(TestCase, DisplayName, attributeName)))
                    CancellationTokenSource.Cancel();
            }
        }
    }
}
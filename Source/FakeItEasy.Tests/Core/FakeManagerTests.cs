﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using FakeItEasy.ExtensionSyntax;
using FakeItEasy.Core;
using FakeItEasy.Configuration;
using System.Diagnostics;
using FakeItEasy.Expressions;
using FakeItEasy.Tests.TestHelpers;

namespace FakeItEasy.Tests.Core
{
    [TestFixture]
    public class FakeManagerTests
    {
        private static readonly IFakeObjectCallRule NonApplicableInterception = new FakeCallRule { IsApplicableTo = x => false };

        private FakeManager CreateFakeManager<T>()
        {
            var result = A.Fake<T>();

            return Fake.GetFakeManager(result);
        }

        private static void AddFakeRule<T>(T fakedObject, FakeCallRule rule) where T : class
        {
            Fake.GetFakeManager(fakedObject).AddRuleFirst(rule);
        }

        private static void AddFakeRule<T>(T fakedObject, Action<FakeCallRule> ruleConfiguration) where T : class
        {
            var rule = new FakeCallRule();
            ruleConfiguration(rule);

            AddFakeRule(fakedObject, rule);
        }

        private static FakeCallRule CreateApplicableInterception()
        {
            return new FakeCallRule
            {
                IsApplicableTo = x => true
            };
        }

        private TestableProxyResult CreateProxyResult<T>()
        {
            return new TestableProxyResult(typeof(T), (IFakedProxy)A.Fake<T>());
        }

        [Test]
        public void Calls_configured_in_a_child_context_does_not_exist_outside_that_context()
        {
            var fake = this.CreateFakeManager<IFoo>();
            var rule = A.Fake<IFakeObjectCallRule>();

            using (Fake.CreateScope())
            {
                fake.AddRuleFirst(rule);    
            }

            Assert.That(fake.Rules, Has.None.EqualTo(rule));
        }

        [Test]
        public void Event_listeners_that_are_removed_should_not_be_invoked_when_event_is_raised()
        {
            var foo = A.Fake<IFoo>();
            var called = false;
            EventHandler listener = (s, e) => called = true;

            foo.SomethingHappened += listener;
            foo.SomethingHappened -= listener;

            foo.SomethingHappened += Raise.With(EventArgs.Empty).Now;
            
            Assert.That(called, Is.False);
        }

        [Test]
        public void Method_call_should_return_default_value_when_theres_no_matching_interception_and_return_type_is_value_type()
        {
            var fake = this.CreateFakeManager<IFoo>();
            var result = ((IFoo)fake.Object).Baz();

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void Method_call_should_not_set_return_value_when_theres_no_matching_interception_and_return_type_is_void()
        {
            var fake = this.CreateFakeManager<IFoo>();
            ((IFoo)fake.Object).Bar();
        }

        [Test]
        public void The_first_interceptor_should_be_applied_when_it_has_not_been_used()
        {
            var fake = this.CreateFakeManager<IFoo>();

            var interception = new FakeCallRule
            {
                IsApplicableTo = x => true
            };

            fake.AddRuleFirst(interception);

            // Act
            ((IFoo)fake.Object).Bar();

            Assert.That(interception.ApplyWasCalled, Is.True);
        }

        [Test]
        public void The_first_applicable_interceptor_should_be_called_when_it_has_not_been_used()
        {
            var fake = this.CreateFakeManager<IFoo>();

            var interception = new FakeCallRule
            {
                IsApplicableTo = x => true
            };

            fake.AddRuleFirst(NonApplicableInterception);
            fake.AddRuleFirst(interception);

            ((IFoo)fake.Object).Bar();

            Assert.That(interception.ApplyWasCalled, Is.True);
        }

        [Test]
        public void The_latest_added_rule_should_be_called_for_ever_when_no_number_of_times_is_specified()
        {
            var fake = this.CreateFakeManager<IFoo>();

            var firstRule = CreateApplicableInterception();
            var latestRule = CreateApplicableInterception();

            fake.AddRuleFirst(firstRule);
            fake.AddRuleFirst(latestRule);

            var foo = (IFoo)fake.Object;
            foo.Bar();
            foo.Bar();
            foo.Bar();

            Assert.That(firstRule.ApplyWasCalled, Is.False);
        }

        [Test]
        public void An_applicable_action_should_be_called_its_specified_number_of_times_before_the_next_applicable_action_is_called()
        {

            var fake = this.CreateFakeManager<IFoo>();

            var applicableTwice = new FakeCallRule
            {
                IsApplicableTo = x => true,
                NumberOfTimesToCall = 2
            };

            var nextApplicable = CreateApplicableInterception();

            fake.AddRuleFirst(nextApplicable);
            fake.AddRuleFirst(applicableTwice);
            
            ((IFoo)fake.Object).Bar();
            ((IFoo)fake.Object).Bar();
            Assert.That(nextApplicable.ApplyWasCalled, Is.False);

            ((IFoo)fake.Object).Bar();
            Assert.That(nextApplicable.ApplyWasCalled, Is.True);
        }

        [Test]
        public void DefaultValue_should_be_returned_when_the_last_applicable_action_has_been_used_its_specified_number_of_times()
        {
            var fake = this.CreateFakeManager<IFoo>();

            var applicableTwice = CreateApplicableInterception();
            applicableTwice.NumberOfTimesToCall = 2;
            applicableTwice.Apply = x => x.SetReturnValue(10);

            fake.AddRuleFirst(applicableTwice);

            ((IFoo)fake.Object).Baz();
            ((IFoo)fake.Object).Baz();

            var result = ((IFoo)fake.Object).Baz();

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void Interceptions_should_return_interceptions_that_are_added()
        {
            var fake = this.CreateFakeManager<IFoo>();

            var one = CreateApplicableInterception();
            var two = CreateApplicableInterception();

            fake.AddRuleFirst(one);
            fake.AddRuleFirst(two);

            Assert.That(fake.Rules, Is.EquivalentTo(new[] { one, two }));
        }

        [Test]
        public void RecordedCalls_contains_all_calls_made_on_the_fake()
        {
            var fake = this.CreateFakeManager<IFoo>();

            ((IFoo)fake.Object).Bar();
            var i = ((IFoo)fake.Object)[1];

            Assert.That(fake.RecordedCallsInScope, Has.Some.Matches<IFakeObjectCall>(x => x.Method.Name == "Bar"));
            Assert.That(fake.RecordedCallsInScope, Has.Some.Matches<IFakeObjectCall>(x => x.Method.Name == "get_Item"));
        }

        [Test]
        public void RecordedCalls_should_contain_calls_that_throws_exceptions()
        {
            // Arrange
            var fake = A.Fake<IFoo>();
            var manager = Fake.GetFakeManager(fake);
            A.CallTo(() => fake.Bar()).Throws(new Exception());

            // Act
            try
            {
                fake.Bar();
            }
            catch { }

            // Assert
            Assert.That(manager.RecordedCallsInScope.Count(), Is.EqualTo(1));
        }

        [Test]
        public void RecordedCalls_only_returns_calls_made_within_the_scope()
        {
            var foo = A.Fake<IFoo>();

            foo.Baz();

            using (Fake.CreateScope())
            {
                foo.Baz();

                Assert.That(Fake.GetCalls(foo).Count(), Is.EqualTo(1));
            }
        }

        [Test]
        public void RecordedCalls_returns_calls_made_in_scope_and_any_inner_scopes()
        {
            var foo = A.Fake<IFoo>();
            
            foo.Baz();

            using (Fake.CreateScope())
            {
                foo.Baz();

                using (Fake.CreateScope())
                {
                    foo.Baz();
                }
            }

            Assert.That(Fake.GetCalls(foo).Count(), Is.EqualTo(3));
        }

        [Test]
        public void Rules_should_only_be_valid_within_the_current_scope()
        {
            var fake = this.CreateFakeManager<IFoo>();
            var rule = A.Fake<IFakeObjectCallRule>();
            A.CallTo(() => rule.IsApplicableTo(A<IFakeObjectCall>.Ignored.Argument)).Returns(true);

            using (Fake.CreateScope())
            {
                fake.AddRuleFirst(rule);
            }

            (fake.Object as IFoo).Bar();

            A.CallTo(() => rule.Apply(A<IInterceptedFakeObjectCall>.Ignored.Argument)).MustNotHaveHappened();
        }

        [Test]
        public void Object_properties_has_property_behavior_when_not_configured()
        {
            var foo = A.Fake<IFoo>();

            foo.SomeProperty = 10;

            Assert.That(foo.SomeProperty, Is.EqualTo(10));
        }

        [Test]
        public void Object_properties_be_set_directly_and_configured_as_methods_interchangeably()
        {
            var foo = A.Fake<IFoo>();

            A.CallTo(() => foo.SomeProperty).Returns(2);
            Assert.That(foo.SomeProperty, Is.EqualTo(2));

            foo.SomeProperty = 5;
            Assert.That(foo.SomeProperty, Is.EqualTo(5));

            A.CallTo(() => foo.SomeProperty).Returns(20);
            Assert.That(foo.SomeProperty, Is.EqualTo(20));

            foo.SomeProperty = 10;
            Assert.That(foo.SomeProperty, Is.EqualTo(10));
        }

        [Test]
        public void Properties_should_be_set_to_fake_objects_when_property_type_is_fakeable_and_the_property_is_not_explicitly_set()
        {
            var foo = A.Fake<IFoo>();

            Assert.That(foo.ChildFoo, Is.InstanceOf<IFakedProxy>());
        }

        [Test]
        public void Non_configured_property_should_have_same_fake_instance_when_accessed_twice_when_property_is_internal()
        {
            // Arrange
            var foo = A.Fake<Foo>();
            

            // Act
            
            // Assert
            Assert.That(foo.InternalVirtualFakeableProperty, Is.SameAs(foo.InternalVirtualFakeableProperty));
        }

        [Test]
        public void Property_should_return_set_value_when_property_is_internal()
        {
            // Arrange
            var foo = A.Fake<Foo>();
            var value = A.Fake<IFoo>();

            // Act
            foo.InternalVirtualFakeableProperty = value;

            // Assert
            Assert.That(foo.InternalVirtualFakeableProperty, Is.SameAs(value));
        }

        [Test]
        public void GetHashCode_on_faked_object_should_return_hash_code_of_fake_when_not_configured()
        {
            var fake = this.CreateFakeManager<IFoo>();
            var foo = (IFoo)fake.Object;

            Assert.That(foo.GetHashCode(), Is.EqualTo(fake.GetHashCode()));
        }

        [Test]
        public void Equals_on_faked_object_should_return_true_if_the_passed_in_object_is_the_same_and_it_is_not_configured()
        {
            var fake = this.CreateFakeManager<IFoo>();
            var foo = (IFoo)fake.Object;

            Assert.That(foo.Equals(foo));
        }

        [Test]
        public void Equals_on_faked_object_should_return_false_when_passed_in_object_is_not_the_same_and_it_is_not_configured()
        {
            var fake = this.CreateFakeManager<IFoo>();
            var foo = (IFoo)fake.Object;

            Assert.That(foo.Equals("Something else"), Is.False);
        }

        [Test]
        public void ToString_should_return_fake_of_type_when_not_configured()
        {
            var fake = this.CreateFakeManager<IFoo>();
            
            Assert.That(fake.Object.ToString(), Is.EqualTo("Faked FakeItEasy.Tests.IFoo"));
        }

        [Test]
        public void SetProxy_should_set_proxy_from_proxy_result()
        {
            var fake = this.CreateFakeManager<IFoo>();

            var proxy = this.CreateProxyResult<IFoo>();

            fake.SetProxy(proxy);

            Assert.That(fake.Object, Is.SameAs(proxy.Proxy));
        }

        [Test]
        public void SetProxy_should_set_the_fake_type_from_the_proxy()
        {
            var fake = this.CreateFakeManager<IFoo>();

            var proxy = this.CreateProxyResult<IFoo>();

            fake.SetProxy(proxy);

            Assert.That(fake.FakeObjectType, Is.EqualTo(typeof(IFoo)));
        }

        [Test]
        public void SetProxy_should_configure_fake_object_to_intercept_calls()
        {
            var fake = this.CreateFakeManager<IFoo>();
            var proxy = this.CreateProxyResult<IFoo>();
            var call = A.Fake<IWritableFakeObjectCall>();
            call.Configure().CallsTo(x => x.Method).Returns(typeof(IFoo).GetMethod("Bar", new Type[] { }));

            fake.SetProxy(proxy);

            var rule = A.Fake<IFakeObjectCallRule>();
            A.CallTo(() => rule.IsApplicableTo(call)).Returns(true);
            
            fake.AddRuleFirst(rule);

            proxy.RaiseCallWasIntercepted(call);

            A.CallTo(() => rule.Apply(A<IInterceptedFakeObjectCall>.Ignored.Argument)).MustHaveHappened();
        }

        [Test]
        public void RemoveRule_should_remove_the_specified_rule_from_the_fake()
        {
            // Arrange
            var fake = this.CreateFakeManager<IFoo>();
            var rule = ExpressionHelper.CreateRule<IFoo>(x => x.Bar());
            fake.AddRuleFirst(rule);

            // Act
            fake.RemoveRule(rule);

            // Assert
            Assert.That(fake.Rules, Has.None.EqualTo(rule));
        }

        [Test]
        public void RemoveRule_should_do_nothing_when_rule_does_not_exist_in_fake()
        {
            // Arrange
            var fake = this.CreateFakeManager<IFoo>();
            var rule = ExpressionHelper.CreateRule<IFoo>(x => x.Bar());

            // Act
            fake.RemoveRule(rule);

            // Assert
            Assert.That(fake.Rules, Has.None.EqualTo(rule));
        }

        [Test]
        public void RemoveRule_should_be_null_guarded()
        {
            // Arrange
            var fake = this.CreateFakeManager<IFoo>();

            // Act
            
            // Assert
            NullGuardedConstraint.Assert(() =>
                fake.RemoveRule(ExpressionHelper.CreateRule<IFoo>(x => x.Bar())));
        }

        [Test]
        public void AddRuleLast_should_add_rule()
        {
            // Arrange
            var fake = this.CreateFakeManager<IFoo>();
            var rule = A.Fake<IFakeObjectCallRule>();
            
            // Act
            fake.AddRuleLast(rule);

            // Assert
            Assert.That(fake.Rules, Has.Some.SameAs(rule));
        }

        [Test]
        public void AddRuleLast_should_add_rule_last_among_the_user_specified_rules()
        {
            // Arrange
            var fake = this.CreateFakeManager<IFoo>();
            var rule = A.Fake<IFakeObjectCallRule>();
            A.CallTo(() => rule.ToString()).Returns("rule!");
            // Act
            fake.AddRuleFirst(A.Fake<IFakeObjectCallRule>());
            fake.AddRuleLast(rule);

            // Assert
            Assert.That(fake.AllUserRules.Last.Value.Rule, Is.SameAs(rule));
        }

        [Test]
        public void Call_should_not_be_recorded_when_DoNotRecordCall_has_been_called()
        {
            // Arrange
            var fake = A.Fake<IFoo>();
            var rule = A.Fake<IFakeObjectCallRule>();
            A.CallTo(() => rule.IsApplicableTo(A<IFakeObjectCall>.Ignored.Argument)).Returns(true);
            A.CallTo(() => rule.Apply(A<IInterceptedFakeObjectCall>.Ignored.Argument)).Invokes(x => x.Arguments.Get<IInterceptedFakeObjectCall>(0).DoNotRecordCall());
            
            Fake.GetFakeManager(fake).AddRuleFirst(rule);

            // Act
            fake.Bar();

            // Assert
            Assert.That(Fake.GetCalls(fake), Is.Empty);
        }

        public class TypeWithNoDefaultConstructorButAllArgumentsFakeable
        {
            public IFoo Foo;
            public IFormatProvider FormatProvider;

            public TypeWithNoDefaultConstructorButAllArgumentsFakeable(IFoo foo, IFormatProvider formatProvider)
            {
                this.Foo = foo;
                this.FormatProvider = formatProvider;
            }
        }
    }
}

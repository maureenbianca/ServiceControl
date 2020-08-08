﻿namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using TestSupport;
    using TestSupport.EndpointTemplates;

    class When_a_failed_message_is_retried : AcceptanceTest
    {
        [Test]
        public async Task Should_remove_failedmessageretries_when_retrying_groups()
        {
            FailedMessageRetriesCountReponse failedMessageRetries = null;

            await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.When(async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    FailedMessage failedMessage = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    if (failedMessage == null)
                    {
                        return false;
                    }

                    ctx.FailureGroupId = failedMessage.FailureGroups.First().Id;

                    return true;
                }, async (bus, ctx) =>
                {
                    ctx.AboutToSendRetry = true;
                    await this.Post<object>($"/api/recoverability/groups/{ctx.FailureGroupId}/errors/retry");
                }).DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (ctx.Retried)
                    {
                        try
                        {
                            await this.Delete($"/api/recoverability/unacknowledgedgroups/{ctx.FailureGroupId}");
                        }
                        catch
                        {
                            return false;
                        }

                        failedMessageRetries = await this.TryGet<FailedMessageRetriesCountReponse>("/api/failedmessageretries/count");

                        return failedMessageRetries.Count == 0;
                    }

                    return false;
                })
                .Run();

            Assert.AreEqual(failedMessageRetries.Count, 0, "FailedMessageRetries not removed");
        }

        [Test]
        public async Task Should_remove_failedmessageretries_when_retrying_individual_messages()
        {
            FailedMessageRetriesCountReponse failedMessageRetries = null;

            await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.When(async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    FailedMessage failedMessage = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    if (failedMessage == null)
                    {
                        return false;
                    }

                    return true;
                }, async (bus, ctx) =>
                {
                    ctx.AboutToSendRetry = true;
                    await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                }).DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (ctx.Retried)
                    {
                        failedMessageRetries = await this.TryGet<FailedMessageRetriesCountReponse>("/api/failedmessageretries/count");

                        return failedMessageRetries.Count == 0;
                    }

                    return false;
                })
                .Run();

            Assert.AreEqual(failedMessageRetries.Count, 0, "FaileMessageRetries not removed");
        }

        public class FailingEndpoint : EndpointConfigurationBuilder
        {
            public FailingEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableFeature<Outbox>();
                    c.ReportSuccessfulRetriesToServiceControl();

                    var recoverability = c.Recoverability();
                    recoverability.Immediate(s => s.NumberOfRetries(0));
                    recoverability.Delayed(s => s.NumberOfRetries(0));
                });
            }

            class StartFeature : Feature
            {
                public StartFeature()
                {
                    EnableByDefault();
                }

                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.RegisterStartupTask(new SendMessageAtStart());
                }

                class SendMessageAtStart : FeatureStartupTask
                {
                    protected override Task OnStart(IMessageSession session)
                    {
                        return session.SendLocal(new MyMessage());
                    }

                    protected override Task OnStop(IMessageSession session)
                    {
                        return Task.FromResult(0);
                    }
                }
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Message Handled");
                    if (Context.AboutToSendRetry)
                    {
                        Context.Retried = true;
                    }
                    else
                    {
                        Context.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, Settings.EndpointName()).ToString();
                        throw new Exception("Simulated Exception");
                    }

                    return Task.FromResult(0);
                }
            }
        }

        public class Context : ScenarioContext
        {
            public string UniqueMessageId { get; set; }
            public string FailureGroupId { get; set; }
            public bool Retried { get; set; }
            public bool AboutToSendRetry { get; set; }
        }

        public class MyMessage : ICommand
        {
        }
    }
}
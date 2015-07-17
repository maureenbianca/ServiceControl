namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.MessageFailures;

    public class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        public void Handle(ArchiveAllInGroup message)
        {
            var query = Session.Query<FailureGroupMessageView, FailedMessages_ByGroup>()
                .Where(m => m.FailureGroupId == message.GroupId && m.Status == FailedMessageStatus.Unresolved)
                .ProjectFromIndexFieldsInto<FailureGroupMessageView>();

            string groupName = null;
            var messageIds = new List<string>();

            using (var stream = Session.Advanced.Stream(query))
            {
                while (stream.MoveNext())
                {
                    if (stream.Current.Document.Status != FailedMessageStatus.Unresolved)
                        continue;

                    if (groupName == null)
                    {
                        groupName = stream.Current.Document.FailureGroupName;
                    }

                    Session.Advanced.DocumentStore.DatabaseCommands.Patch(
                        stream.Current.Document.Id,
                        new[]
                        {
                            new PatchRequest
                            {
                                Type = PatchCommandType.Set,
                                Name = "Status",
                                Value = (int) FailedMessageStatus.Archived,
                                PrevVal = (int) FailedMessageStatus.Unresolved
                            }
                        });

                    messageIds.Add(stream.Current.Document.MessageId);
                }
            }

            Bus.Publish<FailedMessageGroupArchived>(m =>
            {
                m.GroupId = message.GroupId;
                m.GroupName = groupName;
                m.MessageIds = messageIds.ToArray();
            });
        }

        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }
    }
}
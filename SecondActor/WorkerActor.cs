using Akka.Actor;
using Entities.Dtos;
using Entities.Messages;
using System;

namespace SecondActor
{
    public class WorkerActor:ReceiveActor
    {
        public WorkerActor()
        {
            Console.WriteLine("Second Worker Actor Started");

            Receive<GetUserGroupMessage>(msg => Handle(msg));
        }

        private void Handle(GetUserGroupMessage message)
        {
            var sender = Sender;
            var response = new UserGroup()
            {
                UserGroupId = 1,
                UserGroupName = "User Group 1"
            };
            Sender.Tell(response, sender);
        }
    }
}

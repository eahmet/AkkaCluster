using Akka.Actor;
using Entities.Dtos;
using Entities.Messages;
using System;

namespace FirstActor
{
    public class WorkerActor:ReceiveActor
    {
        private readonly IActorRef _secondActorRef = null;
        public WorkerActor(IActorRef secondActorRef)
        {
            _secondActorRef = secondActorRef;
            Console.WriteLine("First Worker Actor Started");

            Receive<GetUserMessage>(msg => Handle(msg));
        }

        private void Handle(GetUserMessage message)
        {
            var sender = Sender;
            var getUserGroupMessage = new GetUserGroupMessage(1);
            UserGroup userGroup = (UserGroup)(_secondActorRef.Ask(getUserGroupMessage).Result);
            var response = new User()
            {
                Name = "John",
                Surname = "Smith",
                UserId = message.UserId,
                UserGroup = userGroup
            };
            Sender.Tell(response, sender);
        }
    }
}

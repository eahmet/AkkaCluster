using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Akka.Actor;
using Entities.Dtos;
using Entities.Messages;

namespace Actor
{
    public class WorkerActor:ReceiveActor
    {
        public WorkerActor()
        {
            Console.WriteLine("Worker Actor Started");

            Receive<GetUserMessage>(msg => Handle(msg));
        }

        private void Handle(GetUserMessage message)
        {
            Console.WriteLine($"Response From : {this.Self.Path}");
            var sender = Sender;
            var response = new User()
            {
                Name = "John",
                Surname = "Smith",
                UserId = message.UserId
            };
            Sender.Tell(response, sender);
        }
    }
}

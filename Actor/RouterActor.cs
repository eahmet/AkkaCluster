using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using Akka.Routing;
using Entities.Messages;

namespace Actor
{
    public class RouterActor:ReceiveActor
    {
        private readonly IActorRef _workerActor = null;
        public RouterActor()
        {
            var config = FromConfig.Instance;
            _workerActor= Context.ActorOf(Props.Create<WorkerActor>().WithRouter(FromConfig.Instance), "ActorLocal");

            Receive<string>(msg => msg.Equals("shutdown"), msg =>
            {
                _workerActor.Tell(PoisonPill.Instance, Self);
                Become(() =>
                {
                    Receive<object>(obj =>
                    {
                        Sender.Tell("Router Service Unavailable, shutting down", Self);
                    });
                });
            });

            // Terminated message handler for child actors
            Receive<Terminated>(t => t.ActorRef.Equals(_workerActor), msg =>
            {
                Console.WriteLine($"Worker Terminated : {msg.ActorRef.ToString()}");
            });

            // receive actor messages to process
            Receive<BaseMessage>(Handle);
        }
        private void Handle(BaseMessage message)
        {
            _workerActor.Tell(message, Sender);
        }
        protected override void PreStart()
        {
            Console.WriteLine($"Router Starting... Path : {this.Self.Path}");
            base.PreStart();
        }
    }
}

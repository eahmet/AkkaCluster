using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Actor;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.HealthCheck;
using Akka.Remote;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;
using Akka.Cluster.Tools;
using Entities.Messages;
using Entities.Dtos;

namespace Router
{
    class Program
    {
        private static ActorSystem _actorSystem;
        private static IActorRef RouterActor;
        private static AkkaHealthCheck _akkaHealthCheck;
        private static IActorRef _listenerActor;
        private static ManualResetEvent manualResetEvent = new ManualResetEvent(false);
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            StartActorSystem();

            manualResetEvent.WaitOne();
        }

        private static void StartActorSystem()
        {
            Config clusterConfig =ConfigurationFactory.ParseString(@"
                petabridge.cmd{
	                                        host = ""0.0.0.0""
	                                        port = 9111
                                        }
				akka {
					actor {
						provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
					    serializers {
                            akka-pubsub = ""Akka.Cluster.Tools.PublishSubscribe.Serialization.DistributedPubSubMessageSerializer, Akka.Cluster.Tools""
                        }
                        serialization-bindings {
                            ""Akka.Cluster.Tools.PublishSubscribe.IDistributedPubSubMessage, Akka.Cluster.Tools"" = akka-pubsub
                            ""Akka.Cluster.Tools.PublishSubscribe.Internal.SendToOneSubscriber, Akka.Cluster.Tools"" = akka-pubsub
                        }
                        serialization-identifiers {
                            ""Akka.Cluster.Tools.PublishSubscribe.Serialization.DistributedPubSubMessageSerializer, Akka.Cluster.Tools"" = 9
                        }
                        deployment {
			                       /Router/ActorLocal {
				                    router = round-robin-pool
				                    resizer {
					                    enabled = on
					                    lower-bound = 5
					                    upper-bound = 10
					                    pressure-threshold = 1
					                    rampup-rate = 0.2
					                    backoff-threshold = 0.3
					                    backoff-rate = 0.1
					                    messages-per-resize = 5
				                    }
                                }
                        }
                    }
					remote {
						log-remote-lifecycle-events = on
						helios.tcp {
							transport-class = ""Akka.Remote.Transport.Helios.HeliosTcpTransport, Akka.Remote""
                            applied-adapters = []
                            transport-protocol = tcp
                            hostname = ""0.0.0.0""
                            port = 9112
                        }
					}
					cluster {
						seed-nodes = []
                        roles = [seed-node-1]
                    }
				}
            ");

            _actorSystem = ActorSystem.Create("abodur", clusterConfig);

            _akkaHealthCheck = AkkaHealthCheck.For(_actorSystem);

            // create pbm host
            PetabridgeCmd cmd = PetabridgeCmd.Get(_actorSystem);
            cmd.RegisterCommandPalette(ClusterCommands.Instance);
            cmd.RegisterCommandPalette(RemoteCommands.Instance);
            cmd.Start();

            RouterActor = _actorSystem.ActorOf(Props.Create<RouterActor>(),"Router");

            for (int i = 0; i < 100; i++)
            {
                RouterActor.Tell(new GetUserMessage(i));
            }
            
        }

        private static void StopActorSystem()
        {
            CoordinatedShutdown
                .Get(_actorSystem)
                .Run(CoordinatedShutdown.ClrExitReason.Instance)
                .Wait();
        }

        #region [event handlers]

        private static void EventListener_ActorSystemQuarantinedEvent(object sender, EventArgs e)
        {
            // write console log
            Console.WriteLine($"This Actor System quarantined by {e.ToString()} and will try to restart.");

            // unsubscribe from event stream
            _actorSystem.EventStream.Unsubscribe<ThisActorSystemQuarantinedEvent>(_listenerActor);

            // detach custom event of terminator
            
            // stop terminator actor
            _actorSystem.Stop(_listenerActor);
            

            // run coordinated shutdown to terminate actor system
            CoordinatedShutdown
                .Get(_actorSystem)
                .Run(CoordinatedShutdown.ClusterLeavingReason.Instance)
                .ContinueWith(t =>
                {
                    StartActorSystem();
                });
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                // log error
                Console.WriteLine($"Unhandled Exception [IsTerminating]   : {e.IsTerminating}");
                Console.WriteLine($"Unhandled Exception [Message]         : {((Exception)e.ExceptionObject).Message}");
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            StopActorSystem();
        }

        #endregion
    }
}

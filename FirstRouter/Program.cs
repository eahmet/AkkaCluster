using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.HealthCheck;
using Akka.Remote;
using Entities.Messages;
using FirstActor;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;
using System;
using System.Threading;
using Entities.Dtos;
using Akka.Routing;

namespace FirstRouter
{
    class Program
    {
        private static ActorSystem _actorSystem;
        private static IActorRef RouterActor;
        private static IActorRef SecondRouterActor;
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
                    log-config-on-start = on 
					actor {
						provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
                        ask-timeout = 60s
                        debug {
			                        receive = on
			                        autoreceive = on
			                        lifecycle = on
			                        event-stream = on
			                        unhandled = on
		                        }
                        deployment {
                                    /SecondRouter {
				                    router = round-robin-group
				                    routees.paths = [""/user/SecondRouter""]
				                    cluster {
					                    enabled = on
					                    allow-local-routees = off
					                    use-role = SecondRouter
				                        }
			                        }
			                       /FirstRouter/FirstActor {
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
						maximum-payload-bytes = 83886080 bytes
		                dot-netty.tcp {
			                hostname = ""127.0.0.1""
			                port = 9112
                            connection-timeout = 15 s
                            send-buffer-size = 256000b
                            receive-buffer-size = 256000b
			                maximum-frame-size = 83886080b
                            backlog = 4096
		                }
					}
					cluster {
						seed-nodes = [""akka.tcp://myactorsystem@127.0.0.1:4053""]
                        roles = [""FirstRouter""]
                    }
				}
            ");

            _actorSystem = ActorSystem.Create("myactorsystem", clusterConfig);

            _akkaHealthCheck = AkkaHealthCheck.For(_actorSystem);

            // create pbm host
            PetabridgeCmd cmd = PetabridgeCmd.Get(_actorSystem);
            cmd.RegisterCommandPalette(ClusterCommands.Instance);
            cmd.RegisterCommandPalette(RemoteCommands.Instance);
            cmd.Start();

            SecondRouterActor = _actorSystem.ActorOf(Props.Empty.WithRouter(FromConfig.Instance), "SecondRouter");
            RouterActor = _actorSystem.ActorOf(Props.Create<RouterActor>(SecondRouterActor),"FirstRouter");

            //Wait for actor system to be initialize
            Thread.Sleep(6000);
            for (int i = 0; i < 10; i++)
            {
                var result = (User)(RouterActor.Ask(new GetUserMessage(i)).Result);
                Console.WriteLine($"Name: {result.Name} - Surname: {result.Surname} - UserId: {result.UserId} - UserGroupId: {result.UserGroup.UserGroupId} - UserGroupName: {result.UserGroup.UserGroupName}");
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

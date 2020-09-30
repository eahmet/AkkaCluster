using System;
using System.IO;
using System.Linq;
using Akka.Actor;
using Akka.Configuration;
using static System.String;


namespace Lighthouse
{
    public static class LighthouseHostFactory
    {
        public static ActorSystem LaunchLighthouse(string ipAddress = null, int? specifiedPort = null,
            string systemName = null)
        {
            systemName = systemName ?? Environment.GetEnvironmentVariable("ACTORSYSTEM")?.Trim();


            var clusterConfig = ConfigurationFactory.ParseString(@"lighthouse{
                                          actorsystem: ""abodur"" 
                                        }
                                        petabridge.cmd{
	                                        host = ""0.0.0.0""
	                                        port = 9110
                                        }

                                        akka {
                                          actor {
                                            provider = cluster
                                          }
                                          
                                          remote {
                                            log-remote-lifecycle-events = DEBUG
                                            dot-netty.tcp {
                                              transport-class = ""Akka.Remote.Transport.DotNetty.TcpTransport, Akka.Remote""
                                              applied-adapters = []
                                              transport-protocol = tcp
                                              #will be populated with a dynamic host-name at runtime if left uncommented
                                              public-hostname = ""127.0.0.1""
                                              hostname = ""0.0.0.0""
                                              port = 4053
                                            }
                                          }            

                                          cluster {
                                            seed-nodes = [] 
                                            roles = [lighthouse]
                                          }
                                        }");

            var lighthouseConfig = clusterConfig.GetConfig("lighthouse");
            if (lighthouseConfig != null && IsNullOrEmpty(systemName))
                systemName = lighthouseConfig.GetString("actorsystem", systemName);

            ipAddress = clusterConfig.GetString("akka.remote.dot-netty.tcp.public-hostname", "127.0.0.1");
            var port = clusterConfig.GetInt("akka.remote.dot-netty.tcp.port");

            var sslEnabled = clusterConfig.GetBoolean("akka.remote.dot-netty.tcp.enable-ssl");
            var selfAddress = sslEnabled ? new Address("akka.ssl.tcp", systemName, ipAddress.Trim(), port).ToString()
                    : new Address("akka.tcp", systemName, ipAddress.Trim(), port).ToString();

            Console.WriteLine($"[Lighthouse] ActorSystem: {systemName}; IP: {ipAddress}; PORT: {port}");
            Console.WriteLine("[Lighthouse] Performing pre-boot sanity check. Should be able to parse address [{0}]", selfAddress);
            Console.WriteLine("[Lighthouse] Parse successful.");


            var seeds = clusterConfig.GetStringList("akka.cluster.seed-nodes").ToList();

            Config injectedClusterConfigString = null;


            if (!seeds.Contains(selfAddress))
            {
                seeds.Add(selfAddress);

                if (seeds.Count > 1)
                {
                    injectedClusterConfigString = seeds.Aggregate("akka.cluster.seed-nodes = [",
                        (current, seed) => current + @"""" + seed + @""", ");
                    injectedClusterConfigString += "]";
                }
                else
                {
                    injectedClusterConfigString = "akka.cluster.seed-nodes = [\"" + selfAddress + "\"]";
                }
            }


            var finalConfig = injectedClusterConfigString != null
                ? injectedClusterConfigString
                    .WithFallback(clusterConfig)
                : clusterConfig;

            return ActorSystem.Create(systemName, finalConfig);
        }
    }
}

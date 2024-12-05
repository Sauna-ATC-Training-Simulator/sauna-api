using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Threading.Tasks;

namespace SaunaSim.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            CreateHostBuilder(args).Build().Run();
            Console.WriteLine("Shutting Down from Main()");
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder.UseStartup<Startup>();

                    // Determine port to use
                    ushort port = 5052;
                    if (CheckForUserDefinedPort(args, out ushort customPort))
                    {
                        port = customPort;
                    }
                    
                    if (!CheckPortAvailability(port))
                    {
                        throw new InvalidOperationException($"Could not start Sauna API: Port {port} was occupied!");
                    }
                    webBuilder.UseUrls($"http://localhost:{port}");
                });

        private static bool CheckPortAvailability(int port)
        {
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

            IEnumerable<int> allActivePorts = tcpConnInfoArray.Select(endpoint => endpoint.Port).ToList();

            return !allActivePorts.Contains(port);
        }


        static bool CheckForUserDefinedPort(string[] args, out ushort port)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].ToLower() == "-p")
                {
                    if (ushort.TryParse(args[i + 1], out ushort p))
                    {
                        port = p;
                        return true;
                    }
                }
            }
            port = 0;
            return false;
        }
    }
}

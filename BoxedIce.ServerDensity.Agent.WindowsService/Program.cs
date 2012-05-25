using System;
using System.ServiceProcess;
using System.Diagnostics;

namespace BoxedIce.ServerDensity.Agent.WindowsService
{

    static class Program
    {

        static void Main(string[] args)
        {

            // if comman line param, then run as command line
            if (args.Length > 0)
            {

                AgentService agentService = null;
                string commandLine = args[0].ToLower();

                if (commandLine != null && commandLine == "console")
                {

                    // run as a console application
                    agentService = new AgentService();
                    agentService.ConsoleStart();

                    // throw some feedback in the console
                    Console.WriteLine(string.Empty);
                    Console.WriteLine("".PadRight(78, '='));
                    Console.WriteLine("Server Density Windows Agent Console Mode");
                    Console.WriteLine("   ...smash your keyboard to stop!");
                    Console.WriteLine("".PadRight(78, '='));
                    Console.ReadKey(true);

                    // wait for key to be pressed above
                    agentService.ConsoleStop();

                }

            }

            // else run as service
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] { new AgentService() };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DMM.Core.IO;

namespace DmmDep
{
#nullable enable

    // Minimal modular host for incremental refactor of the standalone tool.
    // Keeps original Program.cs intact; this file is an alternate entrypoint
    // you can wire in the csproj for testing (or run manually).
    internal static class ProgramModular
    {
        internal static async Task<int> Main(string[] args)
        {
            using IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // register core services
                    services.AddFileSystem(); // registers IFileSystem -> DefaultFileSystem
                    // TODO: register other modular services here (parsers, loggers, analyzers)
                })
                .Build();

            try
            {
                // resolve modular services through DI
                var fs = host.Services.GetRequiredService<IFileSystem>();

                // Example usage: quickly validate args and show a short message.
                if (args.Length == 0)
                {
                    Console.WriteLine("ProgramModular: no args provided. This is the modular entrypoint.");
                    return 1;
                }

                // TODO: replace the following with modularized logic extracted from Program.cs
                Console.WriteLine("ProgramModular: IFileSystem registered successfully.");
                Console.WriteLine($"Sample check: Working directory exists: {fs.DirectoryExists(Environment.CurrentDirectory)}");

                // Keep host running briefly if you need hosted services; otherwise exit.
                await host.StopAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unhandled error in ProgramModular: {ex}");
                return 1;
            }
        }
    }
}
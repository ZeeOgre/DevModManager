using System;
using Microsoft.Extensions.DependencyInjection;

namespace DMM.Core.IO
{
    public static class FileSystemServiceCollectionExtensions
    {
        /// <summary>
        /// Register the project's default IFileSystem implementation.
        /// Use this from startup code: services.AddFileSystem();
        /// </summary>
        public static IServiceCollection AddFileSystem(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            services.AddSingleton<IFileSystem, DefaultFileSystem>();
            return services;
        }
    }
}
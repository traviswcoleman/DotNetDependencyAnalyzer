using System.Reflection;

using DotNetDependencyAnalyzer.Analyzer;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog;

namespace DotNetDependencyAnalyzer
{
	internal static class DIConfig
	{
		private static IServiceProvider ConfigureServices(IConfiguration configuration)
		{
			var services = new ServiceCollection();

			services.AddSingleton(configuration);
			services.AddLogging();

			services.AddDependencyAnalyzer();

			//Handle DI here

			var serviceProvider = services.BuildServiceProvider();
			ConfigureLogging(serviceProvider, configuration);
			return serviceProvider;
		}

		internal static IServiceProvider Configure()
		{
			var configurationBuilder = new ConfigurationBuilder();
			configurationBuilder
				.SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
				.AddJsonFile("appsettings.json", false, true)
#if DEBUG
				.AddJsonFile("appsettings.development.json", true, true);
#else
; //End the first appsettings method when in release
#endif
			return ConfigureServices(configurationBuilder.Build());
		}

		private static void ConfigureLogging(IServiceProvider serviceProvider, IConfiguration configuration)
		{
			var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
			loggerFactory.AddSerilog();

			Log.Logger = new LoggerConfiguration()
				.ReadFrom.Configuration(configuration)
				.CreateLogger();
		}
	}
}

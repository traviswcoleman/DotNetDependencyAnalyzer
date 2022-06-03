using DotNetDependencyAnalyzer.Analyzer;
using DotNetDependencyAnalyzer.Analyzer.Models;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetDependencyAnalyzer
{
	internal class Program
	{
		static void Main(string[] args)
		{
			IServiceProvider serviceProvider = DIConfig.Configure();

			var analyzerService = serviceProvider.GetService<IAnalyzerService>();

			if (analyzerService == null)
			{
				Console.Error.WriteLine("Couldn't create analyzer service");
				return;
			}

			var options = ParseArgs(args);

			var results = analyzerService.AnalyzeProject(options);

			WriteResults(results);
		}

		private static void WriteResults(AnalyzerResult results)
		{
			Console.WriteLine("Finished");
		}

		private static AnalyzerOptions ParseArgs(string[] args)
		{
			return new AnalyzerOptions();
		}
	}
}
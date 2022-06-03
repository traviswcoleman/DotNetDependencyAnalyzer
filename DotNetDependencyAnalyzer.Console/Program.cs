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
			StringWriter SW = new StringWriter();

			SW.WriteLine(results.RootProject);
			for(int i = 0; i < results.Projects.Count; i++)
				PrintProject(SW, results.Projects[i], i == results.Projects.Count -1);

			Console.WriteLine(SW.ToString());
		}

		private static void PrintProject(StringWriter writer, Project project, bool last)
		{
			writer.Write(last ? "└" : "├");
			writer.WriteLine($"─{project.Name}");
			for (int i = 0; i < project.TargetFrameworks.Count; i++)
				PrintFramework(writer, project.TargetFrameworks[i], last ? "  " : "│ ", i == project.TargetFrameworks.Count - 1);
		}

		private static void PrintFramework(StringWriter writer, Framework framework, string prefix, bool last)
		{
			writer.Write(prefix);
			writer.Write(last ? "└" : "├");
			writer.WriteLine($"─{framework.Name}");
			for (int i = 0; i < framework.Dependencies.Count; i++)
				PrintDependency(writer, framework.Dependencies[i], prefix + (last ? "  " : "│ "), i == framework.Dependencies.Count - 1);
		}

		private static void PrintDependency(StringWriter writer, Dependency dependency, string prefix, bool last)
		{
			writer.Write(prefix);
			writer.Write(last ? "└" : "├");
			writer.WriteLine($"─{dependency.Name} {dependency.Version}");
			for (int i = 0; i < dependency.ChildDependencies.Count; i++)
				PrintDependency(writer, dependency.ChildDependencies[i], prefix + (last ? "  " : "│ "), i == dependency.ChildDependencies.Count - 1);
		}

		private static AnalyzerOptions ParseArgs(string[] args)
		{
			return new AnalyzerOptions(args[0]);
		}
	}
}
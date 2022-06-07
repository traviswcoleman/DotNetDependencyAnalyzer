using CommandLine;

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
			AnalyzerOptions options;

			try
			{
				options = ParseArgs(args);
			}
			catch (InvalidDataException)
			{
				return;
			}

			IServiceProvider serviceProvider = DIConfig.Configure();

			var analyzerService = serviceProvider.GetService<IAnalyzerService>();

			if (analyzerService == null)
			{
				Console.Error.WriteLine("Couldn't create analyzer service");
				return;
			}


			var results = analyzerService.AnalyzeProject(options);

			WriteResults(results);
		}

		private static void WriteResults(AnalyzerResult results)
		{
			if (results.AllDependencies != null && results.AllDependencies.Any())
			{
				Console.ForegroundColor = ConsoleColor.Cyan;
				Console.WriteLine("All dependencies:\n");
				Console.ResetColor();
				foreach (var dependency in results.AllDependencies.AsEnumerable().OrderBy(s => s))
					Console.WriteLine(dependency);
				Console.WriteLine();
			}

			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("Graph:\n");
			Console.ResetColor();
			StringWriter SW = new();

			SW.WriteLine(results.RootPath);
			if (results.Projects != null)
			{
				for (int i = 0; i < results.Projects.Count; i++)
					PrintProject(SW, results.Projects[i], i == results.Projects.Count - 1);
			}

			Console.WriteLine(SW.ToString());
			Console.WriteLine();
		}

		private static void PrintProject(StringWriter writer, Project project, bool last)
		{
			writer.Write(last ? "└" : "├");
			writer.WriteLine($"─{project.Name}");
			if (project.LibraryList != null)
			{
				var list = project.LibraryList.ToList();
				for (int i = 0; i < list.Count; i++)
				{
					writer.Write(last ? "  " : "│ ");
					writer.Write("│ ");
					writer.Write(i == project.LibraryList.Count - 1 ? "└" : "├");
					writer.WriteLine($"─{list[i]}");
				}
			}
			if (project.TargetFrameworks != null)
				for (int i = 0; i < project.TargetFrameworks.Count; i++)
					PrintFramework(writer, project.TargetFrameworks[i], last ? "  " : "│ ", i == project.TargetFrameworks.Count - 1);
		}

		private static void PrintFramework(StringWriter writer, Framework framework, string prefix, bool last)
		{
			writer.Write(prefix);
			writer.Write(last ? "└" : "├");
			writer.WriteLine($"─{framework.Name}");
			if (framework.Dependencies != null)
				for (int i = 0; i < framework.Dependencies.Count; i++)
					PrintDependency(writer, framework.Dependencies[i], prefix + (last ? "  " : "│ "), i == framework.Dependencies.Count - 1);
		}

		private static void PrintDependency(StringWriter writer, Dependency dependency, string prefix, bool last)
		{
			writer.Write(prefix);
			writer.Write(last ? "└" : "├");
			writer.WriteLine($"─{dependency.Name} {dependency.Version}");
			if (dependency.ChildDependencies != null)
				for (int i = 0; i < dependency.ChildDependencies.Count; i++)
					PrintDependency(writer, dependency.ChildDependencies[i], prefix + (last ? "  " : "│ "), i == dependency.ChildDependencies.Count - 1);
		}

		private static AnalyzerOptions ParseArgs(string[] args)
		{
			var results = Parser.Default.ParseArguments<CommandLineParameters>(args);

			if (results.Tag == ParserResultType.NotParsed)
				throw new InvalidDataException();

			if (!File.Exists(results.Value.FilePath))
			{
				Console.Error.WriteLine($"File {results.Value.FilePath} does not exist!");
				throw new InvalidDataException();
			}

			return new AnalyzerOptions(results.Value.FilePath, results.Value.OutputPath, results.Value.SearchString, results.Value.PathToTemp);
		}
	}
}
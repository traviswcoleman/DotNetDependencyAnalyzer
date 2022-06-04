﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using DotNetDependencyAnalyzer.Analyzer.Models;

using NuGet.Common;
using NuGet.ProjectModel;

namespace DotNetDependencyAnalyzer.Analyzer
{
	public class AnalyzerService : IAnalyzerService
	{
		private HashSet<string> distinctDependencies = new();

		public AnalyzerResult AnalyzeProject(AnalyzerOptions options)
		{
			if (!DIConfig.Configured)
				throw new InvalidOperationException("Service must be configured before calling");

			string outputPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

			var status = RunDotNet(options.PathToFile, outputPath, DotNetCommand.msbuild);

			DependencyGraphSpec? graph;
			if (status.IsSuccess)
			{
				graph = DependencyGraphSpec.Load(outputPath);
			}
			else
			{
				throw new InvalidOperationException($"Unable to process {options.PathToFile}.\n\nHere is the build output: {status.StdOutputText}\n\nHere is the error output: {status.ErrorStreamText}");
			}

			var result = ProcessGraph(graph, options);

			if (!String.IsNullOrWhiteSpace(options.OutputPath))
				WriteGraph(result, options.OutputPath);

			return result;
		}

		private static void WriteGraph(AnalyzerResult graph, string outputPath)
		{
			using StreamWriter writer = new(outputPath, false, Encoding.UTF8);
			writer.WriteLine(JsonSerializer.Serialize(graph, new JsonSerializerOptions
			{
				DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
				WriteIndented = true,
			}));
			writer.Flush();
		}

		private AnalyzerResult ProcessGraph(DependencyGraphSpec graph, AnalyzerOptions options)
		{
			var result = new AnalyzerResult(Path.GetFileName(options.PathToFile));

			foreach (var project in graph.Projects.Where(p => p.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference))
			{
				ProcessGraphProject(options, result, project);
			}

			if (!result.Projects.Any())
				result.Projects = null;

			if (distinctDependencies.Any())
				result.AllDependencies = distinctDependencies;

			return result;
		}

		private void ProcessGraphProject(AnalyzerOptions options, AnalyzerResult result, PackageSpec project)
		{
			Project outputProject = new(Path.GetFileName(project.FilePath));

			var lockFile = GetLockFile(project.FilePath, project.RestoreMetadata.OutputPath);

			foreach (var targetFramework in project.TargetFrameworks)
			{
				ProcessProjectFramework(options, outputProject, lockFile, targetFramework);
			}

			if (!outputProject.TargetFrameworks.Any())
				outputProject.TargetFrameworks = null;

			if (!String.IsNullOrWhiteSpace(options.SearchString) &&
				outputProject.TargetFrameworks == null &&
				!outputProject.Name.Contains(options.SearchString, StringComparison.InvariantCultureIgnoreCase))
			{
				return;
			}
			else
			{
				result.Projects!.Add(outputProject);
				distinctDependencies.Add(outputProject.Name);
			}
		}

		private void ProcessProjectFramework(AnalyzerOptions options, Project outputProject, LockFile lockFile, TargetFrameworkInformation targetFramework)
		{
			var lockFileTargetFramework = lockFile.Targets.FirstOrDefault(t => t.TargetFramework.Equals(targetFramework.FrameworkName));


			if (lockFileTargetFramework == null) return;

			Framework outputFramework = new(lockFileTargetFramework.Name);

			foreach (var dependency in targetFramework.Dependencies)
			{
				ProcessFrameworkDependency(options, lockFileTargetFramework, outputFramework, dependency);
			}
			if (!outputFramework.Dependencies.Any())
				outputFramework.Dependencies = null;
			if (!String.IsNullOrWhiteSpace(options.SearchString) &&
				outputFramework.Dependencies == null &&
				!outputFramework.Name.Contains(options.SearchString, StringComparison.InvariantCultureIgnoreCase))
			{
				return;
			}
			else
			{
				outputProject.TargetFrameworks!.Add(outputFramework);
				distinctDependencies.Add(outputFramework.Name);
			}
		}

		private void ProcessFrameworkDependency(AnalyzerOptions options, LockFileTarget lockFileTargetFramework, Framework outputFramework, NuGet.LibraryModel.LibraryDependency dependency)
		{
			var library = lockFileTargetFramework.Libraries.FirstOrDefault(library => library.Name == dependency.Name);
			Dependency outputDependency = new(library.Name, library.Version.Version);

			ProcessChildDependencies(options.SearchString, lockFileTargetFramework, library, outputDependency);
			if (!outputDependency.ChildDependencies.Any())
				outputDependency.ChildDependencies = null;
			if (!String.IsNullOrWhiteSpace(options.SearchString) &&
				outputDependency.ChildDependencies == null &&
				!outputDependency.Name.Contains(options.SearchString, StringComparison.InvariantCultureIgnoreCase))
			{
				return;
			}
			else
			{
				outputFramework.Dependencies!.Add(outputDependency);
				distinctDependencies.Add($"{outputDependency.Name} {outputDependency.Version}");
			}
		}

		private void ProcessChildDependencies(string? SearchString, LockFileTarget lockFileTargetFramework, LockFileTargetLibrary library, Dependency outputDependency)
		{
			if (library?.Dependencies?.Any() ?? false)
			{
				foreach (var dependency in library.Dependencies)
				{
					var childLibrary = lockFileTargetFramework.Libraries.FirstOrDefault(library => library.Name == dependency.Id);
					var childDependency = new Dependency(childLibrary.Name, childLibrary.Version.Version);
					ProcessChildDependencies(SearchString, lockFileTargetFramework, childLibrary, childDependency);
					if (!childDependency.ChildDependencies.Any())
						childDependency.ChildDependencies = null;
					if (!String.IsNullOrWhiteSpace(SearchString) &&
						childDependency.ChildDependencies == null &&
						!childDependency.Name.Contains(SearchString, StringComparison.InvariantCultureIgnoreCase))
					{
						continue; //Does not have any children, does not match search string, don't include in the graph
					}
					else //No search, or has included children, or matches the search string
					{
						outputDependency.ChildDependencies!.Add(childDependency);
						distinctDependencies.Add($"{childDependency.Name} {childDependency.Version}");
					}
				}
			}
		}

		private static LockFile GetLockFile(string filePath, string outputPath)
		{
			var status = RunDotNet(filePath, outputPath, DotNetCommand.restore);

			if (!status.IsSuccess)
				throw new InvalidOperationException($"Unable to restore {filePath}.\n\nError text: {status.ErrorStreamText}\n\nOutput text: {status.StdOutputText}");

			string lockFilePath = Path.Combine(outputPath, "project.assets.json");
			return LockFileUtilities.GetLockFile(lockFilePath, NullLogger.Instance);
		}

		private static DotnetStatus RunDotNet(string pathToFile, string outputFile, DotNetCommand command)
		{
			var startInfo = new ProcessStartInfo("dotnet")
			{
				WorkingDirectory = Path.GetDirectoryName(pathToFile),
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			startInfo.ArgumentList.Add(command.ToString());
			startInfo.ArgumentList.Add($"\"{pathToFile}\"");

			if (command == DotNetCommand.msbuild)
			{
				startInfo.ArgumentList.Add("/t:GenerateRestoreGraphFile");
				startInfo.ArgumentList.Add($"/p:RestoreGraphOutputPath=\"{outputFile}\"");
			}

			var proc = new Process()
			{
				StartInfo = startInfo
			};

			try
			{
				proc.Start();

				var oStrm = new StringBuilder();
				var eStrm = new StringBuilder();

				var oStrmTask = HandleTextStream(proc.StandardOutput, oStrm);
				var eStrmTask = HandleTextStream(proc.StandardError, eStrm);

				proc.WaitForExit();

				return new DotnetStatus(oStrm.ToString(), eStrm.ToString(), proc.ExitCode);
			}
			finally
			{
				proc.Dispose();
			}
		}

		private static async Task HandleTextStream(StreamReader reader, StringBuilder stringBuilder)
		{
			await Task.Yield();

			string line;
			while ((line = await reader.ReadLineAsync()) != null)
				stringBuilder.AppendLine(line);
		}
	}
}

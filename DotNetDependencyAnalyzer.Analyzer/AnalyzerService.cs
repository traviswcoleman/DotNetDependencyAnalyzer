using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DotNetDependencyAnalyzer.Analyzer.Models;

using Microsoft.Extensions.Logging;

using NuGet.Common;
using NuGet.ProjectModel;

namespace DotNetDependencyAnalyzer.Analyzer
{
	public class AnalyzerService : IAnalyzerService
	{
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

			return ProcessGraph(graph, options);
		}

		private AnalyzerResult ProcessGraph(DependencyGraphSpec graph, AnalyzerOptions options)
		{
			var result = new AnalyzerResult(Path.GetFileName(options.PathToFile));

			foreach (var project in graph.Projects.Where(p => p.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference))
			{
				Project outputProject = new(Path.GetFileName(project.FilePath));

				var lockFile = GetLockFile(project.FilePath, project.RestoreMetadata.OutputPath);

				foreach(var targetFramework in project.TargetFrameworks)
				{
					var lockFileTargetFramework = lockFile.Targets.FirstOrDefault(t => t.TargetFramework.Equals(targetFramework.FrameworkName));


					if (lockFileTargetFramework == null) continue;

					Framework outputFramework = new(lockFileTargetFramework.Name);

					foreach(var dependency in targetFramework.Dependencies)
					{
						var library = lockFileTargetFramework.Libraries.FirstOrDefault(library => library.Name == dependency.Name);
						Dependency outputDependency = new(library.Name, library.Version.Version);

						GetChildDependencies(lockFileTargetFramework, library, outputDependency);

						outputFramework.Dependencies.Add(outputDependency);
					}

					outputProject.TargetFrameworks.Add(outputFramework);
				}

				result.Projects.Add(outputProject);
			}

			return result;
		}

		private static void GetChildDependencies(LockFileTarget lockFileTargetFramework, LockFileTargetLibrary library, Dependency outputDependency)
		{
			if (library?.Dependencies?.Any() ?? false)
			{
				foreach (var dependency in library.Dependencies)
				{
					var childLibrary = lockFileTargetFramework.Libraries.FirstOrDefault(library => library.Name == dependency.Id);
					var childDependency = new Dependency(childLibrary.Name, childLibrary.Version.Version);
					GetChildDependencies(lockFileTargetFramework, childLibrary, childDependency);
					outputDependency.ChildDependencies.Add(childDependency);
				}
			}
		}

		private LockFile GetLockFile(string filePath, string outputPath)
		{
			var status = RunDotNet(filePath, outputPath, DotNetCommand.restore);

			if (!status.IsSuccess)
				throw new InvalidOperationException($"Unable to restore {filePath}.\n\nError text: {status.ErrorStreamText}\n\nOutput text: {status.StdOutputText}");

			string lockFilePath = Path.Combine(outputPath, "project.assets.json");
			return LockFileUtilities.GetLockFile(lockFilePath, NullLogger.Instance);
		}

		private DotnetStatus RunDotNet(string pathToFile, string outputFile, DotNetCommand command)
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

		private async Task HandleTextStream(StreamReader reader, StringBuilder stringBuilder)
		{
			await Task.Yield();

			string line;
			while ((line = await reader.ReadLineAsync()) != null)
				stringBuilder.AppendLine(line);
		}
	}
}

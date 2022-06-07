using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using DotNetDependencyAnalyzer.Analyzer.Models;
using NugetPc = NuGet;
using NuGet.ProjectModel;
using NugetPr = NuGet.Common;
using System.Runtime.Versioning;
using System.Xml;

namespace DotNetDependencyAnalyzer.Analyzer
{
	public class AnalyzerService : IAnalyzerService
	{
		private readonly HashSet<string> _distinctDependencies = new();

		public AnalyzerResult AnalyzeProject(AnalyzerOptions options)
		{
			if (!DIConfig.Configured)
				throw new InvalidOperationException("Service must be configured before calling");


			string outputPath = options.PathToTemp ?? Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

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
			foreach (var project in graph.Projects)
			{
				Project outputProject = new(Path.GetFileName(project.FilePath))
				{
					Name = $"{project.Name} (Style: {project.RestoreMetadata.ProjectStyle})"
				};
				switch (project.RestoreMetadata.ProjectStyle)
				{
					case ProjectStyle.PackageReference:
						ProcessPackageReferenceProject(options, project, outputProject);
						break;
					case ProjectStyle.PackagesConfig:
						ProcessPackageConfigProject(options, project, outputProject);
						break;
					case ProjectStyle.ProjectJson:
						ProcessProjectJsonProject(options, project, outputProject);
						break;
					default:
						continue;
				}
				if (outputProject.LibraryList.Any())
					result.Projects.Add(outputProject);
			}

			if (_distinctDependencies.Any())
				result.AllDependencies = _distinctDependencies;

			return result;
		}

		private void ProcessProjectJsonProject(AnalyzerOptions options, PackageSpec project, Project outputProject) => throw new NotImplementedException();

		private void ProcessPackageConfigProject(AnalyzerOptions options, PackageSpec project, Project outputProject)
		{
			if (project.RestoreMetadata is not PackagesConfigProjectRestoreMetadata metadata)
				return;

			var packageConfigPath = metadata.PackagesConfigPath;
			var nugetConfigPath = project.RestoreMetadata.ConfigFilePaths.FirstOrDefault();
			string packagesPath = GetPackagesPath(nugetConfigPath);

			if (!String.IsNullOrWhiteSpace(packagesPath))
			{
				packagesPath = Path.Combine(Path.GetDirectoryName(nugetConfigPath), packagesPath);
			}
			else
				return;
						
			var localRepo = new NugetPc.LocalPackageRepository(packagesPath);
			var packages = new NugetPc.PackageReferenceFile(packageConfigPath).GetPackageReferences();

			var frameworks = packages.Select(p => p.TargetFramework.FullName).Distinct().Select(name => new Framework(name)).ToList();

			foreach (var package in packages)
			{
				var framework = frameworks.Where(f => f.Name == package.TargetFramework.FullName).FirstOrDefault();
				if (framework == null)
					continue;

				var nugetPackage = localRepo.FindPackage(package.Id, package.Version);
				if (nugetPackage != null)
				{
					var outputDependency = new Dependency(package.Id, package.Version.Version);
					ProcessNugetPackage(nugetPackage, package.TargetFramework, localRepo, outputDependency, options.SearchString, outputProject);

					if (!String.IsNullOrWhiteSpace(options.SearchString) &&
						outputDependency.ChildDependencies == null &&
						!outputDependency.Name.Contains(options.SearchString, StringComparison.InvariantCultureIgnoreCase))
						continue;
					else
					{
						framework.Dependencies!.Add(outputDependency);
						outputProject.LibraryList!.Add($"{outputDependency.Name} {outputDependency.Version}");
						_distinctDependencies.Add($"{outputDependency.Name} {outputDependency.Version}");
					}
				}
			}

			foreach (var framework in frameworks)
			{
				if (!framework.Dependencies.Any())
					framework.Dependencies = null;

				if (!String.IsNullOrWhiteSpace(options.SearchString) &&
					framework.Dependencies == null &&
					!framework.Name.Contains(options.SearchString, StringComparison.InvariantCultureIgnoreCase))
					continue;
				else
					outputProject.TargetFrameworks!.Add(framework);
			}

			if(!outputProject.TargetFrameworks.Any())
				outputProject.TargetFrameworks = null;
		}

		private string GetPackagesPath(string? packagesConfigPath)
		{
			if (String.IsNullOrWhiteSpace(packagesConfigPath) || ! File.Exists(packagesConfigPath))
				return String.Empty;

			XmlDocument doc = new();
			doc.Load(packagesConfigPath);

			var node = doc["configuration"]["config"]["add"];
			if (node.Attributes["key"].Value == "repositoryPath")
			{
				return node.Attributes["value"].Value;
			}

			return String.Empty;
		}

		private void ProcessNugetPackage(NugetPc.IPackage? nugetPackage, FrameworkName targetFramework, NugetPc.LocalPackageRepository repository, Dependency outputDependency, string? SearchString, Project outputProject)
		{
			if(nugetPackage == null)
			{
				outputDependency.ChildDependencies = null;
				return;
			}

			var dependencySet = nugetPackage.DependencySets.Where(ds => ds.TargetFramework == null || ds.TargetFramework == targetFramework).FirstOrDefault();
			if (dependencySet == null)
			{
				outputDependency.ChildDependencies = null;
				return;
			}

			foreach (var dependency in dependencySet.Dependencies)
			{
				var childDependency = new Dependency(dependency.Id, dependency.VersionSpec.MinVersion.Version);
				var childNugetPackage = repository.FindPackage(dependency.Id, dependency.VersionSpec.MinVersion);
					ProcessNugetPackage(childNugetPackage, targetFramework, repository, childDependency, SearchString, outputProject);
				if (
					!String.IsNullOrWhiteSpace(SearchString) &&
					childDependency.ChildDependencies == null &&
					!childDependency.Name.Contains(SearchString, StringComparison.InvariantCultureIgnoreCase))
				{
					continue;
				}
				else
				{
					outputDependency.ChildDependencies!.Add(childDependency);
					outputProject.LibraryList!.Add($"{childDependency.Name} {childDependency.Version}");
					_distinctDependencies.Add($"{childDependency.Name} {childDependency.Version}");
				}
			}

			if (!outputDependency.ChildDependencies.Any())
				outputDependency.ChildDependencies = null;
		}

		private void ProcessPackageReferenceProject(AnalyzerOptions options, PackageSpec project, Project outputProject)
		{
			var lockFile = GetLockFile(project.FilePath, project.RestoreMetadata.OutputPath);

			if (String.IsNullOrWhiteSpace(options.SearchString))
				outputProject.LibraryList!.AddRange(lockFile.Libraries.Select(l => $"{l.Name} {l.Version.Version}"));
			else
				outputProject.LibraryList!.AddRange(lockFile.Libraries.Select(l => $"{l.Name} {l.Version.Version}").Where(s=>s.Contains(options.SearchString, StringComparison.InvariantCultureIgnoreCase)));

			_distinctDependencies.AddRange(outputProject.LibraryList!);

			foreach (var targetFramework in project.TargetFrameworks)
			{
				ProcessProjectFramework(options, outputProject, lockFile, targetFramework);
			}

			if (!outputProject.TargetFrameworks.Any())
				outputProject.TargetFrameworks = null;

			if (!String.IsNullOrWhiteSpace(options.SearchString) &&
				outputProject.TargetFrameworks == null &&
				!outputProject.Name.Contains(options.SearchString, StringComparison.InvariantCultureIgnoreCase) &&
				outputProject.LibraryList == null)
			{
				return;
			}
			else
			{
				_distinctDependencies.Add(outputProject.Name);
			}
		}

		private void ProcessProjectFramework(AnalyzerOptions options, Project outputProject, LockFile lockFile, TargetFrameworkInformation targetFramework)
		{
			var lockFileTargetFramework = lockFile.Targets.FirstOrDefault(t => t.TargetFramework.Equals(targetFramework.FrameworkName));


			if (lockFileTargetFramework == null)
				return;

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
				_distinctDependencies.Add(outputFramework.Name);
			}
		}

		private void ProcessFrameworkDependency(AnalyzerOptions options, LockFileTarget lockFileTargetFramework, Framework outputFramework, NuGet.LibraryModel.LibraryDependency dependency)
		{
			var library = lockFileTargetFramework.Libraries.FirstOrDefault(library => library.Name == dependency.Name);
			if (library == null)
				return;
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
				_distinctDependencies.Add($"{outputDependency.Name} {outputDependency.Version}");
			}
		}

		private void ProcessChildDependencies(string? SearchString, LockFileTarget lockFileTargetFramework, LockFileTargetLibrary library, Dependency outputDependency)
		{
			if (library?.Dependencies?.Any() ?? false)
			{
				foreach (var dependency in library.Dependencies)
				{
					var childLibrary = lockFileTargetFramework.Libraries.FirstOrDefault(library => library.Name == dependency.Id);
					if (childLibrary == null)
						return;
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
						_distinctDependencies.Add($"{childDependency.Name} {childDependency.Version}");
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
			return LockFileUtilities.GetLockFile(lockFilePath, NugetPr.NullLogger.Instance);
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

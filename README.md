# DotNetDependencyAnalyzer

This program is intended to walk through all of the dotnet/nuget dependencies of a project or solution and return a graph of the results.  You can also search for specific dependencies.

All of the logic is in the DotNetDependencyAnalyzer.dll which can be added to your project.

---
## Running

![Pipeline status](https://github.com/traviswcoleman/DotNetDependencyAnalyzer/actions/workflows/Pipeline.yml/badge.svg)

The Console project is meant to be run as a command line tool

**Command-line**\
.\DotNetDependencyAnalyzer.exe <PathToSolution>\<SolutionName>.sln [-s \<path>] [-o \<path>]\
.\DotNetDependencyAnalyzer.exe <PathToSolution>\<PathToProject>\<ProjectName>.csproj [-s \<path>] [-o \<path>]

**Optional parameters:**\
-s|search \<dependency>: Searches for the specific dependency\
-o|output \<pathToOutput>: Writes the results to this file.  (Defaults to Output.json in the current directory)
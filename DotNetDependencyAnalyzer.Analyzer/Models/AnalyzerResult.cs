using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DotNetDependencyAnalyzer.Analyzer.Models
{
	public class AnalyzerResult
	{
		public AnalyzerResult(string rootPath)
		{
			this.RootPath = rootPath;
			Projects = new List<Project>();
		}

		public string RootPath { get; private set; }
		public IList<Project>? Projects { get; set; }
		public HashSet<string>? AllDependencies { get; set; }
	}
}

using System.Collections.Generic;

namespace DotNetDependencyAnalyzer.Analyzer.Models
{
	public class AnalyzerResult
	{
		public AnalyzerResult(string rootProject)
		{
			this.RootProject = rootProject;
			Projects = new List<Project>();
		}

		public string RootProject { get; private set; }
		public IList<Project> Projects { get; set; }
	}
}

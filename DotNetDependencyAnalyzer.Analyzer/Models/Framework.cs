using System.Collections.Generic;

namespace DotNetDependencyAnalyzer.Analyzer.Models
{
	public class Framework
	{
		public Framework(string name)
		{
			this.Name = name;
			this.Dependencies = new List<Dependency>();
		}

		public string Name { get; set; }
		public IList<Dependency> Dependencies { get; set; }
	}
}

using System;
using System.Collections.Generic;

namespace DotNetDependencyAnalyzer.Analyzer.Models
{
	public class Dependency
	{
		public Dependency(string name, Version version)
		{
			this.Name = name;
			this.Version = version;
			this.ChildDependencies = new List<Dependency>();
		}

		public string Name { get; set; }
		public Version Version { get; set; }
		public IList<Dependency> ChildDependencies { get; set; }
	}
}

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DotNetDependencyAnalyzer.Analyzer.Models
{
	public class Project
	{
		public Project(string name)
		{
			this.Name = name;
			TargetFrameworks = new List<Framework>();
			LibraryList = new HashSet<string>();
		}

		public string Name { get; set; }
		public IList<Framework>? TargetFrameworks { get; set; }
		public HashSet<string>? LibraryList { get; set; }
	}
}

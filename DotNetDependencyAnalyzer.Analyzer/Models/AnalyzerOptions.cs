using System;
using System.Collections.Generic;
using System.Text;


namespace DotNetDependencyAnalyzer.Analyzer.Models
{
	public class AnalyzerOptions
	{
		public string PathToFile { get; private set; }

		public AnalyzerOptions(string pathToFile)
		{
			PathToFile = pathToFile ?? throw new ArgumentNullException(nameof(pathToFile));
		}
	}
}

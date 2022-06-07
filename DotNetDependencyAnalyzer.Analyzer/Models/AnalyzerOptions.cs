using System;
using System.Collections.Generic;
using System.Text;


namespace DotNetDependencyAnalyzer.Analyzer.Models
{
	public class AnalyzerOptions
	{
		public string PathToFile { get; private set; }
		public string? OutputPath { get; private set; }
		public string? SearchString { get; private set; }

		public string? PathToTemp { get; private set; }

		public AnalyzerOptions(string pathToFile, string? outputPath = null, string? searchString = null, string? pathToTemp = null)
		{
			PathToFile = pathToFile ?? throw new ArgumentNullException(nameof(pathToFile));
			OutputPath = outputPath;
			SearchString = searchString;
			PathToTemp = pathToTemp;
		}
	}
}

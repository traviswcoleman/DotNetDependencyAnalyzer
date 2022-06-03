using System;
using System.Collections.Generic;
using System.Text;

using DotNetDependencyAnalyzer.Analyzer.Models;

namespace DotNetDependencyAnalyzer.Analyzer
{
	public interface IAnalyzerService
	{
		public AnalyzerResult AnalyzeProject(AnalyzerOptions options);
	}
}

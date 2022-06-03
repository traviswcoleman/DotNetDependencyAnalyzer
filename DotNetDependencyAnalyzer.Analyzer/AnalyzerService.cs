using System;

using DotNetDependencyAnalyzer.Analyzer.Models;

using Microsoft.Extensions.Logging;

namespace DotNetDependencyAnalyzer.Analyzer
{
	public class AnalyzerService : IAnalyzerService
	{
		private readonly ILogger<AnalyzerService> _logger;

		public AnalyzerService(ILogger<AnalyzerService> logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		public AnalyzerResult AnalyzeProject(AnalyzerOptions options)
		{
			_logger.LogInformation("AnalyzeProject called");
			return new AnalyzerResult();
		}
	}
}

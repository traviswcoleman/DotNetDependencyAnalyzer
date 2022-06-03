using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Extensions.DependencyInjection;

namespace DotNetDependencyAnalyzer.Analyzer
{
	public static class DIConfig
	{
		public static bool Configured { get; private set; } = false;

		public static IServiceCollection AddDependencyAnalyzer(this IServiceCollection services)
		{
			if (Configured)
				throw new InvalidOperationException("DependencyAnalyzer is already configured");

			Configured = true;
			services.AddTransient<IAnalyzerService, AnalyzerService>();

			return services;
		}
	}
}

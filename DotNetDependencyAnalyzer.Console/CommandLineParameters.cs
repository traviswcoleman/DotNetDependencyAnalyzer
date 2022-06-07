using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommandLine;

namespace DotNetDependencyAnalyzer
{
	
	internal class CommandLineParameters
	{
		[Value(0, HelpText ="The path to the solution file or project file", Required =true, MetaName ="File Path")]
		public string? FilePath { get; set; }

		[Option('s', Required =false, HelpText ="Search for a specific dependency")]
		public string? SearchString { get; set; }

		[Option('o', Required =false, HelpText ="Specify an output file to write with the results")]
		public string? OutputPath { get; set; }

		[Option('t', Required = false, HelpText = "Specify path for temp file")]
		public string? PathToTemp { get; set; }
	}
}

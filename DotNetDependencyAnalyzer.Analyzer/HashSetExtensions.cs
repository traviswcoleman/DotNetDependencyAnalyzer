using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetDependencyAnalyzer.Analyzer;
public static class HashSetExtensions
{
	public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> values)
	{
		foreach(T value in values)
			hashSet.Add(value);
	}
}

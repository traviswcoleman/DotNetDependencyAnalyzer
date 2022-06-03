namespace DotNetDependencyAnalyzer.Analyzer
{
    internal class DotnetStatus
    {
        public string StdOutputText { get; private set; }
        public string ErrorStreamText { get; private set; }
        public int ExitCode { get; private set; }

        public bool IsSuccess => ExitCode == 0 && string.IsNullOrWhiteSpace(ErrorStreamText);

        public DotnetStatus(string stdOutputText, string errorStreamText, int exitCode)
        {
            StdOutputText = stdOutputText;
            ErrorStreamText = errorStreamText;
            ExitCode = exitCode;
        }
        
    }
}
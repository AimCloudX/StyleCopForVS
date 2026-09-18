using System;

namespace StyleCopForVS
{
    /// <summary>A StyleCop violation mapped to zero-based editor coordinates.</summary>
    internal sealed class StyleCopDiagnostic
    {
        public StyleCopDiagnostic(string ruleId, string message, int startLine, int startColumn, int endLine, int endColumn)
        {
            RuleId = ruleId;
            Message = message;
            StartLine = startLine;
            StartColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;
        }

        public string RuleId { get; }
        public string Message { get; }
        public int StartLine { get; }
        public int StartColumn { get; }
        public int EndLine { get; }
        public int EndColumn { get; }
    }
}

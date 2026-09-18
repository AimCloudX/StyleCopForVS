using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;

namespace StyleCopForVS
{
    internal sealed class StyleCopTagger : ITagger<ErrorTag>, IDisposable
    {
        private readonly ITextBuffer buffer;
        private readonly ITextDocument document;
        private readonly StyleCopAnalysisService analysisService = new StyleCopAnalysisService();
        private readonly object gate = new object();
        private CancellationTokenSource analysisCancellation = new CancellationTokenSource();
        private IReadOnlyList<StyleCopDiagnostic> diagnostics = Array.Empty<StyleCopDiagnostic>();
        private ITextSnapshot diagnosticSnapshot;
        private bool disposed;

        public StyleCopTagger(ITextBuffer buffer, ITextDocument document)
        {
            this.buffer = buffer;
            this.document = document;
            buffer.Changed += BufferChanged;
            ScheduleAnalysis();
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans == null || spans.Count == 0)
            {
                yield break;
            }

            ITextSnapshot snapshot = spans[0].Snapshot;
            IReadOnlyList<StyleCopDiagnostic> currentDiagnostics;
            ITextSnapshot currentDiagnosticSnapshot;
            lock (gate)
            {
                currentDiagnostics = diagnostics;
                currentDiagnosticSnapshot = diagnosticSnapshot;
            }

            if (currentDiagnosticSnapshot == null || currentDiagnosticSnapshot != snapshot)
            {
                yield break;
            }

            foreach (StyleCopDiagnostic diagnostic in currentDiagnostics)
            {
                SnapshotSpan tagSpan = CreateSpan(snapshot, diagnostic);
                if (tagSpan.IntersectsWith(spans[0]) || spans.Any(span => tagSpan.IntersectsWith(span)))
                {
                    string message = string.Format("{0}: {1}", diagnostic.RuleId, diagnostic.Message);
                    yield return new TagSpan<ErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.Warning, message));
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            buffer.Changed -= BufferChanged;
            lock (gate)
            {
                analysisCancellation.Cancel();
                analysisCancellation.Dispose();
                diagnostics = Array.Empty<StyleCopDiagnostic>();
                diagnosticSnapshot = null;
            }
        }

        private void BufferChanged(object sender, TextContentChangedEventArgs e)
        {
            ScheduleAnalysis();
        }

        private void ScheduleAnalysis()
        {
            CancellationToken token;
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                analysisCancellation.Cancel();
                analysisCancellation.Dispose();
                analysisCancellation = new CancellationTokenSource();
                token = analysisCancellation.Token;
            }

            _ = AnalyzeAsync(token);
        }

        private async Task AnalyzeAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(350), token).ConfigureAwait(false);
                ITextSnapshot snapshot = buffer.CurrentSnapshot;
                string path = document.FilePath;
                IReadOnlyList<StyleCopDiagnostic> result = await Task.Run(
                    () => analysisService.Analyze(path, snapshot.GetText()),
                    token).ConfigureAwait(false);

                token.ThrowIfCancellationRequested();
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);

                lock (gate)
                {
                    if (disposed || token.IsCancellationRequested || buffer.CurrentSnapshot != snapshot)
                    {
                        return;
                    }

                    diagnostics = result;
                    diagnosticSnapshot = snapshot;
                }

                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
            }
            catch (OperationCanceledException)
            {
                // A newer buffer version superseded this analysis.
            }
            catch (Exception)
            {
                // StyleCop Classic is a legacy parser. A syntax it cannot parse
                // must not bring down Visual Studio or leave stale squiggles.
                lock (gate)
                {
                    if (!disposed && !token.IsCancellationRequested)
                    {
                        diagnostics = Array.Empty<StyleCopDiagnostic>();
                        diagnosticSnapshot = buffer.CurrentSnapshot;
                    }
                }
            }
        }

        private static SnapshotSpan CreateSpan(ITextSnapshot snapshot, StyleCopDiagnostic diagnostic)
        {
            if (snapshot.Length == 0)
            {
                return new SnapshotSpan(snapshot, 0, 0);
            }

            int startLineNumber = Math.Min(Math.Max(0, diagnostic.StartLine), snapshot.LineCount - 1);
            int endLineNumber = Math.Min(Math.Max(startLineNumber, diagnostic.EndLine), snapshot.LineCount - 1);
            ITextSnapshotLine startLine = snapshot.GetLineFromLineNumber(startLineNumber);
            ITextSnapshotLine endLine = snapshot.GetLineFromLineNumber(endLineNumber);
            int start = Math.Min(startLine.Start.Position + Math.Max(0, diagnostic.StartColumn), startLine.End.Position);
            int end = Math.Min(endLine.Start.Position + Math.Max(0, diagnostic.EndColumn), endLine.End.Position);

            if (end <= start)
            {
                end = Math.Min(snapshot.Length, Math.Max(start + 1, startLine.End.Position));
            }

            return new SnapshotSpan(snapshot, start, Math.Max(0, end - start));
        }
    }
}

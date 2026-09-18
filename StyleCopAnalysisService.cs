using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using StyleCop;

namespace StyleCopForVS
{
    /// <summary>
    /// Runs the StyleCop Classic 4.7 engine against the text currently held by
    /// the Visual Studio editor. A new core is used for every analysis because
    /// the Classic engine keeps parser/analyzer state between runs.
    /// </summary>
    internal sealed class StyleCopAnalysisService
    {
        public IReadOnlyList<StyleCopDiagnostic> Analyze(string filePath, string text)
        {
            if (string.IsNullOrEmpty(filePath) || text == null)
            {
                return Array.Empty<StyleCopDiagnostic>();
            }

            string fullPath = MakeUsablePath(filePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                directory = Environment.CurrentDirectory;
            }

            StyleCopCore core = null;
            ObjectBasedEnvironment environment = new ObjectBasedEnvironment(
                (path, project, parser, context) => new BufferSourceCode(path, project, parser, text),
                (settingsPath, readOnly) => LoadSettings(core, settingsPath));

            core = new StyleCopCore(environment)
            {
                DisplayUI = false,
                WriteResultsCache = false,
            };
            List<StyleCopDiagnostic> diagnostics = new List<StyleCopDiagnostic>();
            core.ViolationEncountered += (sender, args) =>
            {
                if (args != null && args.SourceCode != null &&
                    string.Equals(args.SourceCode.Path, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    StyleCopDiagnostic diagnostic = CreateDiagnostic(args.Violation);
                    if (diagnostic != null)
                    {
                        diagnostics.Add(diagnostic);
                    }
                }
            };

            try
            {
                string addInDirectory = Path.GetDirectoryName(typeof(StyleCop.CSharp.CsParser).Assembly.Location);
                core.Initialize(new[] { addInDirectory }, false);

                CodeProject project = new CodeProject(
                    fullPath.GetHashCode(),
                    directory,
                    new Configuration(new string[0]));

                if (!environment.AddSourceCode(project, fullPath, null))
                {
                    return Array.Empty<StyleCopDiagnostic>();
                }

                core.FullAnalyze(new[] { project });

                return diagnostics.ToArray();
            }
            finally
            {
            }
        }

        private static Settings LoadSettings(StyleCopCore core, string settingsPath)
        {
            string path = FindSettingsFile(settingsPath);
            if (path == null)
            {
                string extensionDirectory = Path.GetDirectoryName(typeof(StyleCopAnalysisService).Assembly.Location);
                path = FindSettingsFile(extensionDirectory);
            }

            if (path == null)
            {
                return null;
            }

            XmlDocument document = new XmlDocument();
            document.Load(path);
            return new Settings(core, path, document, File.GetLastWriteTimeUtc(path));
        }

        private static string FindSettingsFile(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            string candidate = location;
            if (Directory.Exists(location))
            {
                candidate = Path.Combine(location, Settings.DefaultFileName);
                if (!File.Exists(candidate))
                {
                    candidate = Path.Combine(location, Settings.AlternateFileName);
                }
            }

            return File.Exists(candidate) ? candidate : null;
        }

        private static string MakeUsablePath(string path)
        {
            if (!string.IsNullOrEmpty(path) && Path.IsPathRooted(path))
            {
                return path;
            }

            string name = string.IsNullOrEmpty(path) ? "Untitled.cs" : Path.GetFileName(path);
            return Path.Combine(Path.GetTempPath(), "StyleCopForVS", name);
        }

        private static StyleCopDiagnostic CreateDiagnostic(Violation violation)
        {
            if (violation == null || violation.Rule == null)
            {
                return null;
            }

            if (violation.Location.HasValue)
            {
                CodeLocation location = violation.Location.Value;
                return new StyleCopDiagnostic(
                    violation.Rule.CheckId,
                    violation.Message,
                    Math.Max(0, location.StartPoint.LineNumber - 1),
                    Math.Max(0, location.StartPoint.IndexOnLine - 1),
                    Math.Max(0, location.EndPoint.LineNumber - 1),
                    Math.Max(0, location.EndPoint.IndexOnLine));
            }

            int line = Math.Max(0, violation.Line - 1);
            return new StyleCopDiagnostic(violation.Rule.CheckId, violation.Message, line, 0, line, 0);
        }

        private sealed class BufferSourceCode : SourceCode
        {
            private readonly string path;
            private readonly string text;

            public BufferSourceCode(string path, CodeProject project, SourceParser parser, string text)
                : base(project, parser)
            {
                this.path = path;
                this.text = text;
            }

            public override bool Exists => true;
            public override string Name => System.IO.Path.GetFileName(path);
            public override string Path => path;
            public override DateTime TimeStamp => DateTime.UtcNow;
            public override string Type => "CS";

            public override TextReader Read()
            {
                return new StringReader(text);
            }
        }
    }
}

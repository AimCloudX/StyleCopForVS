using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace StyleCopForVS
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("CSharp")]
    [TagType(typeof(ErrorTag))]
    [Name("StyleCopClassicSquiggles")]
    internal sealed class StyleCopTaggerProvider : ITaggerProvider
    {
        [Import]
        internal ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            ITextDocument document;
            if (!TextDocumentFactoryService.TryGetTextDocument(buffer, out document))
            {
                return null;
            }

            return buffer.Properties.GetOrCreateSingletonProperty(
                () => new StyleCopTagger(buffer, document)) as ITagger<T>;
        }
    }
}

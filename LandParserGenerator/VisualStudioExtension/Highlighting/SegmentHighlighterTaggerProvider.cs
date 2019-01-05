using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace VisualStudioExtension.Highlighting
{
	[Export]
	[Export(typeof(IViewTaggerProvider))] /// Dependency Injection - в качестве IViewTaggerProvider будет импортироваться этот класс
	[ContentType("code")]
	[TagType(typeof(SegmentHighlighterTag))]
	public class SegmentHighlighterTaggerProvider : IViewTaggerProvider
	{
		/// <summary>
		/// Этот метод вызывается при открытии нового cs-файла, для каждого файла создаётся свой tagger
		/// </summary>
		/// <param name="textView"> The text view we are creating a tagger for</param>
		/// <param name="buffer"> The buffer that the tagger will examine for instances of the current word</param>
		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
		{
			// Only provide highlighting on the top-level buffer
			if (textView.TextBuffer != buffer)
				return null;

			return new SegmentHighlighterTagger(textView, buffer) as ITagger<T>;
		}
	}
}

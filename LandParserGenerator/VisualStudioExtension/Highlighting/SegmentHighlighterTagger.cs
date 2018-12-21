using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Media;
using Land.Control;
using Land.VisualStudioExtension;

namespace VisualStudioExtension.Highlighting
{
	public class SegmentHighlighterTag : TextMarkerTag
	{
		public SegmentHighlighterTag()
		   : base("MarkerFormatDefinition/SegmentFormatDefinition")
		{ }
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/SegmentFormatDefinition")]
	[UserVisible(true)]
	internal class SegmentFormatDefinition : MarkerFormatDefinition
	{
		public SegmentFormatDefinition()
		{
			this.Fill = new SolidColorBrush(LandExplorerControl.HighlightingColor);
			this.Border = new Pen(Brushes.Gray, 1.0);
			this.DisplayName = "Highlight Concern Segment";
			this.ZOrder = 5;
		}
	}

	public class SegmentHighlighterTagger : ITagger<SegmentHighlighterTag>
	{
		private ITextView _textView;
		private ITextBuffer _buffer;
		private List<DocumentSegment> _segments = new List<DocumentSegment>();

		public string FileName => GetFileName(_buffer);

		public SegmentHighlighterTagger(ITextView textView, ITextBuffer buffer)
		{
			_textView = textView;
			_buffer = buffer;

			EditorAdapter.OnSetSegments += OnSetSegments;
		}

		private string GetFileName(ITextBuffer buffer)
		{
			buffer.Properties.TryGetProperty(
				typeof(ITextDocument), out ITextDocument document);
			return document?.FilePath;
		}

		private void OnSetSegments(List<DocumentSegment> lst)
		{
			_segments = lst.Where(e => e.FileName == this.FileName).ToList();

			TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
				new SnapshotSpan(_buffer.CurrentSnapshot,
						new Span(0, _buffer.CurrentSnapshot.Length - 1))));
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public IEnumerable<ITagSpan<SegmentHighlighterTag>> GetTags(
			  NormalizedSnapshotSpanCollection spans)
		{
			var res = new List<ITagSpan<SegmentHighlighterTag>>();
			var currentSnapshot = _buffer.CurrentSnapshot;

			foreach (var segment in _segments)
			{
				var snapshotSpan = new SnapshotSpan(
					 currentSnapshot, 
					 new Span(segment.StartOffset, segment.EndOffset - segment.StartOffset)
				);
				res.Add(new TagSpan<SegmentHighlighterTag>(snapshotSpan,
					 new SegmentHighlighterTag()));
			}

			return res;
		}
	}
}

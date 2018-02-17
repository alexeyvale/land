using System;
using System.Collections.Generic;
using System.Windows.Media;

using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace TestGUI
{
	public class CurrentConcernHighlighter : IBackgroundRenderer
	{
		private TextArea textEditor { get; set; }
		private List<Tuple<int, int>> Segments { get; set; } = new List<Tuple<int, int>>();

		public CurrentConcernHighlighter(TextArea editor)
		{
			textEditor = editor;
		}

		public void SetSegments(List<Tuple<int, int>> segments)
		{
			Segments = segments;
			textEditor.InvalidateVisual();
		}

		public void ResetSegments()
		{
			SetSegments(new List<Tuple<int, int>>());
		}

		public KnownLayer Layer
		{
			get
			{
				return KnownLayer.Background;
			}
		}

		public void Draw(TextView textView, DrawingContext drawingContext)
		{
			textView.EnsureVisualLines();

			foreach(var segmentPos in Segments)
			{
				var segment = new TextSegment { StartOffset = segmentPos.Item1, EndOffset = segmentPos.Item2 };

				foreach (System.Windows.Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
				{
					drawingContext.DrawRoundedRectangle(
						new SolidColorBrush(Color.FromArgb(45, 100, 200, 100)),
						new Pen(new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)), 1),
						new System.Windows.Rect(r.Location, new System.Windows.Size(textView.ActualWidth, r.Height)),
						3, 3
					);
				}
			}
		}
	}
}

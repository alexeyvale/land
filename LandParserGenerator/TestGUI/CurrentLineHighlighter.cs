using System;
using System.Windows.Media;

using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace TestGUI
{
	public class CurrentLineHighlighter : IBackgroundRenderer
	{
		private TextArea textEditor { get; set; }

		public CurrentLineHighlighter(TextArea editor)
		{
			textEditor = editor;
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
			var line = textEditor.Document.GetLineByOffset(textEditor.Caret.Offset);
			var segment = new TextSegment { StartOffset = line.Offset, EndOffset = line.EndOffset };

			foreach (System.Windows.Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
			{
				drawingContext.DrawRoundedRectangle(
					new SolidColorBrush(Color.FromArgb(45, 180, 180, 180)),
					new Pen(new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)), 1),
					new System.Windows.Rect(r.Location, new System.Windows.Size(textView.ActualWidth, r.Height)),
					3, 3
				);
			}
		}
	}
}

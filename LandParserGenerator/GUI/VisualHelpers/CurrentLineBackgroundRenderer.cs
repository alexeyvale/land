using System;
using System.Windows.Media;

using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace Land.GUI
{
	public class CurrentLineBackgroundRenderer : IBackgroundRenderer
	{
		private TextArea TextEditor { get; set; }

		public CurrentLineBackgroundRenderer(TextArea editor)
		{
			TextEditor = editor;
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
			var line = TextEditor.Document.GetLineByOffset(TextEditor.Caret.Offset);
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

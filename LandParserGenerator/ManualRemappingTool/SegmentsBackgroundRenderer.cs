using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Shapes;

using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

using Land.Core;

namespace ManualRemappingTool
{
	public class SegmentsBackgroundRenderer : IBackgroundRenderer
	{
		private TextArea TextArea { get; set; }
		private List<Tuple<List<SegmentLocation>, Color>> SegmentGroups { get; set; } = new List<Tuple<List<SegmentLocation>, Color>>();
		
		public SegmentsBackgroundRenderer(TextArea editor)
		{
			TextArea = editor;
			TextArea.TextView.BackgroundRenderers.Add(this);
		}

		public void SetSegments(List<SegmentLocation> segments, Color color)
		{
			SegmentGroups.Add(new Tuple<List<SegmentLocation>, Color>(segments, color));
			TextArea.TextView.Redraw();
		}

		public void ResetSegments()
		{
			SegmentGroups = new List<Tuple<List<SegmentLocation>, Color>>();
			TextArea.TextView.Redraw();
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

			foreach (var segments in SegmentGroups)
			{
				foreach (var segment in segments.Item1)
				{
					var textSegment = new TextSegment { StartOffset = segment.Start.Offset, EndOffset = segment.End.Offset + 1 };

					foreach (System.Windows.Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, textSegment))
					{
						drawingContext.DrawRoundedRectangle(
							new SolidColorBrush(segments.Item2),
							new Pen(new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)), 1),
							new System.Windows.Rect(r.Location, new System.Windows.Size(r.Width, r.Height)),
							3, 3
						);
					}
				}
			}
		}
	}
}

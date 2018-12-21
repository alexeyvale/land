using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Linq;

using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

using Land.Control;

namespace Land.GUI
{
	public class SegmentsBackgroundRenderer : IBackgroundRenderer
	{
		private TextArea TextEditor { get; set; }
		private List<Tuple<HashSet<DocumentSegment>, Color>> SegmentGroups { get; set; } = new List<Tuple<HashSet<DocumentSegment>, Color>>();
		
		public SegmentsBackgroundRenderer(TextArea editor)
		{
			TextEditor = editor;
		}

		public void SetSegments(IEnumerable<DocumentSegment> segments, Color color)
		{
			SegmentGroups.Add(new Tuple<HashSet<DocumentSegment>, Color>(
				new HashSet<DocumentSegment>(segments), color)
			);

			TextEditor.TextView.Redraw();
		}

		public void ResetSegments(IEnumerable<DocumentSegment> segments = null)
		{
			if (segments?.Count() > 0)
			{
				for (var i = 0; i < SegmentGroups.Count; ++i)
				{
					SegmentGroups[i].Item1.ExceptWith(segments);

					if (SegmentGroups[i].Item1.Count == 0)
						SegmentGroups.RemoveAt(i--);
				}
			}
			else
			{
				SegmentGroups.Clear();
			}

			TextEditor.TextView.Redraw();
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
					var textSegment = new TextSegment { StartOffset = segment.StartOffset, EndOffset = segment.EndOffset + 1 };

					foreach (System.Windows.Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, textSegment))
					{
						drawingContext.DrawRoundedRectangle(
							new SolidColorBrush(segments.Item2),
							new Pen(new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)), 1),
							new System.Windows.Rect(r.Location, new System.Windows.Size(segment.CaptureWholeLine ? textView.ActualWidth : r.Width, r.Height)),
							3, 3
						);
					}
				}
			}
		}

		private static Brush GetHatchBrush(int hatchSize, Color hatchColor, Color backgroundColor)
		{
			var hatchVisual = new Canvas();
			hatchVisual.Children.Add(new Rectangle()
			{
				Fill = new SolidColorBrush(backgroundColor),
				Width = hatchSize,
				Height = hatchSize
			});
			hatchVisual.Children.Add(new Path()
			{
				Stroke = new SolidColorBrush(hatchColor),
				Data = PathGeometry.Parse($"M 0 0 l {hatchSize} {hatchSize}")
            });
			hatchVisual.Children.Add(new Path()
			{
				Stroke = new SolidColorBrush(hatchColor),
				Data = PathGeometry.Parse($"M 0 {hatchSize} l {hatchSize} -{hatchSize}")
            });

			return new VisualBrush()
			{
				TileMode = TileMode.Tile,
				Viewport = new System.Windows.Rect(0, 0, hatchSize, hatchSize),
				Viewbox = new System.Windows.Rect(0, 0, hatchSize, hatchSize),
				ViewboxUnits = BrushMappingMode.Absolute,
				ViewportUnits = BrushMappingMode.Absolute,
				Visual = hatchVisual
			};
		}
	}
}

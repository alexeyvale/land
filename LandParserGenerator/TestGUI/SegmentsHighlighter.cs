using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Shapes;

using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace TestGUI
{
	public class SegmentToHighlight
	{
		public int StartOffset { get; set; }
		public int EndOffset { get; set; }
		public bool HighlightWholeLine { get; set; }
	}

	public class SegmentsHighlighter : IBackgroundRenderer
	{
		private TextArea textEditor { get; set; }
		private List<Tuple<List<SegmentToHighlight>, Color>> SegmentGroups { get; set; } = new List<Tuple<List<SegmentToHighlight>, Color>>();

		private Random Generator { get; set; } = new Random();
		private Color[] ColorsList { get; set; } = new Color[] {
			Color.FromArgb(45, 100, 200, 100),
			Colors.Cyan,
			Colors.HotPink,
			Colors.Coral,
			Colors.Gold,
			Colors.LightSkyBlue,
			Colors.Thistle
		};

		public SegmentsHighlighter(TextArea editor)
		{
			textEditor = editor;
		}

		public Color SetSegments(List<SegmentToHighlight> segments, Color? color = null)
		{
			var currentColor = color.HasValue 
				? color.Value
				: SegmentGroups.Count < ColorsList.Length 
					? ColorsList[SegmentGroups.Count] 
					: Color.FromArgb(45, (byte)Generator.Next(100, 206), (byte)Generator.Next(100, 206), (byte)Generator.Next(100, 206));

			SegmentGroups.Add(new Tuple<List<SegmentToHighlight>, Color>(segments, currentColor));

			textEditor.TextView.Redraw();
			return currentColor;
		}

		public void ResetSegments()
		{
			SegmentGroups = new List<Tuple<List<SegmentToHighlight>, Color>>();
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
							new System.Windows.Rect(r.Location, new System.Windows.Size(segment.HighlightWholeLine ? textView.ActualWidth : r.Width, r.Height)),
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

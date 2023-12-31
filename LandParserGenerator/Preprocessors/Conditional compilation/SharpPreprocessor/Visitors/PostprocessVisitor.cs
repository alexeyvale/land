﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Land.Core;
using Land.Core.Parsing.Tree;

namespace SharpPreprocessing.ConditionalCompilation
{
	internal class PostprocessVisitor : BaseTreeVisitor
	{
		private List<SegmentLocation> SkippedSegments { get; set; }

		/// <summary>
		/// Сколько исключенных из компиляции символов было учтено на данный момент 
		/// </summary>
		private int IncludedCharsCount { get; set; } = 0;

		/// <summary>
		/// Сколько исключенных из компиляции строк было учтено на данный момент 
		/// </summary>
		private int IncludedLinesCount { get; set; } = 0;

		/// <summary>
		/// Сколько исключенных из компиляции участков было учтено на данный момент 
		/// </summary>
		private int IncludedSegmentsCount { get; set; } = 0;

		public PostprocessVisitor(List<SegmentLocation> segments)
		{
			SkippedSegments = segments;
		}

		public override void Visit(Node node)
		{
			/// У нелистового узла сбрасываем якорь, его нужно перевычислить
			/// после правки якорей листьев-потомков
			if (node.Children.Count > 0)
			{
				node.ResetLocation();
			}
			else
			{
				if (node.Location != null)
				{
					var start = node.Location.Start.Offset + IncludedCharsCount;

					/// Пока начало содержимого узла в текущих координатах лежит правее
					/// начала первого не возвращённого в рассмотрение сегмента в координатах исходного файла,
					/// поправляем текущие координаты с учётом добавления этого сегмента
					while (IncludedSegmentsCount < SkippedSegments.Count 
						&& SkippedSegments[IncludedSegmentsCount].Start.Offset <= start)
					{
						IncludedCharsCount += SkippedSegments[IncludedSegmentsCount].Length.Value;
						IncludedLinesCount += SkippedSegments[IncludedSegmentsCount].End.Line.Value 
							- SkippedSegments[IncludedSegmentsCount].Start.Line.Value + 1;
						start += SkippedSegments[IncludedSegmentsCount].Length.Value;
						IncludedSegmentsCount += 1;
					}

					node.Location.Shift(IncludedLinesCount, 0, IncludedCharsCount);
				}
			}

			base.Visit(node);
		}
	}
}

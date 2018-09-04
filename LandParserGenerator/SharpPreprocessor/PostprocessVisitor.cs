using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Land.Core;
using Land.Core.Parsing;
using Land.Core.Parsing.Tree;

using sharp_preprocessor;

namespace SharpPreprocessor
{
	internal class PostprocessVisitor : BaseTreeVisitor
	{
		private List<Segment> SkippedSegments { get; set; }

		/// <summary>
		/// Сколько исключенных из компиляции символов было учтено на данный момент 
		/// </summary>
		private int IncludedCharsCount { get; set; } = 0;

		/// <summary>
		/// Сколько исключенных из компиляции участков было учтено на данный момент 
		/// </summary>
		private int IncludedSegmentsCount { get; set; } = 0;

		public PostprocessVisitor(List<Segment> segments)
		{
			SkippedSegments = segments;
		}

		public override void Visit(Node node)
		{
			/// У нелистового узла сбрасываем якорь, его нужно перевычислить
			/// после правки якорей листьев-потомков
			if (node.Children.Count > 0)
			{
				node.ResetAnchor();
			}
			else
			{
				if (node.StartOffset.HasValue)
				{
					var start = node.StartOffset.Value + IncludedCharsCount;

					/// Пока начало содержимого узла в текущих координатах лежит правее
					/// начала первого не возвращённого в рассмотрение сегмента в координатах исходного файла,
					/// поправляем текущие координаты с учётом добавления этого сегмента
					while (IncludedSegmentsCount < SkippedSegments.Count 
						&& SkippedSegments[IncludedSegmentsCount].StartOffset <= start)
					{
						IncludedCharsCount += SkippedSegments[IncludedSegmentsCount].Length;
						start += SkippedSegments[IncludedSegmentsCount].Length;
						IncludedSegmentsCount += 1;
					}

					node.SetAnchor(start, node.EndOffset.Value + IncludedCharsCount);
				}
			}

			base.Visit(node);
		}
	}
}

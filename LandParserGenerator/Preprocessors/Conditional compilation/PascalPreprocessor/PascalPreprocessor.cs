using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Land.Core;
using Land.Core.Parsing;
using Land.Core.Parsing.Tree;
using Land.Core.Parsing.Preprocessing;

using pascalabc_preprocessor;

namespace PascalPreprocessing.ConditionalCompilation
{
	public class ExcludedSegmentLocation: SegmentLocation
	{
		public bool EndsOnEol { get; set; }
	}

	public class PascalPreprocessor: BasePreprocessor
    {		
		private BaseParser Parser { get; set; }
		public override List<Message> Log { get { return Parser?.Log; } }

		public List<ExcludedSegmentLocation> Excluded { get; set; } = new List<ExcludedSegmentLocation>();

		public PascalPreprocessor()
		{
			Parser = ParserProvider.GetParser();
		}

		public override string Preprocess(string text, out bool success)
		{
			/// Разбираем файл, находим директивы препроцессора
			var root = Parser.Parse(text);
			success = Parser.Log.Count == 0 || Parser.Log[Parser.Log.Count - 1].Type != MessageType.Error;

			/// Правим источник сообщений в логе
			foreach (var rec in Log)
				rec.Source = this.GetType().FullName;

			if (success)
			{
				var visitor = new DirectivesVisitor(text);
				root.Accept(visitor);

				for (var i = visitor.SegmentsToExclude.Count - 1; i >= 0; --i)
				{
					text = text.Remove(
						visitor.SegmentsToExclude[i].Start.Offset,
						visitor.SegmentsToExclude[i].Length.Value
					);
				}

				Excluded = visitor.SegmentsToExclude;

				return text;
			}
			else
			{
				return text;
			}
		}

		public override void Postprocess(Node root, List<Message> log)
		{
			if (Excluded.Count > 0)
			{
				var locations = log.Where(l => l.Location != null).Select(l => l.Location);

				if (root != null)
				{
					var getLocationsVisitor = new GatherLocationsVisitor();
					root.Accept(getLocationsVisitor);
					locations = locations.Concat(getLocationsVisitor.Locations);
				}

				locations = locations.Distinct().OrderBy(l => l.Offset);

				/// Сколько исключенных из компиляции символов было учтено на данный момент 
				var includedCharsCount = 0;
				/// Сколько исключенных из компиляции строк было учтено на данный момент 
				var includedLinesCount = 0;
				/// Сколько исключенных из компиляции строк было учтено на данный момент для текущей строки
				var includedColumnsCount = 0;
				/// Сколько исключенных из компиляции участков было учтено на данный момент 
				var includedSegmentsCount = 0;
				/// Номер последней строки, в которую включили удалённый сегмент
				var currentLineIndex = -1;

				foreach (var loc in locations)
				{
					var start = loc.Offset + includedCharsCount;

					/// Пока начало содержимого узла в текущих координатах лежит правее
					/// начала первого не возвращённого в рассмотрение сегмента в координатах исходного файла,
					/// поправляем текущие координаты с учётом добавления этого сегмента
					while (includedSegmentsCount < Excluded.Count
						&& Excluded[includedSegmentsCount].Start.Offset <= start)
					{
						includedCharsCount += Excluded[includedSegmentsCount].Length.Value;
						includedLinesCount += Excluded[includedSegmentsCount].End.Line.Value 
							- Excluded[includedSegmentsCount].Start.Line.Value 
							+ (Excluded[includedSegmentsCount].EndsOnEol ? 1 : 0);

						if (Excluded[includedSegmentsCount].Start.Line == Excluded[includedSegmentsCount].End.Line
							&& Excluded[includedSegmentsCount].Start.Line == currentLineIndex)
						{
							includedColumnsCount += Excluded[includedSegmentsCount].Length.Value;
						}
						else
						{
							currentLineIndex = Excluded[includedSegmentsCount].End.Line.Value;
							includedColumnsCount = Excluded[includedSegmentsCount].Start.Line == Excluded[includedSegmentsCount].End.Line
								? Excluded[includedSegmentsCount].Length.Value
								: Excluded[includedSegmentsCount].End.Column.Value + 1;
						}

						start += Excluded[includedSegmentsCount].Length.Value;
						includedSegmentsCount += 1;
					}

					loc.Shift(
						includedLinesCount,
						loc.Line + includedLinesCount == currentLineIndex ? includedColumnsCount : 0,
						includedCharsCount
					);
				}
			}
		}
	}
}

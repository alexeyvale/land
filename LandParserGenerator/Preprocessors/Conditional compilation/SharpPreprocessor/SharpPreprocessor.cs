﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Land.Core;
using Land.Core.Parsing;
using Land.Core.Parsing.Tree;
using Land.Core.Parsing.Preprocessing;

using sharp_preprocessor;

namespace SharpPreprocessing.ConditionalCompilation
{
	public class SharpPreprocessor: BasePreprocessor
    {		
		private BaseParser Parser { get; set; }
		public override List<Message> Log { get { return Parser?.Log; } }

		public List<SegmentLocation> Excluded { get; set; } = new List<SegmentLocation>();

		public SharpPreprocessor()
		{
			Parser = ParserProvider.GetParser();
			Properties = new SharpPreprocessorProperties();
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
				var visitor = new DirectivesVisitor(text, 
                    ((SharpPreprocessorProperties)Properties).PredefinedSymbols);
				root.Accept(visitor);

				for (var i = visitor.SegmentsToExclude.Count - 1; i >= 0; --i)
				{
					var length = Math.Max(
							text.IndexOf('\n', visitor.SegmentsToExclude[i].End.Offset), 
							visitor.SegmentsToExclude[i].End.Offset
						) - visitor.SegmentsToExclude[i].Start.Offset + 1;

					text = text.Remove(
						visitor.SegmentsToExclude[i].Start.Offset,
						length
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
				/// Сколько исключенных из компиляции участков было учтено на данный момент 
				var includedSegmentsCount = 0;

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
							- Excluded[includedSegmentsCount].Start.Line.Value + 1;
						start += Excluded[includedSegmentsCount].Length.Value;
						includedSegmentsCount += 1;
					}

					loc.Shift(includedLinesCount, 0, includedCharsCount);
				}
			}
		}
	}
}

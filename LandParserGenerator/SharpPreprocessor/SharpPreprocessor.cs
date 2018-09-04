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
	public class Segment
	{
		public int StartOffset { get; set; }
		public int Length { get; set; }
	}

	public class SharpPreprocessor: BasePreprocessor
    {		
		private BaseParser Parser { get; set; }
		public override List<Message> Log { get { return Parser?.Log; } }

		public List<Segment> SegmentsToSkip { get; set; } = new List<Segment>();

		public SharpPreprocessor()
		{
			Parser = ParserProvider.GetParser();
		}

		public override string Preprocess(string text, out bool success)
		{
			/// Разбираем файл, находим директивы препроцессора
			var root = Parser.Parse(text);
			success = Parser.Log[Parser.Log.Count - 1].Type != MessageType.Error;

			/// Правим источник сообщений в логе
			foreach (var rec in Log)
				rec.Source = this.GetType().FullName;

			if (success)
			{
				var visitor = new DirectivesVisitor(text);
				root.Accept(visitor);

				for (var i = visitor.SectionsToExclude.Count - 1; i >= 0; --i)
				{
					var length = text.IndexOf('\n', visitor.SectionsToExclude[i].Item2)
						- visitor.SectionsToExclude[i].Item1 + 1;

					text = text.Remove(
						visitor.SectionsToExclude[i].Item1,
						length
					);

					SegmentsToSkip.Add(new Segment()
					{
						Length = length,
						StartOffset = visitor.SectionsToExclude[i].Item1
					});
				}

				SegmentsToSkip.Reverse();

				return text;
			}
			else
			{
				return text;
			}
		}

		public override void Postprocess(Node root, List<Message> log)
		{
			var visitor = new PostprocessVisitor(SegmentsToSkip);
			root.Accept(visitor);


		}
	}
}

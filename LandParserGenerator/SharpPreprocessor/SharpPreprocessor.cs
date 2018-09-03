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
	internal class DirectivesVisitor : BaseTreeVisitor
	{
		public class LevelInfo
		{
			public bool CompileCurrentSection { get; set; }
			public bool SkipAll { get; set; }
		}

		private string Text { get; set; }
		public Stack<LevelInfo> Levels { get; set; } = new Stack<LevelInfo>();

		public List<Tuple<int, int>> SectionsToSkip = new List<Tuple<int, int>>();
		public int SkippingSectionStart { get; set; }

		public HashSet<string> SymbolsDefined = new HashSet<string>();

		public DirectivesVisitor(string text)
		{
			Text = text;
		}

		public override void Visit(Node node)
		{
			switch (node.Symbol)
			{
				case "if_part":
					/// Весь if нужно пропустить, если он вложен в пропускаемую секцию
					var skipAll = Levels.Count != 0 && !Levels.Peek().CompileCurrentSection;

					Levels.Push(new LevelInfo()
					{
						CompileCurrentSection = !skipAll && VisitCondition(node.Children[1]),
						SkipAll = skipAll
					});

					if (!Levels.Peek().CompileCurrentSection)
						SkippingSectionStart = node.StartOffset.Value;
					break;
				case "elif_part":
				case "else_part":
					/// Если не нужно пропустить всю оставшуюся часть текущего if
					if (!Levels.Peek().SkipAll)
					{
						/// и предыдущая секция компилируемая
						if (Levels.Peek().CompileCurrentSection)
						{
							/// всё продолжение будет некомпилируемое
							Levels.Peek().SkipAll = true;
							Levels.Peek().CompileCurrentSection = false;
						}
						else
						{
							Levels.Peek().CompileCurrentSection 
								= node.Symbol == "else_part" || VisitCondition(node.Children[1]);
						}

						if (!Levels.Peek().CompileCurrentSection)
							SkippingSectionStart = node.StartOffset.Value;
						else
							SectionsToSkip.Add(new Tuple<int, int>(SkippingSectionStart, Text.IndexOf('\n', node.StartOffset.Value)));
					}
					break;
				case "endif_part":
					if (!Levels.Peek().CompileCurrentSection)
						SectionsToSkip.Add(new Tuple<int, int>(SkippingSectionStart, Text.IndexOf('\n', node.StartOffset.Value)));
					Levels.Pop();
					break;
				case "define":
					for (var i = 1; i < node.Children.Count; ++i)
						SymbolsDefined.Add(node.Children[i].Symbol);
					break;
				case "undef":
					for (var i = 1; i < node.Children.Count; ++i)
						SymbolsDefined.Remove(node.Children[i].Symbol);
					break;
				default:
					base.Visit(node);
					break;
			}
		}

		public bool VisitCondition(Node node)
		{
			switch (node.Symbol)
			{
				case "condition":
					return VisitCondition(node.Children[0]) || VisitCondition(node.Children[1]);
				case "or_right":
					return node.Children.Count > 0 ? VisitCondition(node.Children[1]) || VisitCondition(node.Children[2]) : false;
				case "ands":
					return VisitCondition(node.Children[0]) && VisitCondition(node.Children[1]);
				case "and_right":
					return node.Children.Count > 0 ? VisitCondition(node.Children[1]) || VisitCondition(node.Children[2]) : true;
				case "atom_or_neg":
					return node.Children.Count > 1 ? !VisitCondition(node.Children[1]) : VisitCondition(node.Children[0]);
				case "atom":
					return node.Children.Count > 1 ? VisitCondition(node.Children[1]) : VisitCondition(node.Children[0]);
				case "ID":
					return node.Value[0] == "true" || SymbolsDefined.Contains(node.Value[0]);
				default:
					return false;
			}
		}
	}

	internal class PostprocessVisitor : BaseTreeVisitor
	{
		private List<Segment> SkippedSegments { get; set; }

		private int LastSum { get; set; } = 0;
		private int LastIdx { get; set; } = 0;

		public PostprocessVisitor(List<Segment> segments)
		{
			SkippedSegments = segments;
		}

		public override void Visit(Node node)
		{
			if (node.Children.Count > 0)
			{
				node.ResetAnchor();
			}
			else
			{
				if (node.StartOffset.HasValue)
				{
					var start = node.StartOffset.Value + LastSum;

					while(LastIdx < SkippedSegments.Count && SkippedSegments[LastIdx].StartOffset <= start)
					{
						LastSum += SkippedSegments[LastIdx].Length;
						start += SkippedSegments[LastIdx].Length;
						LastIdx += 1;
					}

					node.SetAnchor(start, node.EndOffset.Value + LastSum);
				}
			}

			base.Visit(node);
		}
	}

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

				for (var i = visitor.SectionsToSkip.Count - 1; i >= 0; --i)
				{
					var length = text.IndexOf('\n', visitor.SectionsToSkip[i].Item2)
						- visitor.SectionsToSkip[i].Item1 + 1;

					text = text.Remove(
						visitor.SectionsToSkip[i].Item1,
						length
					);

					SegmentsToSkip.Add(new Segment()
					{
						Length = length,
						StartOffset = visitor.SectionsToSkip[i].Item1
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Land.Core;
using Land.Core.Parsing.Tree;

namespace SharpPreprocessor
{

	internal class DirectivesVisitor : BaseTreeVisitor
	{
		public class LevelInfo
		{
			/// <summary>
			/// Нужно ли включать в компилируемый код текущую секцию директивы #if
			/// </summary>
			public bool IncludeCurrentSegment { get; set; }

			/// <summary>
			/// Нужно ли исключить из компиляции всё от текущего места до конца
			/// </summary>
			public bool ExcludeToEnd { get; set; }

			/// <summary>
			/// Является ли текущий if вложенным в исключаемую область
			/// </summary>
			public bool NestedInExcluded { get; set; }
		}

		/// <summary>
		/// Текст, в котором нужно найти исключаемые участки
		/// </summary>
		private string Text { get; set; }

		/// <summary>
		/// Уровни вложенности директив
		/// </summary>
		public Stack<LevelInfo> Levels { get; set; } = new Stack<LevelInfo>();

		/// <summary>
		/// Области, которые нужно исключить из компиляции
		/// </summary>
		public List<SegmentLocation> SegmentsToExclude = new List<SegmentLocation>();

		/// <summary>
		/// Текущая исключаемая секция
		/// </summary>
		public SegmentLocation CurrentSegment { get; set; }

		/// <summary>
		/// Определённые в текущем месте программы символы
		/// </summary>
		public HashSet<string> SymbolsDefined = new HashSet<string>();

		public DirectivesVisitor(string text, HashSet<string> predefined = null)
		{
			Text = text;

			if (predefined != null)
				SymbolsDefined.UnionWith(predefined);
		}

		private void InitCurrentSegment(PointLocation start)
		{
			CurrentSegment = new SegmentLocation()
			{
				Start = start
			};
		}

		private void FinCurrentSegment(PointLocation end)
		{
			var lineEndOffset = Math.Max(Text.IndexOf('\n', end.Offset), end.Offset);

			CurrentSegment.End = new PointLocation(end.Line, end.Column, lineEndOffset);
		}

		public override void Visit(Node node)
		{
			switch (node.Symbol)
			{
				case "if":
					/// Весь if нужно пропустить, если он вложен в пропускаемую секцию
					var nestedInExcluded = Levels.Count != 0 && !Levels.Peek().IncludeCurrentSegment;

					Levels.Push(new LevelInfo()
					{
						IncludeCurrentSegment = !nestedInExcluded && VisitCondition(node.Children[1]),
						ExcludeToEnd = nestedInExcluded,
						NestedInExcluded = nestedInExcluded
					});

					if (!nestedInExcluded && !Levels.Peek().IncludeCurrentSegment)
						InitCurrentSegment(node.Anchor.Start);

					break;
				case "elif":
				case "else":
					/// Если не нужно пропустить всю оставшуюся часть текущего if
					if (!Levels.Peek().NestedInExcluded && !Levels.Peek().ExcludeToEnd)
					{
						/// и предыдущая секция компилируемая
						if (Levels.Peek().IncludeCurrentSegment)
						{
							/// всё продолжение будет некомпилируемое
							Levels.Peek().ExcludeToEnd = true;
							Levels.Peek().IncludeCurrentSegment = false;
							InitCurrentSegment(node.Anchor.Start);
						}
						else
						{
							Levels.Peek().IncludeCurrentSegment 
								= node.Symbol == "else" || VisitCondition(node.Children[1]);

							/// Если текущая секция компилируемая, добавляем предыдущую к списку пропускаемых областей
							/// иначе пропустим её одним блоком с предыдущей
							if (Levels.Peek().IncludeCurrentSegment)
							{
								FinCurrentSegment(node.Anchor.End);
								SegmentsToExclude.Add(CurrentSegment);
							}
						}
					}
					break;
				case "endif":
					if (!Levels.Peek().NestedInExcluded && !Levels.Peek().IncludeCurrentSegment)
					{
						FinCurrentSegment(node.Anchor.End);
						SegmentsToExclude.Add(CurrentSegment);
					}
					Levels.Pop();
					break;
				case "define":
					if (Levels.Count == 0 || Levels.Peek().IncludeCurrentSegment)
					{
						for (var i = 1; i < node.Children.Count; ++i)
							SymbolsDefined.Add(node.Children[i].Value[0]);
					}
					break;
				case "undef":
					if (Levels.Count == 0 || Levels.Peek().IncludeCurrentSegment)
					{
						for (var i = 1; i < node.Children.Count; ++i)
							SymbolsDefined.Remove(node.Children[i].Value[0]);
					}
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
}

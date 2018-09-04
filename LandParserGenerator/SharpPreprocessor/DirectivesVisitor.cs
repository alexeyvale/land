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
			/// <summary>
			/// Нужно ли включать в компилируемый код текущую секцию директивы #if
			/// </summary>
			public bool IncludeCurrentSection { get; set; }

			/// <summary>
			/// Нужно ли исключить из компиляции всё от текущего места до конца
			/// </summary>
			public bool ExcludeToEnd { get; set; }

			/// <summary>
			/// Является ли текущий if вложенным в исключаемую область
			/// </summary>
			public bool NestedInExcluded { get; set; }
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
				case "if":
					/// Весь if нужно пропустить, если он вложен в пропускаемую секцию
					var nestedInExcluded = Levels.Count != 0 && !Levels.Peek().IncludeCurrentSection;

					Levels.Push(new LevelInfo()
					{
						IncludeCurrentSection = !nestedInExcluded && VisitCondition(node.Children[1]),
						ExcludeToEnd = nestedInExcluded,
						NestedInExcluded = nestedInExcluded
					});

					if (!nestedInExcluded && !Levels.Peek().IncludeCurrentSection)
						SkippingSectionStart = node.StartOffset.Value;
					break;
				case "elif":
				case "else":
					/// Если не нужно пропустить всю оставшуюся часть текущего if
					if (!Levels.Peek().NestedInExcluded && !Levels.Peek().ExcludeToEnd)
					{
						/// и предыдущая секция компилируемая
						if (Levels.Peek().IncludeCurrentSection)
						{
							/// всё продолжение будет некомпилируемое
							Levels.Peek().ExcludeToEnd = true;
							Levels.Peek().IncludeCurrentSection = false;
							SkippingSectionStart = node.StartOffset.Value;
						}
						else
						{
							Levels.Peek().IncludeCurrentSection 
								= node.Symbol == "else" || VisitCondition(node.Children[1]);

							/// Если текущая секция компилируемая, добавляем предыдущую к списку пропускаемых областей
							/// иначе пропустим её одним блоком с предыдущей
							if (Levels.Peek().IncludeCurrentSection)
							{
								SectionsToSkip.Add(
									new Tuple<int, int>(SkippingSectionStart, Text.IndexOf('\n', node.StartOffset.Value))
								);
							}
						}
					}
					break;
				case "endif":
					if (!Levels.Peek().NestedInExcluded && !Levels.Peek().IncludeCurrentSection)
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
}

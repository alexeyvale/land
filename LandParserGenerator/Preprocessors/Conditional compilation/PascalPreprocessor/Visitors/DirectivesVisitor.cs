using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Land.Core;
using Land.Core.Parsing.Tree;

namespace PascalPreprocessing.ConditionalCompilation
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
		public List<ExcludedSegmentLocation> SegmentsToExclude = new List<ExcludedSegmentLocation>();

		/// <summary>
		/// Текущая исключаемая секция
		/// </summary>
		public ExcludedSegmentLocation CurrentSegment { get; set; }

		/// <summary>
		/// Определённые в текущем месте программы символы
		/// </summary>
		public HashSet<string> SymbolsDefined = new HashSet<string>();

		public DirectivesVisitor(string text)
		{
			Text = text;
		}

		public override void Visit(Node node)
		{
			if (node.Symbol == "directive")
			{
				switch (node.Children[1].Value[0])
				{
					case "ifdef":
					case "ifndef":
						/// Весь if нужно пропустить, если он вложен в пропускаемую секцию
						var nestedInExcluded = Levels.Count != 0 && !Levels.Peek().IncludeCurrentSegment;

						Levels.Push(new LevelInfo()
						{
							IncludeCurrentSegment = !nestedInExcluded
								&& !(node.Children[1].Value[0] == "ifdef" ^ SymbolsDefined.Contains(node.Children[2].Value[0])),
							ExcludeToEnd = nestedInExcluded,
							NestedInExcluded = nestedInExcluded
						});

						if (!nestedInExcluded && !Levels.Peek().IncludeCurrentSegment)
							CurrentSegment = new ExcludedSegmentLocation() { Start = node.Anchor.Start };

						break;
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
								CurrentSegment = new ExcludedSegmentLocation() { Start = node.Anchor.Start };
							}
							else
							{
								Levels.Peek().IncludeCurrentSegment = true;

								/// Если текущая секция компилируемая, добавляем предыдущую к списку пропускаемых областей
								/// иначе пропустим её одним блоком с предыдущей
								if (Levels.Peek().IncludeCurrentSegment)
								{
									CurrentSegment.End = node.Anchor.End;
									CurrentSegment.EndsOnEol = Text[CurrentSegment.End.Offset] == '\n';

									SegmentsToExclude.Add(CurrentSegment);
								}
							}
						}
						break;
					case "endif":
						if (!Levels.Peek().NestedInExcluded && !Levels.Peek().IncludeCurrentSegment)
						{
							CurrentSegment.End = node.Anchor.End;
							CurrentSegment.EndsOnEol = Text[CurrentSegment.End.Offset] == '\n';

							SegmentsToExclude.Add(CurrentSegment);
						}
						Levels.Pop();
						break;
					case "define":
						if (Levels.Count == 0 || Levels.Peek().IncludeCurrentSegment)
						{
							SymbolsDefined.Add(node.Children[2].Value[0]);
						}
						break;
					case "undef":
						if (Levels.Count == 0 || Levels.Peek().IncludeCurrentSegment)
						{
							SymbolsDefined.Remove(node.Children[2].Value[0]);
						}
						break;
				}
			}
			else
			{
				base.Visit(node);
			}
		}
	}
}

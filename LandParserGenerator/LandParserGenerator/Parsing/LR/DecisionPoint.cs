using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.LR
{
	public class StackTop
	{
		public int State { get; set; }
		public Node Symbol { get; set; }
	}

	public abstract class DecisionPoint
	{
		/// <summary>
		/// Индекс токена, на котором было принято решение
		/// </summary>
		public int DecisionTokenIndex { get; set; }

		/// <summary>
		/// Вершины стеков символов и состояний
		/// </summary>
		public StackTop ParsingStackTop { get; set; }
	}

	public class ChooseTransitionDecision : DecisionPoint
	{ }

	public class FinishAnyDecision : DecisionPoint
	{	
		/// <summary>
		/// Сколько было предпринято попыток перепринять
		/// решение в этой точке
		/// </summary>
		public int AttemptsCount { get; set; }

		/// <summary>
		/// Узел, который соответствует символу Any
		/// </summary>
		public Node AnyNode { get; set; }
	}
}

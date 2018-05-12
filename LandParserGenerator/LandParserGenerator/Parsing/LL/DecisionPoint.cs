using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.LL
{
	public abstract class DecisionPoint
	{
		/// <summary>
		/// Индекс токена, на котором было принято решение
		/// </summary>
		public int DecisionTokenIndex { get; set; }

		/// <summary>
		/// Ссылка на узел, находившийся на вершине стека разбора
		/// на момент принятия решения
		/// </summary>
		public Node ParsingStackTop { get; set; }
	}

	public class ChooseAlternativeDecision : DecisionPoint
	{
		/// <summary>
		/// Список альтернатив, из которых можно выбрать в этой точке
		/// </summary>
		public List<Alternative> Alternatives { get; set; }

		/// <summary>
		/// Индекс выбранной в настоящий момент альтернативы
		/// </summary>
		public int ChosenIndex { get; set; }
	}

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

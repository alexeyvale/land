using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing
{
	public abstract class DecisionPoint
	{
		/// <summary>
		/// Индекс токена, на котором было принято решение
		/// </summary>
		public int DecisionTokenIndex { get; set; }
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
	}
}

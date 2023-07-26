using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core.Specification
{
	public class FirstBuilder
	{
		private bool UseModifiedFirst { get; set; }
		private Grammar GrammarObject { get; set; }

		public FirstBuilder(Grammar g, bool useModifiedFirst)
		{
			UseModifiedFirst = useModifiedFirst;
			GrammarObject = g;

			BuildFirst();
		}

		private Dictionary<string, HashSet<string>> FirstCache { get; set; }

		/// <summary>
		/// Построение множеств FIRST для нетерминалов
		/// </summary>
		private void BuildFirst()
		{
			FirstCache = new Dictionary<string, HashSet<string>>();

			/// Изначально множества пустые
			foreach (var nt in GrammarObject.Rules)
			{
				FirstCache[nt.Key] = new HashSet<string>();
			}

			var changed = true;

			/// Пока итеративно вносятся изменения
			while (changed)
			{
				changed = false;

				/// Проходим по всем альтернативам и пересчитываем FIRST 
				foreach (var nt in GrammarObject.Rules)
				{
					var oldCount = FirstCache[nt.Key].Count;

					foreach (var alt in nt.Value)
					{
						FirstCache[nt.Key].UnionWith(First(alt));
					}

					if (!changed)
					{
						changed = oldCount != FirstCache[nt.Key].Count;
					}
				}
			}
		}

		public HashSet<string> First(List<string> sequence)
		{
			/// FIRST последовательности - это либо FIRST для первого символа,
			/// либо, если последовательность пустая, null
			if (sequence.Count > 0)
			{
				var first = new HashSet<string>();
				var elementsCounter = 0;

				/// Если первый элемент - нетерминал, из которого выводится пустая строка,
				/// нужно взять first от следующего элемента
				for (; elementsCounter < sequence.Count; ++elementsCounter)
				{
					var elemFirst = First(sequence[elementsCounter]);
					var containsEmpty = elemFirst.Remove(null);

					first.UnionWith(elemFirst);

					/// Если из текущего элемента нельзя вывести пустую строку
					/// и (для модифицированной версии First) он не равен ANY
					if (!containsEmpty
						&& (!UseModifiedFirst || sequence[elementsCounter] != Grammar.ANY_TOKEN_NAME))
						break;
				}

				if (elementsCounter == sequence.Count)
					first.Add(null);

				return first;
			}
			else
			{
				return new HashSet<string>() { null };
			}
		}

		public HashSet<string> First(Alternative alt)
		{
			return First(alt.Elements.Select(e => e.Symbol).ToList());
		}

		public HashSet<string> First(string symbol)
		{
			var gramSymbol = GrammarObject[symbol];

			return gramSymbol is NonterminalSymbol
				? new HashSet<string>(FirstCache[gramSymbol.Name])
				: new HashSet<string>() { gramSymbol.Name };
		}
	}
}

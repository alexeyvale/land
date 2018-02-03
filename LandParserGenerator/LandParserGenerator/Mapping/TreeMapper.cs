using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Mapping
{
	public class ConcernMapping
	{
		/// Веса операций для Левенштейна
		private const double InsertionCost = 1;
		private const double DeletionCost = 1;
		private const double SubstitutionCost = 1;

		private Grammar Gram { get; set; }

		public Dictionary<Node, Node> Mapping { get; set; }
		public Dictionary<Node, Dictionary<Node, double>> Similarities { get; set; }

		public ConcernMapping(Grammar g)
		{
			Gram = g;
		}

		public void Remap(List<ConcernPoint> point, Node oldTree, Node newTree)
		{
			var visitor = new LandExplorerVisitor();
			oldTree.Accept(visitor);

			/// Отображаем острова из одного дерева в острова из другого
			var nodesToRemap = visitor.Land;
			visitor.Land = new List<Node>();
			newTree.Accept(visitor);
			var candidates = visitor.Land;

			Mapping = new Dictionary<Node, Node>();
			Similarities = new Dictionary<Node, Dictionary<Node, double>>();

			foreach(var oldNode in nodesToRemap)
			{
				if(!Mapping.ContainsKey(oldNode))
				{
					var newCandidates = candidates.Where(c => c.Symbol == oldNode.Symbol);
					var similarities = newCandidates.ToDictionary(c=>c, c => Similarity(oldNode, c));
					var maxSimilarity = similarities.Max(s => s.Value);
					Mapping[oldNode] = similarities.Where(s => s.Value == maxSimilarity).First().Key;
				}
			}
		}

		private double Similarity(Node a, Node b)
		{
			/// Если узлы разных типов, они точно непохожи
			if (a.Symbol != b.Symbol)
				return 0;

			/// Если узлы - листья, смотрим на похожесть содержимого
			if (a.Value.Count > 0 && b.Value.Count > 0)
				return Levenshtein(a.Value, b.Value);

			/// Иначе сопоставляем детей
			var rawSimilarity = a.Children.Where(c=>Mapping.ContainsKey(c) && b.Children.Contains(Mapping[c])).Sum(c=>Similarities[c][Mapping[c]]);
			/// Рассматриваем только тех детей, которые ещё не были ничему сопоставлены
			var aTypes = a.Children.Where(c=>!Mapping.ContainsKey(c)).GroupBy(c => c.Symbol).ToDictionary(g => g.Key, g => g.ToList());
			var bTypes = b.Children.Where(c=>!Mapping.ContainsValue(c)).GroupBy(c => c.Symbol).ToDictionary(g => g.Key, g => g.ToList());		

			/// Каждого потомка узла a пытаемся сопоставить с потомками узла b того же типа
			foreach (var type in aTypes.Keys)
				if (bTypes.ContainsKey(type))
				{
					var similarities = new double[aTypes[type].Count, bTypes[type].Count];

					/// В рамках типа сопоставляем всех со всеми
					foreach (var aNode in aTypes[type])
					{
						Similarities[aNode] = new Dictionary<Node, double>();
						foreach (var bNode in bTypes[type])
							Similarities[aNode][bNode] = Similarity(aNode, bNode);
					}					

					/// Выбираем наилучший вариант
					var usedNodes = new HashSet<Node>();
					foreach(var aNode in aTypes[type])
					{
						var maxSimilarity = Similarities[aNode].Max(p => p.Value);
						Mapping[aNode] = Similarities[aNode].First(p => p.Value == maxSimilarity).Key;
						usedNodes.Add(Mapping[aNode]);
					}
				}

			return rawSimilarity / a.Children.Count;
		}

		/// Расстояние Левенштейна
		private static double Levenshtein<T>(IEnumerable<T> a, IEnumerable<T> b)
		{
			if (a.Count() == 0 ^ b.Count() == 0)
				return 1;
			if (a.Count() == 0 && b.Count() == 0)
				return 0;

			Type elementType = a.ElementAt(0).GetType();
			bool containsEnumerable = typeof(System.Collections.IEnumerable).IsAssignableFrom(elementType); 

			/// Сразу отбрасываем общие префиксы и суффиксы
			var commonPrefixLength = 0;
			while (a.ElementAt(commonPrefixLength).Equals(b.ElementAt(commonPrefixLength)))
				++commonPrefixLength;
			a = a.Skip(commonPrefixLength);
			b = b.Skip(commonPrefixLength);

			var commonSuffixLength = 0;
			while (a.ElementAt(a.Count() - 1 - commonSuffixLength).Equals(b.ElementAt(b.Count() - 1 - commonSuffixLength)))
				++commonSuffixLength;
			a = a.Take(a.Count() - commonSuffixLength);
			b = b.Take(b.Count() - commonSuffixLength);

			/// Согласно алгоритму Вагнера-Фишера, вычисляем матрицу расстояний
			var distances = new double[a.Count() + 1, b.Count() + 1];
			distances[0, 0] = 0;

			/// Заполняем первую строку и первый столбец
			for (int i = 1; i <= a.Count(); ++i)
				distances[i, 0] = distances[i - 1, 0] + DeletionCost;
			for (int j = 1; j <= b.Count(); ++j)
				distances[0, j] = distances[0, j - 1] + InsertionCost;

			for (int i = 1; i <= a.Count(); i++)
				for (int j = 1; j <= b.Count(); j++)
				{
					/// Если элементы - это тоже перечислимые наборы элементов, считаем для них расстояние
					double cost = b.ElementAt(j - 1).Equals(a.ElementAt(i - 1)) ? 
						0 : containsEnumerable ? Levenshtein((IEnumerable<dynamic>)a.ElementAt(i-1), (IEnumerable<dynamic>)b.ElementAt(j-1)) : SubstitutionCost;
					distances[i, j] = Math.Min(Math.Min(
						distances[i - 1, j] + DeletionCost, 
						distances[i, j - 1] + InsertionCost),
						distances[i - 1, j - 1] + cost);
				}

			return 1 - distances[a.Count(), b.Count()] / Math.Max(a.Count(), b.Count());
		}
	}
}

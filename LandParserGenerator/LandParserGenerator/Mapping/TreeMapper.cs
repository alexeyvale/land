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
		private Dictionary<Node, Node> Mapping { get; set; }

		public ConcernMapping(Grammar g)
		{
			Gram = g;
		}

		public void Remap(List<ConcernPoint> point, Node oldTree, Node newTree)
		{
			var visitor = new LandExplorerVisitor();
			oldTree.Accept(visitor);

			var nodesToRemap = visitor.Land;
			visitor.Land = new List<Node>();
			newTree.Accept(visitor);
			var candidates = visitor.Land;

			Mapping = new Dictionary<Node, Node>();
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
			if (a.Symbol != b.Symbol)
				return 0;

			if (a.Value.Count > 0 && b.Value.Count > 0)
				return Levenshtein(a.Value, b.Value); // В диапазон от 0 до 1!!!!!!!

			var aTypes = a.Children.GroupBy(c => c.Symbol).ToDictionary(g => g.Key, g => g.ToList());
			var bTypes = b.Children.GroupBy(c => c.Symbol).ToDictionary(g => g.Key, g => g.ToList());

			var rawSimilarity = 0.0;

			foreach (var type in aTypes.Keys)
				if (bTypes.ContainsKey(type))
				{
					var similarities = new double[aTypes[type].Count, bTypes[type].Count];

					for (int i = 0; i < aTypes[type].Count; ++i)
						for (int j = 0; j < aTypes[type].Count; ++j)
							similarities[i, j] = Similarity(aTypes[type][i], bTypes[type][j]);
				}

			return rawSimilarity / a.Children.Count;
		}

		/// Расстояние Левенштейна
		private static double Levenshtein<T>(IEnumerable<T> a, IEnumerable<T> b)
		{
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
					double cost = b.ElementAt(j - 1).Equals(a.ElementAt(i - 1)) ? 0 : SubstitutionCost;
					distances[i, j] = Math.Min(Math.Min(
						distances[i - 1, j] + DeletionCost, 
						distances[i, j - 1] + InsertionCost),
						distances[i - 1, j - 1] + cost);
				}
			return distances[a.Count(), b.Count()];
		}
	}
}

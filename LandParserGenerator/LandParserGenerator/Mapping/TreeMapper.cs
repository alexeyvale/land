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
		const double InsertionCost = 1;
		const double DeletionCost = 1;
		const double SubstitutionCost = 1;

		public static void Remap(List<ConcernPoint> point, Node oldTree, Node newTree)
		{

		}

		private static double Similarity(Node a, Node b)
		{
			return 0;
		}

		/// Временная копипаста
		private static double Levenshtein(string a, string b)
		{
			/// Если одна из строк пустая, они непохожи
			if (String.IsNullOrEmpty(a) ^ String.IsNullOrEmpty(b))
				return 0;

			/// Согласно алгоритму Вагнера-Фишера, вычисляем матрицу расстояний
			var distances = new int[a.Length + 1, b.Length + 1];

			/// Заполняем первую строку и первый столбец
			for (int i = 0; i <= a.Length; distances[i, 0] = i++) ;
			for (int j = 0; j <= b.Length; distances[0, j] = j++) ;

			for (int i = 1; i <= a.Length; i++)
				for (int j = 1; j <= b.Length; j++)
				{
					int cost = b[j - 1] == a[i - 1] ? 0 : 1;
					distances[i, j] = Math.Min(
						Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
						distances[i - 1, j - 1] + cost
					);
				}
			return distances[a.Length, b.Length];
		}

		private static double Levenshtein(List<string> a, List<string> b)
		{
			return 0;
		}

	}
}

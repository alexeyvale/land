using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class NodeSimilarityPair
	{
		public Node Node { get; set; }
		public PointContext Context { get; set; }
		public double Similarity { get; set; }
	}

	public static class ContextFinder
	{
		/// Веса операций для Левенштейна
		private const double InsertionCost = 1;
		private const double DeletionCost = 1;
		private const double SubstitutionCost = 1;

		private const double ChildrenContextWeight = 1;
		private const double AncestorsContextWeight = 1;
		private const double SiblingsContextWeight = 0.5;

		/// <summary>
		/// Поиск узла дерева, соответствующего заданному контексту
		/// </summary>
		/// <param name="context"></param>
		/// <param name="tree"></param>
		/// <returns>Список кандидатов, отсортированных по степени похожести</returns>
		public static List<NodeSimilarityPair> Find(PointContext context, string fileName, Node tree)
		{
			var result = new List<NodeSimilarityPair>();
			var groupVisitor = new GroupNodesByTypeVisitor(context.NodeType);

			tree.Accept(groupVisitor);

			if (groupVisitor.Grouped.ContainsKey(context.NodeType))
			{
				foreach (var node in groupVisitor.Grouped[context.NodeType])
				{
					var nodeContext = new PointContext(node, fileName);

					result.Add(new NodeSimilarityPair()
					{
						Node = node,
						Context = nodeContext,
						Similarity = Similarity(context, nodeContext)
					});
				}
			}

			return result.OrderByDescending(p=>p.Similarity).ToList();
		}

		/// Возвращает оценку похожести контекста на контекст переданного узла
		private static double Similarity(PointContext context, PointContext candidateContext)
		{
			/// Сравниваем контекст потомков
			var childrenSimilarity = 
				ChildrenContextSimilarity(context.ChildrenContext, candidateContext.ChildrenContext);

			/// Сравниваем контекст предков
			var ancestorsSimilarity = 
				AncestorsContextSimilarity(context.AncestorsContext, candidateContext.AncestorsContext);

			return (childrenSimilarity * ChildrenContextWeight + ancestorsSimilarity * AncestorsContextWeight) 
				/ (AncestorsContextWeight + ChildrenContextWeight);
		}

		private static double AncestorsContextSimilarity(List<OuterContextElement> originContext, List<OuterContextElement> candidateContext)
		{
			var candidateAncestorsContext =
				candidateContext.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g);
			var ancestorsMapping = new Dictionary<OuterContextElement, OuterContextElement>();
			var rawSimilarity = 0.0;

			foreach (var ancestor in originContext
				.Where(oc => candidateAncestorsContext.ContainsKey(oc.Type)))
			{
				var similarities = new Dictionary<OuterContextElement, double>();

				foreach (var candidateAncestor in candidateAncestorsContext[ancestor.Type])
				{
					similarities[candidateAncestor] =
						ChildrenContextSimilarity(ancestor.ChildrenContext, candidateAncestor.ChildrenContext);
				}

				var bestCandidate = similarities.OrderByDescending(s => s.Value).FirstOrDefault();

				if (bestCandidate.Key != null)
				{
					ancestorsMapping[ancestor] = bestCandidate.Key;
					rawSimilarity += bestCandidate.Value;
				}
			}

			return rawSimilarity / originContext.Count;
		}

		private static double ChildrenContextSimilarity(List<InnerContextElement> originContext, List<InnerContextElement> candidateContext)
		{
			var candidateChildrenContext = 
				candidateContext.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g);
			var childrenMapping = new Dictionary<InnerContextElement, InnerContextElement>();
			var rawSimilarity = 0.0;

			foreach (var child in originContext
				.Where(oc=>candidateChildrenContext.ContainsKey(oc.Type)))
			{
				var similarities = new Dictionary<InnerContextElement, double>();

				foreach (var candidateChild in candidateChildrenContext[child.Type])
					similarities[candidateChild] = Levenshtein(child.Value, candidateChild.Value);

				var bestCandidate = similarities.OrderByDescending(s => s.Value).FirstOrDefault();

				if (bestCandidate.Key != null)
				{
					childrenMapping[child] = bestCandidate.Key;
					rawSimilarity += bestCandidate.Value * child.Priority;
				}
			}

			return rawSimilarity / originContext.Sum(c => c.Priority);
		}

		///  Похожесть на основе расстояния Левенштейна
		private static double Levenshtein<T>(IEnumerable<T> a, IEnumerable<T> b)
		{
			if (a.Count() == 0 ^ b.Count() == 0)
				return 0;
			if (a.Count() == 0 && b.Count() == 0)
				return 1;

			/// Сразу отбрасываем общие префиксы и суффиксы
			var commonPrefixLength = 0;
			while (commonPrefixLength < a.Count() && commonPrefixLength < b.Count() 
				&& a.ElementAt(commonPrefixLength).Equals(b.ElementAt(commonPrefixLength)))
				++commonPrefixLength;
			a = a.Skip(commonPrefixLength);
			b = b.Skip(commonPrefixLength);

			var commonSuffixLength = 0;
			while (commonSuffixLength < a.Count() && commonSuffixLength < b.Count()
				&& a.ElementAt(a.Count() - 1 - commonSuffixLength).Equals(b.ElementAt(b.Count() - 1 - commonSuffixLength)))
				++commonSuffixLength;
			a = a.Take(a.Count() - commonSuffixLength).ToList();
			b = b.Take(b.Count() - commonSuffixLength).ToList();

			if (a.Count() == 0 && b.Count() == 0)
				return 1;

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
						0 : (1 - Levenshtein(a.ElementAt(i-1), b.ElementAt(j-1)));
					distances[i, j] = Math.Min(Math.Min(
						distances[i - 1, j] + DeletionCost, 
						distances[i, j - 1] + InsertionCost),
						distances[i - 1, j - 1] + cost);
				}

			return 1 - distances[a.Count(), b.Count()] / 
				Math.Max(a.Count() + commonSuffixLength + commonPrefixLength, b.Count() + commonSuffixLength + commonPrefixLength);
		}

		private static double Levenshtein<T>(T a, T b)
		{
			if (a is IEnumerable<string>)
				return Levenshtein((IEnumerable<string>)a, (IEnumerable<string>)b);
			else if (a is string)
				return Levenshtein((IEnumerable<char>)a, (IEnumerable<char>)b);
			else return a.Equals(b) ? 1 : 1 - SubstitutionCost;
		}
	}
}

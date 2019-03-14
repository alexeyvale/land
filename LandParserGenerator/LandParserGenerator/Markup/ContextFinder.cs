using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class RemapCandidateInfo
	{
		private const double HeaderContextWeight = 1;
		private const double AncestorsContextWeight = 0.5;
		private const double InnerContextWeight = 0.6;
		private const double SiblingsContextWeight = 0.4;

		public Node Node { get; set; }
		public PointContext Context { get; set; }

		public double? HeaderSimilarity { get; set; }
		public double? AncestorSimilarity { get; set; }
		public double? InnerSimilarity { get; set; }
		public double? SiblingsSimilarity { get; set; }

		public double Similarity => 
			((HeaderSimilarity ?? 1) * HeaderContextWeight 
			+ (AncestorSimilarity ?? 1) * AncestorsContextWeight
			+ (InnerSimilarity ?? 1) * InnerContextWeight)
			/ (HeaderContextWeight + AncestorsContextWeight + InnerContextWeight);

		public override string ToString()
		{
			return $"{String.Format("{0:f4}", Similarity)} [H: {String.Format("{0:f2}", HeaderSimilarity)}; A: {String.Format("{0:f2}", AncestorSimilarity)}; I: {String.Format("{0:f2}", InnerSimilarity)}]";
		}
	}

	public static class ContextFinder
	{
		/// <summary>
		/// Поиск узлов дерева, соответствующих точкам привязки
		/// </summary>
		/// <param name="points">Точки привязки, сгруппированные по типу связанного с ними узла</param>
		/// <param name="candidateNodes">Узлы дерева, среди которых нужно найти соответствующие точкам, также сгруппированные по типу</param>
		/// <returns></returns>
		public static Dictionary<AnchorPoint, List<RemapCandidateInfo>> Find(
			Dictionary<string, List<AnchorPoint>> points, 
			Dictionary<string, List<Node>> candidateNodes, 
			TargetFileInfo candidateFileInfo
		)
		{
			var result = new Dictionary<AnchorPoint, List<RemapCandidateInfo>>();

			foreach (var typePointsPair in points)
			{
				foreach (var point in typePointsPair.Value)
				{
					var candidates = candidateNodes.ContainsKey(typePointsPair.Key)
						? candidateNodes[typePointsPair.Key].Select(node =>
							new RemapCandidateInfo()
							{
								Node = node,
								Context = new PointContext()
								{
									FileName = candidateFileInfo.FileName,
									NodeType = node.Type
								}
							}).ToList()
						: new List<RemapCandidateInfo>();

					foreach (var candidate in candidates)
					{
						candidate.Context.HeaderContext = PointContext.GetHeaderContext(candidate.Node);
						candidate.HeaderSimilarity = Levenshtein(point.Context.HeaderContext, candidate.Context.HeaderContext);
					}

					foreach (var candidate in candidates)
					{
						candidate.Context.AncestorsContext = PointContext.GetAncestorsContext(candidate.Node);
						candidate.AncestorSimilarity = Levenshtein(point.Context.AncestorsContext, candidate.Context.AncestorsContext);
					}

					foreach (var candidate in candidates)
					{
						candidate.Context.InnerContext = PointContext.GetInnerContext(
							new TargetFileInfo() { FileName = candidateFileInfo.FileName, FileText = candidateFileInfo.FileText, TargetNode = candidate.Node }
						);
						candidate.InnerSimilarity = Levenshtein(point.Context.InnerContext, candidate.Context.InnerContext);
					}

					result[point] = candidates;
				}
			}

			return result;
		}

		public static List<RemapCandidateInfo> Find(AnchorPoint point, TargetFileInfo targetInfo)
		{
			var visitor = new GroupNodesByTypeVisitor(new List<string> { point.Context.NodeType });
			targetInfo.TargetNode.Accept(visitor);

			return Find(
				new Dictionary<string, List<AnchorPoint>> { { point.Context.NodeType, new List<AnchorPoint> { point } } },
				visitor.Grouped, targetInfo
			)[point];
		}

		private static double Similarity(List<AncestorsContextElement> originContext, List<AncestorsContextElement> candidateContext)
		{
			var candidateAncestorsContext =
				candidateContext.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g);
			var ancestorsMapping = new Dictionary<AncestorsContextElement, AncestorsContextElement>();
			var rawSimilarity = 0.0;

			foreach (var ancestor in originContext
				.Where(oc => candidateAncestorsContext.ContainsKey(oc.Type)))
			{
				var similarities = new Dictionary<AncestorsContextElement, double>();

				foreach (var candidateAncestor in candidateAncestorsContext[ancestor.Type])
				{
					similarities[candidateAncestor] = Similarity(ancestor.HeaderContext, candidateAncestor.HeaderContext);
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

		private static double Similarity(List<HeaderContextElement> originContext, List<HeaderContextElement> candidateContext)
		{
			var candidateChildrenContext =
				candidateContext.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g);
			var childrenMapping = new Dictionary<HeaderContextElement, HeaderContextElement>();
			var rawSimilarity = 0.0;

			foreach (var child in originContext
				.Where(oc => candidateChildrenContext.ContainsKey(oc.Type)))
			{
				var similarities = new Dictionary<HeaderContextElement, double>();

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

		private static double Similarity(List<InnerContextElement> originContext, List<InnerContextElement> candidateContext)
		{
			var candidateInnerContext =
				candidateContext.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g);
			var childrenMapping = new Dictionary<InnerContextElement, InnerContextElement>();
			var rawSimilarity = 0.0;

			foreach (var child in originContext
				.Where(oc => candidateInnerContext.ContainsKey(oc.Type)))
			{
				var similarities = new Dictionary<InnerContextElement, double>();

				foreach (var candidateChild in candidateInnerContext[child.Type])
					similarities[candidateChild] = FuzzyHashing.CompareHashes(child.Hash, candidateChild.Hash);

				var bestCandidate = similarities.OrderByDescending(s => s.Value).FirstOrDefault();

				if (bestCandidate.Key != null)
				{
					childrenMapping[child] = bestCandidate.Key;
					rawSimilarity += bestCandidate.Value * child.Priority;
				}
			}

			return rawSimilarity / originContext.Sum(c => c.Priority);
		}

		private static double Similarity(List<SiblingsContextElement> originContext, List<SiblingsContextElement> candidateContext)
		{
			return 1;
		}

		///  Похожесть на основе расстояния Левенштейна
		private static double Levenshtein<T>(IEnumerable<T> a, IEnumerable<T> b)
		{
			if (a.Count() == 0 ^ b.Count() == 0)
				return 0;
			if (a.Count() == 0 && b.Count() == 0)
				return 1;

			var denominator = 0.0;

			if (a is IEnumerable<TypedPrioritizedContextElement>)
			{
				var comparer = new EqualsIgnoreValueComparer();

				var aSockets = (a as IEnumerable<IEqualsIgnoreValue>)
					.GroupBy(e => e, comparer).ToDictionary(g => g.Key, g => g.Count());
				var bSockets = (b as IEnumerable<IEqualsIgnoreValue>)
					.GroupBy(e => e, comparer).ToDictionary(g => g.Key, g => g.Count());

				denominator += aSockets.Sum(kvp 
					=> ((TypedPrioritizedContextElement)kvp.Key).Priority * kvp.Value);

				foreach (var kvp in aSockets)
				{
					var sameKey = bSockets.Keys.FirstOrDefault(e => e.EqualsIgnoreValue(kvp.Key));

					if (sameKey != null)
						bSockets[sameKey] -= kvp.Value;
				}

				denominator += bSockets.Where(kvp => kvp.Value > 0)
					.Sum(kvp => ((TypedPrioritizedContextElement)kvp.Key).Priority * kvp.Value);
			}
			else
			{
				denominator = Math.Max(a.Count(), b.Count());
			}

			/// Сразу отбрасываем общие префиксы и суффиксы
			var commonPrefixLength = 0;
			while (commonPrefixLength < a.Count() && commonPrefixLength < b.Count()
				&& a.ElementAt(commonPrefixLength).Equals(b.ElementAt(commonPrefixLength)))
				++commonPrefixLength;			
			a = a.Skip(commonPrefixLength).ToList();
			b = b.Skip(commonPrefixLength).ToList();

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
				distances[i, 0] = distances[i - 1, 0] + PriorityCoefficient(a.ElementAt(i - 1));
			for (int j = 1; j <= b.Count(); ++j)
				distances[0, j] = distances[0, j - 1] + PriorityCoefficient(b.ElementAt(j - 1));

			for (int i = 1; i <= a.Count(); i++)
				for (int j = 1; j <= b.Count(); j++)
				{
					/// Если элементы - это тоже перечислимые наборы элементов, считаем для них расстояние
					double cost = 1 - DispatchLevenshtein(a.ElementAt(i - 1), b.ElementAt(j - 1));
					distances[i, j] = Math.Min(Math.Min(
						distances[i - 1, j] + PriorityCoefficient(a.ElementAt(i - 1)),
						distances[i, j - 1] + PriorityCoefficient(b.ElementAt(j - 1))),
						distances[i - 1, j - 1] + PriorityCoefficient(a.ElementAt(i - 1)) * cost);
				}

			return 1 - distances[a.Count(), b.Count()] / denominator;
		}

		private static double PriorityCoefficient(object elem)
		{
			switch(elem)
			{
				case HeaderContextElement headerContext:
					return headerContext.Priority;
				case InnerContextElement innerContext:
					return innerContext.Priority;
				default:
					return 1;
			}
		}

		/// Похожесть новой последовательности на старую 
		/// при переходе от последовательности a к последовательности b
		private static double DispatchLevenshtein<T>(T a, T b)
		{
			if (a is IEnumerable<string>)
				return Levenshtein((IEnumerable<string>)a, (IEnumerable<string>)b);
			if (a is IEnumerable<HeaderContextElement>)
				return Levenshtein((IEnumerable<HeaderContextElement>)a, (IEnumerable<HeaderContextElement>)b);
			if (a is IEnumerable<AncestorsContextElement>)
				return Levenshtein((IEnumerable<AncestorsContextElement>)a, (IEnumerable<AncestorsContextElement>)b);
			else if (a is string)
				return Levenshtein((IEnumerable<char>)a, (IEnumerable<char>)b);
			else if (a is HeaderContextElement)
				return EvalSimilarity(a as HeaderContextElement, b as HeaderContextElement);
			else if(a is InnerContextElement)
				return EvalSimilarity(a as InnerContextElement, b as InnerContextElement);
			else if (a is AncestorsContextElement)
				return EvalSimilarity(a as AncestorsContextElement, b as AncestorsContextElement);
			else
				return a.Equals(b) ? 1 : 0;
		}

		private static double EvalSimilarity(HeaderContextElement a, HeaderContextElement b)
		{
			if (a.EqualsIgnoreValue(b))
			{
				return a.ExactMatch
					? String.Join("", a.Value) == String.Join("", b.Value) ? 1 : 0
					: Levenshtein(String.Join("", a.Value), String.Join("", b.Value));
			}
			else
				return 0;
		}

		private static double EvalSimilarity(InnerContextElement a, InnerContextElement b)
		{
			if (a.EqualsIgnoreValue(b))
			{
				var score = 0d;

				if (a.Text != null && b.Text != null)
					score = Levenshtein(a.Text, b.Text);
				else if (a.Hash != null && b.Hash != null)
					score = FuzzyHashing.CompareHashes(a.Hash, b.Hash);
				/// Если мы попали сюда, строка из одного контекста не захеширована, 
				/// так как слишком короткая, а в другом контексте есть только хэш без текста. 
				/// Такое возможно только если длина одной строки меньше MIN_TEXT_LENGTH, 
				/// а второй - больше MAX_TEXT_LENGTH
				else
					score = 0;

				return score < FuzzyHashing.MIN_TEXT_LENGTH / (double)InnerContextElement.MAX_TEXT_LENGTH ? 0 : score;
			}
			else
				return 0;
		}

		private static double EvalSimilarity(AncestorsContextElement a, AncestorsContextElement b)
		{
			return a.Type == b.Type ? Levenshtein(a.HeaderContext, b.HeaderContext) : 0;
		}
	}
}

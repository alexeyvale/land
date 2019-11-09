using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Land.Markup.Binding
{
	/// <summary>
	/// Присваивает итоговую оценку 1 кандидату с полностью совпадающими заголовком и предками
	/// </summary>
	public class SameHeaderAndAncestorsHeuristic : ISimilarityHeuristic
	{
		public long Priority => 10;

		public List<RemapCandidateInfo> PredictSimilarity(
			PointContext source,
			List<RemapCandidateInfo> candidates)
		{
			var bestMatch = candidates.Where(c => c.HeaderSimilarity == 1 && c.AncestorSimilarity == 1).ToList();

			if (bestMatch.Count == 1)
				bestMatch.Single().Similarity = 1;

			return candidates;
		}
	}
}
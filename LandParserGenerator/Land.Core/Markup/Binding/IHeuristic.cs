using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Land.Markup.Binding
{
	public interface IHeuristic	{ }

	public interface IWeightsHeuristic: IHeuristic
	{
		Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights
		);
	}

	public interface ISimilarityHeuristic: IHeuristic
	{
		List<RemapCandidateInfo> PredictSimilarity(
			PointContext source,
			List<RemapCandidateInfo> candidates
		);
	}
}
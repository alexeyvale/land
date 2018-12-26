using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Land.Core.Markup;

namespace Land.Control
{
	public class PointCandidatesPair
	{
		public ConcernPoint Point { get; set; }

		public ObservableCollection<NodeSimilarityPair> Candidates { get; set; } =
			new ObservableCollection<NodeSimilarityPair>();
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Land.Core.Parsing.Tree;

namespace Land.Markup.Binding
{
	public interface IContextFinder
	{
		Dictionary<ConcernPoint, List<RemapCandidateInfo>> Find(
			Dictionary<string, List<ConcernPoint>> points,
			Dictionary<string, List<Node>> candidateNodes,
			ParsedFile candidateFileInfo);

		List<RemapCandidateInfo> Find(ConcernPoint point, ParsedFile targetInfo);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public interface IContextFinder
	{
		Dictionary<ConcernPoint, List<IRemapCandidateInfo>> Find(
			Dictionary<string, List<ConcernPoint>> points,
			Dictionary<string, List<Node>> candidateNodes,
			TargetFileInfo candidateFileInfo);

		List<IRemapCandidateInfo> Find(ConcernPoint point, TargetFileInfo targetInfo);
	}
}

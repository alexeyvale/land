using System;
using System.Collections.Generic;

using Land.Core.Parsing.Tree;

namespace Land.Core.Parsing.Preprocessing
{
	public class NoopPreprocessor: BasePreprocessor
	{
		public override string Preprocess(string source, out bool success)
		{
			success = true;
			return source;
		}

		public override void Postprocess(Node root, List<Message> log) { }
	}
}

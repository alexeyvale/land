using System;
using System.Collections.Generic;

using Land.Core.Parsing.Tree;

namespace Land.Core.Parsing.Preprocessing
{
	public class PipelinePreprocessor : BasePreprocessor
	{
		private List<BasePreprocessor> Pipeline { get; set; } = new List<BasePreprocessor>();

		public PipelinePreprocessor(params BasePreprocessor[] preprocs)
		{
			foreach (var preproc in preprocs)
				Pipeline.Add(preproc);
		}

		public override string Preprocess(string source, out bool success)
		{
			var preprocessed = source;
			success = true;
			Log = new List<Message>();

			foreach (var preproc in Pipeline)
			{
				preprocessed = preproc.Preprocess(preprocessed, out success);
				Log.AddRange(preproc.Log);

				if (!success)
					return source;
			}

			return preprocessed;
		}

		public override void Postprocess(Node root, List<Message> log)
		{
			foreach (var preproc in Pipeline)
				 preproc.Postprocess(root, log);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;
using Land.Core.Parsing.Tree;

namespace Land.Core.Parsing
{
	public abstract class BasePreprocessor
	{
		public virtual List<Message> Log { get; set; } = new List<Message>();

		public abstract string Preprocess(string text, out bool success);

		public abstract void Postprocess(Node root, List<Message> log);
	}

	public class NoopPreprocessor: BasePreprocessor
	{
		public override string Preprocess(string source, out bool success)
		{
			success = true;
			return source;
		}

		public override void Postprocess(Node root, List<Message> log) { }
	}

	public class PipelinePreprocessor : BasePreprocessor
	{
		private List<BasePreprocessor> Preprocs { get; set; } = new List<BasePreprocessor>();

		public PipelinePreprocessor(params BasePreprocessor[] preprocs)
		{
			foreach (var preproc in preprocs)
				Preprocs.Add(preproc);
		}

		public override string Preprocess(string source, out bool success)
		{
			var preprocessed = source;
			success = true;
			Log = new List<Message>();

			foreach (var preproc in Preprocs)
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
			foreach (var preproc in Preprocs)
				 preproc.Postprocess(root, log);
		}
	}
}

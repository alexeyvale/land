using System;
using System.Collections.Generic;

using Land.Core;
using Land.Core.Parsing.Preprocessing;
using Land.Core.Parsing.Tree;

namespace SharpPreprocessing.TreePostprocessing
{
	public class SharpPreprocessor : BasePreprocessor
	{
		private PipelinePreprocessor Pipeline { get; set; } = new PipelinePreprocessor(
			new SharpPreprocessing.ConditionalCompilation.SharpPreprocessor(),
			new InternalSharpPreprocessor()
		);

		public override void Postprocess(Node root, List<Message> log)
		{
			Pipeline.Postprocess(root, log);
		}

		public override string Preprocess(string text, out bool success)
		{
			return Pipeline.Preprocess(text, out success);
		}
	}

	internal class InternalSharpPreprocessor : BasePreprocessor
	{
		public override void Postprocess(Node root, List<Message> log)
		{
			var visitor = new EntityIdentificationVisitor();
			root.Accept(visitor);

			Log.AddRange(visitor.Log);
			foreach (var rec in Log)
				rec.Source = this.GetType().FullName;	
		}

		public override string Preprocess(string text, out bool success)
		{
			success = true;
			return text;
		}
	}
}

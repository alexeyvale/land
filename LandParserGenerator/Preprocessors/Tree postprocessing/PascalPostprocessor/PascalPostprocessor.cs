using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core;
using Land.Core.Parsing.Preprocessing;
using Land.Core.Parsing.Tree;

namespace PascalPreprocessing.TreePostprocessing
{
	public class PascalPostprocessor : BasePreprocessor
	{
		private PipelinePreprocessor Pipeline { get; set; } = new PipelinePreprocessor(
			new PascalPreprocessing.ConditionalCompilation.PascalPreprocessor(),
			new InternalPascalPreprocessor()
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

	internal class InternalPascalPreprocessor : BasePreprocessor
	{
		public override void Postprocess(Node root, List<Message> log)
		{
			var visitor = new RoutineAggregationVisitor();
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

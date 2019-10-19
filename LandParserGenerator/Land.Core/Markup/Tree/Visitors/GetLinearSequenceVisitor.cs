using System;
using System.Linq;
using System.Collections.Generic;

namespace Land.Markup.Tree
{
	public class GetLinearSequenceVisitor : BaseMarkupVisitor
	{
		public List<MarkupElement> Sequence { get; set; }
			= new List<MarkupElement>();

		public override void Visit(ConcernPoint point)
		{
			Sequence.Add(point);
		}

		public override void Visit(Concern concern)
		{
			base.Visit(concern);
			Sequence.Add(concern);
		}

		public static List<MarkupElement> GetElements(IEnumerable<MarkupElement> roots)
		{
			var visitor = new GetLinearSequenceVisitor();

			foreach (var root in roots)
				root.Accept(visitor);

			return visitor.Sequence;
		}

		public static List<ConcernPoint> GetPoints(IEnumerable<MarkupElement> roots)
		{
			return GetElements(roots).Select(e=>e as ConcernPoint)
				.Where(e=>e != null).ToList();
		}

		public static List<Concern> GetConcerns(IEnumerable<MarkupElement> roots)
		{
			return GetElements(roots).Select(e => e as Concern)
				.Where(e => e != null).ToList();
		}
	}
}

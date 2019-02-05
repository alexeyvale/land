using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public static class RelationsExtension
	{
		#region Пространственные отношения между участками, соответствующими точкам привязки

		public static bool Spatial_IsSiblingOf(this ConcernPoint a, ConcernPoint b)
		{
			return GetAstLandParent(a) == GetAstLandParent(b);
		}

		public static bool Spatial_Preceeds(this ConcernPoint a, ConcernPoint b)
		{
			return a.Spatial_IsSiblingOf(b)
				&& GetAstLandParent(a).Children.IndexOf(a.AstNode) <= GetAstLandParent(a).Children.IndexOf(b.AstNode);
		}

		public static bool Spatial_Follows(this ConcernPoint a, ConcernPoint b)
		{
			return a.Spatial_IsSiblingOf(b)
				&& GetAstLandParent(a).Children.IndexOf(a.AstNode) >= GetAstLandParent(a).Children.IndexOf(b.AstNode);
		}

		public static bool Spatial_Includes(this ConcernPoint a, ConcernPoint b)
		{
			return a.Location.Includes(b.Location);
		}

		public static bool Spatial_Includes(this Concern a, ConcernPoint b)
		{
			return GetLinearSequenceVisitor.GetPoints(new List<Concern> { a })
				.Any(p => p.Spatial_Includes(b));
		}

		public static bool Spatial_Includes(this Concern a, Concern b)
		{
			var aPoints = GetLinearSequenceVisitor.GetPoints(new List<Concern> { a });

			return GetLinearSequenceVisitor.GetPoints(new List<Concern> { b })
				.All(bPoint => aPoints.Any(aPoint => aPoint.Spatial_Includes(bPoint)));
		}

		public static bool Spatial_Intersects(this ConcernPoint a, ConcernPoint b)
		{
			return a.Location.Overlaps(b.Location);
		}

		public static bool Spatial_Intersects(this Concern a, Concern b)
		{
			var aPoints = GetLinearSequenceVisitor.GetPoints(new List<Concern> { a });

			return GetLinearSequenceVisitor.GetPoints(new List<Concern> { b })
				.All(bPoint => aPoints.Any(aPoint => aPoint.Spatial_Intersects(bPoint)));
		}

		private static Node GetAstLandParent(ConcernPoint p)
		{
			var currentNode = p.AstNode;

			while (!(currentNode.Parent?.Options.IsLand ?? true))
				currentNode = currentNode.Parent;

			return currentNode.Parent;
		}

		#endregion

		#region Теоретико-множественные отношения между функциональностями и точками

		public static bool Set_Includes(this Concern a, ConcernPoint b)
		{
			return GetLinearSequenceVisitor.GetPoints(new List<Concern> { a })
				.Any(p=>p.AstNode == b.AstNode);
		}

		public static bool Set_Includes(this Concern a, Concern b)
		{
			var aNodes = new HashSet<Node>(
				GetLinearSequenceVisitor.GetPoints(new List<Concern> { a }).Select(p => p.AstNode)
			);

			var bNodes = new HashSet<Node>(
				GetLinearSequenceVisitor.GetPoints(new List<Concern> { b }).Select(p => p.AstNode)
			);

			return bNodes.IsSubsetOf(aNodes);
		}

		public static bool Set_Intersects(this Concern a, Concern b)
		{
			var aNodes = new HashSet<Node>(
				GetLinearSequenceVisitor.GetPoints(new List<Concern> { a }).Select(p => p.AstNode)
			);

			var bNodes = new HashSet<Node>(
				GetLinearSequenceVisitor.GetPoints(new List<Concern> { b }).Select(p => p.AstNode)
			);

			return aNodes.Intersect(bNodes).Count() > 0;
		}

		#endregion
	}
}

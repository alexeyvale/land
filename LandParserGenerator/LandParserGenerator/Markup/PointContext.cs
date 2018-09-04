using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[DataContract(IsReference = true)]
	public class InnerContextElement
	{
		[DataMember]
		public double Priority { get; set; }

		[DataMember]
		public string Type { get; set; }

		[DataMember]
		public List<string> Value { get; set; }

		public static explicit operator InnerContextElement(Node node)
		{
			return new InnerContextElement()
			{
				Type = node.Symbol,
				Value = node.Value,
				Priority = node.Options.Priority.Value
			};
		}
	}

	[DataContract(IsReference = true)]
	public class OuterContextElement
	{
		[DataMember]
		public string Type { get; set; }

		[DataMember]
		public List<InnerContextElement> ChildrenContext { get; set; }

		public static explicit operator OuterContextElement(Node node)
		{
			return new OuterContextElement()
			{
				Type = node.Symbol,
				ChildrenContext = node.Children.Select(c => (InnerContextElement)c).ToList()
		};
		}
	}

	[DataContract(IsReference = true)]
	public class PointContext
	{
		public string FileName { get; set; }

		public string NodeType { get; set; }

		/// <summary>
		/// Контекст потомков узла, к которому привязана точка разметки
		/// </summary>
		public List<InnerContextElement> ChildrenContext { get; set; }

		/// <summary>
		/// Контекст предков узла, к которому привязана точка разметки
		/// </summary>
		public List<OuterContextElement> AncestorsContext { get; set; }

		/// <summary>
		/// Контекст уровня, на котором находится узел, к которому привязана точка разметки
		/// </summary>
		public List<OuterContextElement> SiblingsContext { get; set; }

		public PointContext(Node node, string fileName)
		{
			FileName = fileName;
			NodeType = node.Type;

			ChildrenContext = node.Children.Select(c => (InnerContextElement)c).ToList();

			AncestorsContext = new List<OuterContextElement>();
			SiblingsContext = new List<OuterContextElement>();

			var tmpParent = node.Parent;

			if(tmpParent != null)
			{
				SiblingsContext = tmpParent.Children.Select(c=>(OuterContextElement)c).ToList();
			}

			while(tmpParent != null)
			{
				AncestorsContext.Add((OuterContextElement)tmpParent);
				tmpParent = tmpParent.Parent;
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace LandParserGenerator.Parsing.Tree
{
	[DataContract(IsReference = true)]
	public class Node
	{
		/// <summary>
		/// Идентификатор узла, уникальный в пределах одного дерева
		/// </summary>
		[DataMember]
		public int Id { get; set; }

		/// <summary>
		/// Родительский узел
		/// </summary
		[DataMember]
		public Node Parent { get; set; }

		/// <summary>
		/// Символ грамматики, которому соответствует узел
		/// </summary>
		[DataMember]
		public string Symbol { get; set; }

		/// <summary>
		/// Набор токенов, соответствующих листовому узлу
		/// </summary>
		[DataMember]
		public List<string> Value { get; set; } = new List<string>();

		/// <summary>
		/// Потомки узла
		/// </summary>
		[DataMember]
		public List<Node> Children { get; set; } = new List<Node>();

		/// <summary>
		/// Опции, связанные с построением дерева и отображением деревьев
		/// </summary>
		[DataMember]
		public LocalOptions Options { get; set; }

		[DataMember]
		protected Location Anchor { get; set; }

		[DataMember]
		protected bool AnchorReady { get; set; }

		public int? StartOffset
		{
			get
			{
				if (Anchor == null && !AnchorReady)
					GetAnchorFromChildren();
				return Anchor?.StartOffset;
			}
		}
		public int? EndOffset
		{
			get
			{
				if (Anchor == null && !AnchorReady)
					GetAnchorFromChildren();
				return Anchor?.EndOffset;
			}
		}

		protected void GetAnchorFromChildren()
		{
			if (Children.Count > 0)
			{
				Anchor = Children[0].Anchor;

				foreach (var child in Children)
				{
					if (child.Anchor == null)
						child.GetAnchorFromChildren();

					if (Anchor == null)
						Anchor = child.Anchor;
					else
						Anchor = Anchor.Merge(child.Anchor);
				}
			}

			AnchorReady = true;
		}

		public Node(string smb, LocalOptions opts = null)
		{
			Symbol = smb;
			Options = opts ?? new LocalOptions();
		}

		public void AddLastChild(Node child)
		{
			Children.Add(child);
			child.Parent = this;
		}

		public void AddFirstChild(Node child)
		{
			Children.Insert(0, child);
			child.Parent = this;
		}

		public void ResetChildren()
		{
			Children = new List<Node>();
		}

		public void SetAnchor(int start, int end)
		{
			AnchorReady = true;

			Anchor = new Location()
			{
				StartOffset = start,
				EndOffset = end
			};
		}

		public void SetValue(params string[] vals)
		{
			Value = new List<string>(vals);
		}

		public void Accept(BaseVisitor visitor)
		{
			visitor.Visit(this);
		}

		public override string ToString()
		{
			return Symbol + (Value.Count > 0 ? ": " + String.Join(" ", Value) : "");
		}
	}
}

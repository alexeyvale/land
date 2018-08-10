using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace Land.Core.Parsing.Tree
{
	[DataContract(IsReference = true)]
	public class Node
	{
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
		/// Псевдоним символа, которому соответствует узел
		/// в соответствии с подструктурой
		/// </summary>
		[DataMember]
		public string Alias { get; set; }

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

		protected Location Anchor { get; set; }

		public int? StartOffset
		{
			get
			{
				if (Anchor == null)
					GetAnchorFromChildren();
				return Anchor?.StartOffset;
			}
		}
		public int? EndOffset
		{
			get
			{
				if (Anchor == null)
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
		}

		/// <summary>
		/// Возвращает текст токенов из области, 
		/// соответствующей данному узлу
		/// </summary>
		public List<string> GetValue()
		{
			if (Value.Count > 0)
				return new List<string>(Value);

			return Children.SelectMany(c => c.GetValue()).ToList();
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
			Anchor = null;
		}

		public void AddFirstChild(Node child)
		{
			Children.Insert(0, child);
			child.Parent = this;
			Anchor = null;
		}

		public void ResetChildren()
		{
			Children = new List<Node>();
			Anchor = null;
		}

		public void Reset()
		{
			ResetChildren();
			Value.Clear();
		}

		public void SetAnchor(int start, int end)
		{
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
			return (String.IsNullOrEmpty(Alias) ? Symbol : Alias) 
				+ (Value.Count > 0 ? ": " + String.Join(" ", Value) : "");
		}
	}
}

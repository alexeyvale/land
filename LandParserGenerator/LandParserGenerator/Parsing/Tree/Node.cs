using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core.Parsing.Tree
{
	public class Node
	{
		/// <summary>
		/// Родительский узел
		/// </summary
		public Node Parent { get; set; }

		/// <summary>
		/// Символ грамматики, которому соответствует узел
		/// </summary>
		public string Symbol { get; set; }

		/// <summary>
		/// Псевдоним символа, которому соответствует узел
		/// в соответствии с подструктурой
		/// </summary>
		public string Alias { get; set; }

		/// <summary>
		/// Набор токенов, соответствующих листовому узлу
		/// </summary>
		public List<string> Value { get; set; } = new List<string>();

		/// <summary>
		/// Потомки узла
		/// </summary>
		public List<Node> Children { get; set; } = new List<Node>();

		/// <summary>
		/// Опции, связанные с построением дерева и отображением деревьев
		/// </summary>
		public LocalOptions Options { get; set; }

		public string Type => !String.IsNullOrEmpty(Alias) ? Alias : Symbol;

		protected SegmentLocation _location;
		private bool LocationReady { get; set; } = false;
		public SegmentLocation Location
		{
			get
			{
				if (!LocationReady)
					GetAnchorFromChildren();
				return _location;
			}
		}

		public Node(string symbol, LocalOptions opts = null)
		{
			Symbol = symbol;
			Options = opts ?? new LocalOptions();
		}

		public Node(Node node)
		{
			Symbol = node.Symbol;
			Options = node.Options;
			Parent = node.Parent;
			Alias = node.Alias;
			Children = node.Children;
			Value = node.Value;

			_location = node._location;
			LocationReady = node.LocationReady;
		}

		public void CopyFromNode(Node node)
		{
			this.Symbol = node.Symbol;
			this.Options = node.Options;
			this.Parent = node.Parent;
			this.Alias = node.Alias;
			this.Children = node.Children;
			this.Value = node.Value;

			this._location = node._location;
			this.LocationReady = node.LocationReady;
		}

		protected void GetAnchorFromChildren()
		{
			if (Children.Count > 0)
			{
				_location = Children[0].Location;

				foreach (var child in Children)
				{
					if (child.Location == null)
						child.GetAnchorFromChildren();

					if (_location == null)
						_location = child.Location;
					else
						_location = _location.SmartMerge(child.Location);
				}
			}

			LocationReady = true;
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

		public void AddLastChild(Node child)
		{
			Children.Add(child);
			child.Parent = this;
			ResetAnchor();
		}

		public void AddFirstChild(Node child)
		{
			Children.Insert(0, child);
			child.Parent = this;
			ResetAnchor();
		}

		public void ResetChildren()
		{
			Children = new List<Node>();
			ResetAnchor();
		}

		public void ResetAnchor()
		{
			_location = null;
			LocationReady = false;
		}

		public void Reset()
		{
			ResetChildren();
			Value.Clear();
		}

		public void SetAnchor(PointLocation start, PointLocation end)
		{
			_location = new SegmentLocation()
			{
				Start = start,
				End = end
			};

			LocationReady = true;
		}

		public void SetValue(params string[] vals)
		{
			Value = new List<string>(vals);
		}

		public virtual void Accept(BaseTreeVisitor visitor)
		{
			visitor.Visit(this);
		}

		public override string ToString()
		{
			return (String.IsNullOrEmpty(Alias) ? Symbol : Alias) 
				+ (Value.Count > 0 ? ": " + String.Join(" ", Value.Select(v=>v.Trim())) : "");
		}
	}
}

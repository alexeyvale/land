using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;

namespace Land.Core.Parsing.Tree
{
	public class Node: MarshalByRefObject
	{
		/// <summary>
		/// Родительский узел
		/// </summary
		public Node Parent { get; set; }

		/// <summary>
		/// Символ грамматики, которому соответствует узел
		/// </summary>
		public string Symbol { get; set; }

		public string UserifiedSymbol { get; set; }

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
		/// Опции, связанные с конкретным вхождением в грамматику символа,
		/// породившего данный узел
		/// </summary>
		public SymbolOptionsManager Options { get; set; }

		public SymbolArguments Arguments { get; set; }

		public string Type => !String.IsNullOrEmpty(Alias) ? Alias : UserifiedSymbol ?? Symbol;

		protected SegmentLocation _location;
		private bool LocationReady { get; set; } = false;
		public SegmentLocation Location
		{
			get
			{
				if (!LocationReady)
					GetLocationFromChildren();
				return _location;
			}
		}

		public Node(string symbol, SymbolOptionsManager opts = null)
		{
			Symbol = symbol;
			Options = opts ?? new SymbolOptionsManager();
		}

		public Node(Node node)
		{
			Symbol = node.Symbol;
			UserifiedSymbol = node.UserifiedSymbol;
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
			this.UserifiedSymbol = node.UserifiedSymbol;
			this.Options = node.Options;
			this.Parent = node.Parent;
			this.Alias = node.Alias;
			this.Children = node.Children;
			this.Value = node.Value;

			this._location = node._location;
			this.LocationReady = node.LocationReady;
		}

		protected void GetLocationFromChildren()
		{
			if (Children.Count > 0)
			{
				_location = Children[0].Location;

				foreach (var child in Children)
				{
					if (child.Location == null)
						child.GetLocationFromChildren();

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

		public void AddLastChild(Node child, bool mergeLocation = false)
		{
			Children.Add(child);
			child.Parent = this;

			if (mergeLocation)
			{
				_location = Location != null
					? Location.SmartMerge(child.Location)
					: child.Location;
			}
			else
			{
				ResetLocation();
			}
		}

		public void ReplaceChild(Node child, int position, bool mergeLocation = false)
		{
			if (position <= Children.Count)
			{
				Children.RemoveAt(position);
				InsertChild(child, position, mergeLocation);
			}
		}

		public void InsertChild(Node child, int position, bool mergeLocation = false)
		{
			if (position <= Children.Count)
			{
				if (position == Children.Count)
					AddLastChild(child);
				else
				{
					Children.Insert(position, child);
					child.Parent = this;

					if (mergeLocation)
					{
						_location = Location != null
							? Location.SmartMerge(child.Location)
							: child.Location;
					}
					else
					{
						ResetLocation();
					}
				}
			}
		}

		public void AddFirstChild(Node child, bool mergeLocation = false)
		{
			Children.Insert(0, child);
			child.Parent = this;

			if (mergeLocation)
			{
				_location = Location != null
					? Location.SmartMerge(child.Location)
					: child.Location;
			}
			else
			{
				ResetLocation();
			}
		}

		public void ResetChildren()
		{
			Children = new List<Node>();
			ResetLocation();
		}

		public void ResetLocation()
		{
			_location = null;
			LocationReady = false;
		}

		public void Reset()
		{
			ResetChildren();
			Value.Clear();
		}

		public void SetLocation(PointLocation start, PointLocation end)
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
			return (String.IsNullOrEmpty(Alias) ? UserifiedSymbol ?? Symbol : Alias) 
				+ (Value.Count > 0 ? ": " + String.Join(" ", Value.Select(v=>v.Trim())) : "");
		}

		public override object InitializeLifetimeService() => null;
	}
}

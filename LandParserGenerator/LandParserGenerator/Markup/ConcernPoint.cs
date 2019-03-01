using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.Serialization;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[DataContract(IsReference = true)]
	public class ConcernPoint: MarkupElement, INotifyPropertyChanged
	{
		#region Anchor

		private AnchorPoint _anchor;

		[DataMember]
		public AnchorPoint Anchor
		{
			get
			{
				return _anchor;
			}
			set
			{
				if (_anchor != null)
				{
					_anchor.PropertyChanged -= AnchorPropertyChanged;
					_anchor.Links.Remove(this);
				}

				_anchor = value;

				if (value != null)
				{
					_anchor.PropertyChanged += AnchorPropertyChanged;
					_anchor.Links.Add(this);
				}
			}
		}

		/// <summary>
		/// Признак того, что координаты невозможно использовать для перехода
		/// </summary>
		public bool HasInvalidLocation => Anchor.HasIrrelevantLocation || Anchor.HasMissingLocation;

		/// <summary>
		/// Имя файла, с которым связана точка
		/// </summary>
		public string FileName => Anchor.Context.FileName;

		/// <summary>
		/// Тип узла дерева, с которым связана точка
		/// </summary>
		public string Type => Anchor.Context.NodeType;

		/// <summary>
		/// Координаты участка в тексте, которому соответствует точка 
		/// </summary>
		public SegmentLocation Location => Anchor.Location;

		#endregion

		#region PropertyChanged

		public new event PropertyChangedEventHandler PropertyChanged;

		public void ParentPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(sender, e);
		}

		public void AnchorPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(sender, e);
		}

		#endregion

		public ConcernPoint(AnchorPoint anchor, Concern parent = null)
		{
			Anchor = anchor;

			Parent = parent;
			Name = anchor.AstNode.Type;

			if (anchor.AstNode.Value.Count > 0)
				Name += ": " + String.Join(" ", anchor.AstNode.Value);
			else
			{
				if (anchor.AstNode.Children.Count > 0)
				{
					Name += ": " + String.Join(" ", anchor.AstNode.Children.SelectMany(c => c.Value.Count > 0 ? c.Value
						: new List<string>() { '"' + (String.IsNullOrEmpty(c.Alias) ? c.Symbol : c.Alias) + '"' }));
				}
			}

			base.PropertyChanged += ParentPropertyChanged;
		}

		public ConcernPoint(string name, AnchorPoint anchor, Concern parent = null)
		{
			Anchor = anchor;

			Name = name;
			Parent = parent;

			base.PropertyChanged += ParentPropertyChanged;
		}

		public ConcernPoint(string name, string comment, AnchorPoint anchor, Concern parent = null)
			: this(name, anchor, parent)
		{
			Comment = comment;
		}

		public override void Accept(BaseMarkupVisitor visitor)
		{
			visitor.Visit(this);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using Newtonsoft.Json;
using Land.Core;
using Land.Core.Specification;
using Land.Core.Parsing.Tree;
using Land.Markup.Tree;
using Land.Markup.Binding;
using Land.Markup.CoreExtension;

namespace Land.Markup
{
	public class ConcernPoint: MarkupElement, INotifyPropertyChanged
	{
		private PointContext _context;

		[JsonIgnore]
		public PointContext Context
		{
			get { return _context; }

			set
			{
				_context = value;
				_context.LinkPoint(Id);
			}
		}

		/// <summary>
		/// Признак того, что координаты, хранимые точкой, не соответствуют тексту
		/// </summary>
		[JsonIgnore]
		public bool HasIrrelevantLocation { get; set; }

		/// <summary>
		/// Признак того, что координаты потеряны
		/// </summary>
		[JsonIgnore]
		public bool HasMissingLocation => Location == null;

		/// <summary>
		/// Признак того, что координаты невозможно использовать для перехода
		/// </summary>
		[JsonIgnore]
		public bool HasInvalidLocation => HasIrrelevantLocation || HasMissingLocation;

		/// <summary>
		/// Узел AST, которому соответствует точка
		/// </summary>
		private Node _node;

		[JsonIgnore]
		public Node AstNode
		{
			get => _node;
			set
			{
				_node = value;
				HasIrrelevantLocation = false;

				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Location"));
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("HasMissingLocation"));
			}
		}

		/// <summary>
		/// Координаты участка в тексте, которому соответствует точка 
		/// </summary>
		[JsonIgnore]
		public SegmentLocation Location => _node?.Location;

		public new event PropertyChangedEventHandler PropertyChanged;

		public void ParentPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(sender, e);
		}

		public ConcernPoint() { }

		public ConcernPoint(Node node, PointContext context, Concern parent = null)
		{
			Context = context;
			AstNode = node;
			Parent = parent;
			Name = node.Type;

			if (node.Value.Count > 0)
				Name += ": " + String.Join(" ", node.Value);
			else
			{
				if (node.Children.Count > 0)
				{
					Name += ": " + String.Join(" ", node.Children.SelectMany(c => c.Value.Count > 0 ? c.Value
						: new List<string>() { '"' + (String.IsNullOrEmpty(c.Alias) ? c.Symbol : c.Alias) + '"' }));
				}
			}

			base.PropertyChanged += ParentPropertyChanged;
		}

		public ConcernPoint(string name, string comment, Node node, PointContext context, Concern parent = null)
		{
			Context = context;
			AstNode = node;
			Parent = parent;
			Name = name;
			Comment = comment;

			base.PropertyChanged += ParentPropertyChanged;
		}

		public void Relink(Node node, PointContext context)
		{
			AstNode = node;
			Context = context;
		}

		public void Relink(RemapCandidateInfo candidate)
		{
			AstNode = candidate.Node;
			Context = candidate.Context;
		}

		public override void Accept(BaseMarkupVisitor visitor)
		{
			visitor.Visit(this);
		}
	}

	public class ParsedFile
	{
		/// <summary>
		/// Текст файла
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// Корень АСД для данного файла
		/// </summary>
		public Node Root { get; set; }

		/// <summary>
		/// Имя файла
		/// </summary>
		public string Name { get; set; }
	}
}

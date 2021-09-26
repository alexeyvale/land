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

		private LineContext _lineContext;

		[JsonIgnore]
		public LineContext LineContext
		{
			get { return _lineContext; }

			set
			{
				_lineContext = value;

				if (value != null)
				{
					_lineContext.LinkPoint(Id);
				}
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
		public bool HasMissingLocation => LineContext != null && _lineLocation == null || _nodeLocation == null;

		/// <summary>
		/// Признак того, что координаты невозможно использовать для перехода
		/// </summary>
		[JsonIgnore]
		public bool HasInvalidLocation => HasIrrelevantLocation || HasMissingLocation;


		private SegmentLocation _nodeLocation;

		[JsonIgnore]
		public SegmentLocation NodeLocation
		{
			get => _nodeLocation;
			set
			{
				_nodeLocation = value;
				HasIrrelevantLocation = false;

				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Location"));
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("HasMissingLocation"));
			}
		}

		private SegmentLocation _lineLocation;

		[JsonIgnore]
		public SegmentLocation LineLocation
		{
			get => _lineLocation;
			set
			{
				_lineLocation = value;
				HasIrrelevantLocation = false;

				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Location"));
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("HasMissingLocation"));
			}
		}

		public new event PropertyChangedEventHandler PropertyChanged;

		public void ParentPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(sender, e);
		}

		public ConcernPoint() { }

		public ConcernPoint(
			string name, 
			string comment, 
			PointContext context,
			SegmentLocation location,
			LineContext line,
			SegmentLocation lineLocation,
			Concern parent = null)
		{
			Context = context;
			LineContext = line;
			NodeLocation = location;
			LineLocation = lineLocation;
			Parent = parent;
			Name = name;
			Comment = comment;

			base.PropertyChanged += ParentPropertyChanged;
		}

		public void Relink(
			PointContext context, 
			SegmentLocation location, 
			LineContext lineContext, 
			SegmentLocation lineLocation)
		{
			NodeLocation = location;
			LineLocation = lineLocation;
			Context = context;
			LineContext = lineContext;
		}

		public void Relink(RemapCandidateInfo candidate)
		{
			NodeLocation = candidate.Node.Location;
			Context = candidate.Context;
		}

		public override void Accept(BaseMarkupVisitor visitor)
		{
			visitor.Visit(this);
		}

		public static string GetDefaultName(Node node)
		{
			var name = node.Type;

			if (node.Value.Count > 0)
			{
				name += ": " + String.Join(" ", node.Value);
			}
			else
			{
				if (node.Children.Count > 0)
				{
					name += ": " + String.Join(" ", node.Children.SelectMany(c => c.Value.Count > 0 ? c.Value
						: new List<string>() { '"' + (String.IsNullOrEmpty(c.Alias) ? c.Symbol : c.Alias) + '"' }));
				}
			}

			return name;
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

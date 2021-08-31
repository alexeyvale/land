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
		public bool HasMissingLocation => Location == null;

		/// <summary>
		/// Признак того, что координаты невозможно использовать для перехода
		/// </summary>
		[JsonIgnore]
		public bool HasInvalidLocation => HasIrrelevantLocation || HasMissingLocation;


		private SegmentLocation _location;

		[JsonIgnore]
		public SegmentLocation Location
		{
			get => _location;
			set
			{
				_location = value;
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

		public ConcernPoint(string name, string comment, SegmentLocation location, PointContext context, LineContext line, Concern parent = null)
		{
			Context = context;
			LineContext = line;
			Location = location;
			Parent = parent;
			Name = name;
			Comment = comment;

			base.PropertyChanged += ParentPropertyChanged;
		}

		public void Relink(SegmentLocation location, PointContext context, LineContext line)
		{
			Location = location;
			Context = context;
			LineContext = line;
		}

		public void Relink(RemapCandidateInfo candidate)
		{
			Location = candidate.Node.Location;
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

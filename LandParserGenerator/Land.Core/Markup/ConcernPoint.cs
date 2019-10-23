using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.Serialization;
using Land.Core;
using Land.Core.Specification;
using Land.Core.Parsing.Tree;
using Land.Markup.Tree;
using Land.Markup.Binding;
using Land.Markup.CoreExtension;

namespace Land.Markup
{
	[DataContract]
	public class ConcernPoint: MarkupElement, INotifyPropertyChanged
	{
		[DataMember]
		public PointContext Context { get; set; }

		/// <summary>
		/// Признак того, что координаты, хранимые точкой, не соответствуют тексту
		/// </summary>
		public bool HasIrrelevantLocation { get; set; }

		/// <summary>
		/// Признак того, что координаты потеряны
		/// </summary>
		public bool HasMissingLocation => Location == null;

		/// <summary>
		/// Признак того, что координаты невозможно использовать для перехода
		/// </summary>
		public bool HasInvalidLocation => HasIrrelevantLocation || HasMissingLocation;

		/// <summary>
		/// Узел AST, которому соответствует точка
		/// </summary>
		private Node _node;
		public Node AstNode
		{
			get => _node;
			set
			{
				_node = value;
				HasIrrelevantLocation = false;

				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Location"));
			}
		}

		/// <summary>
		/// Координаты участка в тексте, которому соответствует точка 
		/// </summary>
		public SegmentLocation Location => _node?.Location;

		public new event PropertyChangedEventHandler PropertyChanged;

		public void ParentPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(sender, e);
		}

		public ConcernPoint() { }

		public ConcernPoint(ParsedFile targetInfo, Concern parent = null)
		{
			Context = PointContext.Create(targetInfo);

			AstNode = targetInfo.Root;
			Parent = parent;
			Name = targetInfo.Root.Type;

			if (targetInfo.Root.Value.Count > 0)
				Name += ": " + String.Join(" ", targetInfo.Root.Value);
			else
			{
				if (targetInfo.Root.Children.Count > 0)
				{
					Name += ": " + String.Join(" ", targetInfo.Root.Children.SelectMany(c => c.Value.Count > 0 ? c.Value
						: new List<string>() { '"' + (String.IsNullOrEmpty(c.Alias) ? c.Symbol : c.Alias) + '"' }));
				}
			}

			base.PropertyChanged += ParentPropertyChanged;
		}

		public ConcernPoint(string name, ParsedFile targetInfo, Concern parent = null)
		{
			Name = name;
			Context = PointContext.Create(targetInfo);
			AstNode = targetInfo.Root;
			Parent = parent;

			base.PropertyChanged += ParentPropertyChanged;
		}

		public ConcernPoint(string name, string comment, ParsedFile targetInfo, Concern parent = null)
			: this(name, targetInfo, parent)
		{
			Comment = comment;
		}

		public void Relink(ParsedFile targetInfo)
		{
			AstNode = targetInfo.Root;
			Context = PointContext.Create(targetInfo);
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
		public string Name { get; set; }
		public string Text { get; set; }
		public Node Root { get; set; }
		public LanguageMarkupSettings MarkupSettings { get; set; }
	}

	public class LanguageMarkupSettings : MarshalByRefObject
	{
		public bool UseHorizontalContext { get; private set; } = false;

		public LanguageMarkupSettings(SymbolOptionsManager opts)
		{
			if (opts != null)
			{
				UseHorizontalContext = opts.IsSet(MarkupOption.USEHORIZONTAAL);
			}
		}

		public override object InitializeLifetimeService() => null;
	}
}

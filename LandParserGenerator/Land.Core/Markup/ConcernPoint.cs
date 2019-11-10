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

		public ConcernPoint(Node node, ParsedFile file, Concern parent = null)
		{
			Context = new PointContext(node, file);
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

		public ConcernPoint(string name, Node node, ParsedFile file, Concern parent = null)
		{
			Context = new PointContext(node, file);
			AstNode = node;
			Parent = parent;
			Name = name;

			base.PropertyChanged += ParentPropertyChanged;
		}

		public ConcernPoint(string name, Node node, string comment, ParsedFile targetInfo, Concern parent = null)
			: this(name, node, targetInfo, parent)
		{
			Comment = comment;
		}

		public void Relink(Node node, ParsedFile targetInfo)
		{
			AstNode = targetInfo.Root;
			Context = new PointContext(node, targetInfo);
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
		/// Описывающий файл контекст, используемый при перепривязке
		/// </summary>
		public FileContext BindingContext { get; set; }

		/// <summary>
		/// Специфичные для языка настройки перепривязки
		/// </summary>
		public LanguageMarkupSettings MarkupSettings { get; set; }

		/// <summary>
		/// Имя файла
		/// </summary>
		public string Name => BindingContext?.Name;
	}

	[Serializable]
	public class LanguageMarkupSettings
	{
		public bool UseHorizontalContext { get; private set; } = false;

		public LanguageMarkupSettings(SymbolOptionsManager opts)
		{
			if (opts != null)
			{
				UseHorizontalContext = opts.IsSet(MarkupOption.USEHORIZONTAAL);
			}
		}
	}
}

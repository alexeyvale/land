using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.Serialization;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[DataContract(IsReference = true)]
	public class AnchorPoint: INotifyPropertyChanged
	{
		[DataMember]
		public PointContext Context { get; set; }

		[DataMember]
		public HashSet<ConcernPoint> Links { get; set; } = new HashSet<ConcernPoint>();

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

		public event PropertyChangedEventHandler PropertyChanged;

		public AnchorPoint(PointContext context, Node astNode)
		{
			Context = context;
			AstNode = astNode;
		}
	}

	public class TargetFileInfo
	{
		public string FileName { get; set; }
		public string FileText { get; set; }
		public Node TargetNode { get; set; }
	}
}

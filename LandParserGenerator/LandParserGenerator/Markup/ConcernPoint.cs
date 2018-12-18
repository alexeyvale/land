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
		[DataMember]
		public PointContext Context { get; set; }

		/// <summary>
		/// Признак того, что координаты, хранимые точкой, не соответствуют тексту
		/// </summary>
		public bool HasIrrelevantLocation { get; set; }

		/// <summary>
		/// Признак того, что координаты потеряны
		/// </summary>
		public bool HasMissingLocation => _location == null;

		/// <summary>
		/// Признак того, что координаты невозможно использовать для перехода
		/// </summary>
		public bool HasInvalidLocation => HasIrrelevantLocation || HasMissingLocation;

		/// <summary>
		/// Координаты участка в тексте, которому соответствует точка 
		/// </summary>
		private SegmentLocation _location;
		public SegmentLocation Location
		{
			get => _location;
			set
			{
				_location = value;
				HasIrrelevantLocation = false;

				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Location"));
			}
		}

		public new event PropertyChangedEventHandler PropertyChanged;

		public ConcernPoint(TargetFileInfo targetInfo, Concern parent = null)
		{
			Context = PointContext.Create(targetInfo);

			Location = targetInfo.TargetNode.Anchor;
			Parent = parent;
			Name = targetInfo.TargetNode.Type;

			if (targetInfo.TargetNode.Value.Count > 0)
				Name += ": " + String.Join(" ", targetInfo.TargetNode.Value);
			else
			{
				if (targetInfo.TargetNode.Children.Count > 0)
				{
					Name += ": " + String.Join(" ", targetInfo.TargetNode.Children.SelectMany(c => c.Value.Count > 0 ? c.Value
						: new List<string>() { '"' + (String.IsNullOrEmpty(c.Alias) ? c.Symbol : c.Alias) + '"' }));
				}
			}
		}

		public ConcernPoint(string name, TargetFileInfo targetInfo, Concern parent = null)
		{
			Name = name;
			Context = PointContext.Create(targetInfo);
			Parent = parent;
		}

		public ConcernPoint(string name, string comment, TargetFileInfo targetInfo, Concern parent = null)
			: this(name, targetInfo, parent)
		{
			Comment = comment;
		}

		public void Relink(TargetFileInfo targetInfo)
		{
			Location = targetInfo.TargetNode.Anchor;
			Context = PointContext.Create(targetInfo);
		}

		public override void Accept(BaseMarkupVisitor visitor)
		{
			visitor.Visit(this);
		}
	}

	public class TargetFileInfo
	{
		public string FileName { get; set; }
		public string FileText { get; set; }
		public Node TargetNode { get; set; }
	}
}

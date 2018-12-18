using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.Serialization;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[DataContract(IsReference = true)]
	public class Concern: MarkupElement
	{
		[DataMember]
		public ObservableCollection<MarkupElement> Elements { get; set; }

		public new event PropertyChangedEventHandler PropertyChanged;

		public void ParentPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(sender, e);
		}

		public Concern(string name, Concern parent = null)
		{
			Name = name;
			Parent = parent;
			Elements = new ObservableCollection<MarkupElement>();

			base.PropertyChanged += ParentPropertyChanged;
		}

		public Concern(string name, string comment, Concern parent = null)
			: this(name, parent)
		{
			Comment = comment;
		}

		public override void Accept(BaseMarkupVisitor visitor)
		{
			visitor.Visit(this);
		}
	}
}

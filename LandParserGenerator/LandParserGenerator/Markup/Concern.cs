using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[DataContract(IsReference = true)]
	public class Concern: MarkupElement
	{
		[DataMember]
		public ObservableCollection<MarkupElement> Elements { get; set; }

		public Concern(string name, Concern parent = null)
		{
			Name = name;
			Parent = parent;
			Elements = new ObservableCollection<MarkupElement>();
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

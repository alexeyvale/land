using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[DataContract(IsReference = true)]
	public abstract class MarkupElement
	{
		[DataMember]
		public string Name { get; set; }

		[DataMember]
		public Concern Parent { get; set; }

		public abstract void Accept(BaseMarkupVisitor visitor);
	}
}

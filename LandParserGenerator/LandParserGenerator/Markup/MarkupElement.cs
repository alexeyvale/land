using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Markup
{
	[DataContract(IsReference = true)]
	public abstract class MarkupElement
	{
		[DataMember]
		public string Name { get; set; }

		[DataMember]
		public Concern Parent { get; set; }
	}
}

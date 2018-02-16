using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Markup
{
	[DataContract]
	public abstract class MarkupElement
	{
		[DataMember]
		public string Name { get; set; }

		public Concern Parent { get; set; }
	}
}

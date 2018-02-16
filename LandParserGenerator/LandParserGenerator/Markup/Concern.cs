using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Markup
{
	[DataContract]
	public class Concern: MarkupElement
	{
		[DataMember]
		public List<MarkupElement> Elements { get; set; }

		public Concern(string name, Concern parent = null)
		{
			Name = name;
			Parent = parent;
			Elements = new List<MarkupElement>();
		}
	}
}

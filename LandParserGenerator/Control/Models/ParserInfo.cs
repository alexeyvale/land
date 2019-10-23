using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using Land.Core.Parsing;
using Land.Markup;

namespace Land.Control
{
	public class ParserInfo
	{
		public AppDomain Domain { get; set; }
		public LanguageMarkupSettings MarkupSettings { get; set; }
		public BaseParser Parser { get; set; }
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Land.Markup.Relations;
using Land.Markup.Tree;

namespace Land.Markup
{
	public struct SerializationUnit
	{
		public ObservableCollection<MarkupElement> Markup { get; set; }

		public List<RelatedPair<MarkupElement>> ExternalRelatons { get; set; }
	}
}

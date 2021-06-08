using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Land.Markup.Relations;
using Land.Markup.Binding;

namespace Land.Markup
{
	public struct AncestorsPointsPair
	{
		public List<AncestorsContextElement> Ancestors { get; set; }
		public List<PointContext> Points { get; set; }
	}

	public struct SerializationUnit
	{
		public ObservableCollection<MarkupElement> Markup { get; set; }

		public List<AncestorsPointsPair> PointContexts { get; set; }

		public List<RelatedPair<MarkupElement>> ExternalRelatons { get; set; }
	}
}

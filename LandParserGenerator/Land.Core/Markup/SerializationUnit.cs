using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;
using Land.Markup.Relations;
using Land.Markup.Tree;

namespace Land.Markup
{
	[DataContract]
	public struct SerializationUnit
	{
		[DataMember]
		public ObservableCollection<MarkupElement> Markup { get; set; }

		[DataMember]
		public List<RelatedPair<MarkupElement>> ExternalRelatons { get; set; }
	}
}

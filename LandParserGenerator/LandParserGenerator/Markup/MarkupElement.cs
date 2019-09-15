using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.Serialization;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[DataContract]
	public abstract class MarkupElement: INotifyPropertyChanged
	{
		private string _name;
		private string _comment;

		[DataMember]
		public string Name {
			get => _name;
			set
			{
				_name = value;

				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name"));
			}
		}

		[DataMember]
		public string Comment
		{
			get => _comment;
			set
			{
				_comment = value;

				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Comment"));
			}
		}

		public Concern Parent { get; set; }

		public event PropertyChangedEventHandler PropertyChanged;

		public abstract void Accept(BaseMarkupVisitor visitor);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using Newtonsoft.Json;
using Land.Markup.Tree;

namespace Land.Markup
{
	[JsonObject] 
	public abstract class MarkupElement: INotifyPropertyChanged
	{
		private string _name;
		private string _comment;

		public string Name {
			get => _name;
			set
			{
				_name = value;

				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name"));
			}
		}

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

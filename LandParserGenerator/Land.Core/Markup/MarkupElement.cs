using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.ComponentModel;
using Land.Markup.Tree;

namespace Land.Markup
{
	public abstract class MarkupElement: INotifyPropertyChanged
	{
		public Guid Id { get; set; } = Guid.NewGuid();

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

		[JsonIgnore]
		public Concern Parent { get; set; }

		public event PropertyChangedEventHandler PropertyChanged;

		public abstract void Accept(BaseMarkupVisitor visitor);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.GUI
{
	public class ColumnSettingsAttribute : System.Attribute
	{
		public ColumnSettingsAttribute(string Name) { this.Name = Name; this.Index = Index; }
		public ColumnSettingsAttribute(string Name, int Index) { this.Name = Name; this.Index = Index; }

		public string Name { get; set; }
		public int? Index { get; set; }
	}
}

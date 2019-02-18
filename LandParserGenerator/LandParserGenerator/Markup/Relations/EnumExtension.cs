using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public static class EnumExtension
	{
		public static T GetAttribute<T>(this Enum enumVal) where T : System.Attribute
		{
			var type = enumVal.GetType();
			var memInfo = type.GetMember(enumVal.ToString());
			var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
			return (attributes.Length > 0) ? (T)attributes[0] : null;
		}

		public static string GetDescription(this Enum enumVal)
		{
			return enumVal.GetAttribute<DescriptionAttribute>().Description;
		}
	}
}

using System;

namespace Preprocessor.Core
{
	[AttributeUsage(AttributeTargets.Property)]
	public class DisplayedNameAttribute : Attribute
	{
		public string Text;

		public DisplayedNameAttribute(string text)
		{
			this.Text = text;
		}
	}

	[AttributeUsage(AttributeTargets.Property)]
	public class ConverterAttribute : Attribute
	{
		public Type ConverterType;

		public ConverterAttribute(Type type)
		{
			this.ConverterType = type;
		}
	}

	[AttributeUsage(AttributeTargets.Property)]
	public class PropertyToSetAttribute : Attribute
	{ }

	public abstract class PropertyConverter
	{
		public abstract string ToString(object val);

		public abstract object ToValue(string str);
	}
}

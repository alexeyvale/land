using System;

namespace Land.Core.Parsing.Preprocessing
{
	[AttributeUsage(AttributeTargets.Property)]
	public class DisplayedName : Attribute
	{
		public string Text;

		public DisplayedName(string text)
		{
			this.Text = text;
		}
	}

	[AttributeUsage(AttributeTargets.Property)]
	public class Converter : Attribute
	{
		public Type ConverterType;

		public Converter(Type type)
		{
			this.ConverterType = type;
		}
	}

    [AttributeUsage(AttributeTargets.Property)]
    public class PropertyToSet : Attribute
    { }

    public abstract class SettingsPropertyConverter
	{
		public abstract string ToString(object val);

		public abstract object ToValue(string str);
	}
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Land.Markup;

namespace Land.Control
{
	public class MarkupTreeIconColorConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			var IsFocused = (bool)values[0];
			var IsSelectionActive = (bool)values[1];

			return IsFocused && IsSelectionActive
				? Brushes.WhiteSmoke
				: Brushes.DimGray;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			return new object[] { };
		}
	}
}

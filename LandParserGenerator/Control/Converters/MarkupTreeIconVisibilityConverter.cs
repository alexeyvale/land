using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using Land.Core.Markup;

namespace Land.Control
{
	public class MarkupTreeIconVisibilityConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			var label = (Label)values[0];
			var markupElement = (MarkupElement)values[1];

			switch (label.Name)
			{
				case "MissingIcon":
					return markupElement is ConcernPoint point1 
						? point1.HasMissingLocation
							? Visibility.Visible : Visibility.Collapsed 
						: Visibility.Collapsed;

				case "PointIcon":
					return markupElement is ConcernPoint point2
						? point2.HasMissingLocation
							? Visibility.Collapsed : Visibility.Visible
						: Visibility.Collapsed;

				case "ConcernIcon":
					return markupElement is Concern
						? Visibility.Visible : Visibility.Collapsed;
			}

			return Visibility.Collapsed;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			return new object[] { };
		}
	}
}

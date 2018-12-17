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
	public class IconVisibilityConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			var label = (Label)values[0];
			var markupElement = (MarkupElement)values[1];

			switch (label.Name)
			{
				case "MissingIcon":
					return markupElement is ConcernPoint point1 
						? point1.Location == null 
							? Visibility.Visible : Visibility.Hidden 
						: Visibility.Hidden;

				case "PointIcon":
					return markupElement is ConcernPoint point2
						? point2.Location == null
							? Visibility.Hidden : Visibility.Visible
						: Visibility.Hidden;

				case "ConcernIcon":
					return markupElement is Concern
						? Visibility.Visible : Visibility.Hidden;
			}

			return Visibility.Hidden;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			return new object[] { };
		}
	}
}

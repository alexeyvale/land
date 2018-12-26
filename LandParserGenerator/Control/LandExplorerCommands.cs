using System.Windows.Input;

namespace Land.Control
{
	public static class LandExplorerCommands
	{
		public static RoutedUICommand AddPoint { get; } = new RoutedUICommand
			(
				"Добавить точку привязки",
				"AddPoint",
				typeof(LandExplorerCommands),
				new InputGestureCollection()
				{
					new KeyGesture(Key.A, ModifierKeys.Control | ModifierKeys.Shift)
				}
			);

		public static RoutedUICommand AddConcern { get; } = new RoutedUICommand
			(
				"Добавить функциональность",
				"AddConcern",
				typeof(LandExplorerCommands),
				new InputGestureCollection()
				{
					new KeyGesture(Key.A, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt)
				}
			);

		public static RoutedUICommand AddLand { get; } = new RoutedUICommand
			(
				"Добавить все острова",
				"AddLand",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Rename { get; } = new RoutedUICommand
			(
				"Переименовать элемент",
				"Rename",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Relink_Markup { get; } = new RoutedUICommand
			(
				"Перепривязать",
				"Relink",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Delete_Missing { get; } = new RoutedUICommand
			(
				"Удалить элемент разметки",
				"Delete",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Relink_Missing { get; } = new RoutedUICommand
			(
				"Перепривязать",
				"Relink",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Accept_Missing { get; } = new RoutedUICommand
			(
				"Подтвердить",
				"Accept",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Delete_Markup { get; } = new RoutedUICommand
			(
				"Удалить элемент разметки",
				"Delete",
				typeof(LandExplorerCommands)
			);


		public static RoutedUICommand Highlight { get; } = new RoutedUICommand
			(
				"Выделить функциональности",
				"Highlight",
				typeof(LandExplorerCommands)
			);
	}
}

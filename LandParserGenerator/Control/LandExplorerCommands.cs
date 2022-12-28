using System.Windows.Input;

namespace Land.Control
{
	public static class LandExplorerCommands
	{
		public static RoutedUICommand SelectPoint { get; } = new RoutedUICommand
			(
				"Добавить точку привязки",
				"SelectPoint",
				typeof(LandExplorerCommands),
				new InputGestureCollection()
				{
					new KeyGesture(Key.A, ModifierKeys.Control | ModifierKeys.Shift)
				}
			);

		public static RoutedUICommand AddPoint { get; } = new RoutedUICommand
			(
				"Быстрое добавление точки привязки",
				"AddPoint",
				typeof(LandExplorerCommands),
				new InputGestureCollection()
				{
					new KeyGesture(Key.A, ModifierKeys.Control | ModifierKeys.Alt)
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
				"Разметить всю сушу",
				"AddLand",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Rename { get; } = new RoutedUICommand
			(
				"Переименовать элемент",
				"Rename",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Highlight { get; } = new RoutedUICommand
			(
				"Выделить функциональности",
				"Highlight",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand OpenConcernGraph { get; } = new RoutedUICommand
			(
				"Открыть граф зависимостей",
				"OpenConcernGraph",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand CollapseAll { get; } = new RoutedUICommand
			(
				"Свернуть всё",
				"CollapseAll",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand DeleteWithSource { get; } = new RoutedUICommand
			(
				"Удалить вместе с исходным кодом",
				"DeleteWithSource",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand TurnOff { get; } = new RoutedUICommand
			(
				"Отключить",
				"TurnOff",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand TurnOn { get; } = new RoutedUICommand
			(
				"Включить",
				"TurnOn",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Delete_Markup { get; } = new RoutedUICommand
			(
				"Удалить",
				"Delete",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand RelinkSame_Markup { get; } = new RoutedUICommand
			(
				"Перепривязаться к объемлющему или вложенному элементу",
				"RelinkSame",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand RelinkCurrent_Markup { get; } = new RoutedUICommand
			(
				"Перепривязаться к текущему месту в коде",
				"RelinkCurrent",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Export{ get; } = new RoutedUICommand
			(
				"Экспортировать...",
				"Export",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Import { get; } = new RoutedUICommand
			(
				"Импортировать...",
				"Import",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Delete_Missing { get; } = new RoutedUICommand
			(
				"Удалить",
				"Delete",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Relink_Missing { get; } = new RoutedUICommand
			(
				"Перепривязаться к текущему месту в коде",
				"Relink",
				typeof(LandExplorerCommands)
			);

		public static RoutedUICommand Accept_Missing { get; } = new RoutedUICommand
			(
				"Подтвердить",
				"Accept",
				typeof(LandExplorerCommands)
			);
	}
}

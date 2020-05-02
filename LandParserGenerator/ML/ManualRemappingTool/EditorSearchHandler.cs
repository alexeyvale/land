using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ManualRemappingTool
{
    public class EditorSearchHandler : TextAreaInputHandler
    {
        public EditorSearchHandler(TextArea textArea) : base(textArea)
        {
            RegisterCommands(this.CommandBindings);
            this.Attach();
        }

        void RegisterCommands(ICollection<CommandBinding> commandBindings)
        {
            commandBindings.Add(new CommandBinding(ApplicationCommands.Find, ExecuteFind));
            commandBindings.Add(new CommandBinding(SearchCommands.FindNext, ExecuteFindNext));
            commandBindings.Add(new CommandBinding(SearchCommands.FindPrevious, ExecuteFindPrevious));
            commandBindings.Add(new CommandBinding(SearchCommands.CloseSearchPanel, ExecuteCloseSearchPanel));
        }

        SearchPanel QuickSearchPanel;

		void ExecuteFind(object sender, ExecutedRoutedEventArgs e)
		{
			var area = (TextArea)sender;

			if (QuickSearchPanel == null || QuickSearchPanel.IsClosed)
			{
				QuickSearchPanel = GenerateSearchPanel(area);
			}

			QuickSearchPanel.SearchPattern = area.Selection.GetText();

			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input,
				(Action)delegate { QuickSearchPanel.Open(); });
			QuickSearchPanel.Dispatcher.BeginInvoke(DispatcherPriority.Input,
				(Action)QuickSearchPanel.Reactivate);
		}

		void ExecuteFindNext(object sender, ExecutedRoutedEventArgs e)
		{
			if (QuickSearchPanel != null)
			{
				QuickSearchPanel.FindNext();
			}
		}

		void ExecuteFindPrevious(object sender, ExecutedRoutedEventArgs e)
		{
			if (QuickSearchPanel != null)
			{
				QuickSearchPanel.FindPrevious();
			}
		}

		void ExecuteCloseSearchPanel(object sender, ExecutedRoutedEventArgs e)
		{
			if (QuickSearchPanel != null)
			{
				QuickSearchPanel.Close();
			}

			QuickSearchPanel = null;
		}

		private SearchPanel GenerateSearchPanel(TextArea area)
		{
			var panel = SearchPanel.Install(area);
			panel.MarkerBrush = new SolidColorBrush(Color.FromRgb(140, 50, 50));

			return panel;
		}
	}
}

using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using System.IO;
using System.Windows.Media;
using System.Runtime.Serialization;

using Land.Core;
using Land.Core.Markup;
using Land.Control;

namespace Land.GUI
{
	public class EditorAdapter : IEditorAdapter
	{
		private MainWindow EditorWindow { get; set; }
		private string SettingsPath { get; set; }
		private Action<string> DocumentSavingCallback { get; set; }

		public EditorAdapter(MainWindow window, string settingsPath)
		{
			EditorWindow = window;
			SettingsPath = settingsPath;
		}

		#region IEditorAdapter

		public string GetActiveDocumentName()
		{
			var activeTab = GetActiveDocumentTab();

			return activeTab != null ? EditorWindow.Documents[activeTab].DocumentName : null;
		}

		public int? GetActiveDocumentOffset()
		{
			var activeTab = GetActiveDocumentTab();

			return activeTab != null ? EditorWindow.Documents[activeTab].Editor.CaretOffset : (int?)null;
		}

		public string GetActiveDocumentText()
		{
			var activeTab = GetActiveDocumentTab();

			return activeTab != null ? EditorWindow.Documents[activeTab].Editor.Text : null;
		}

		public string GetDocumentText(string documentName)
		{
			return EditorWindow.Documents.Where(d => d.Value.DocumentName == documentName)
				.Select(d => d.Value.Editor.Text).FirstOrDefault();
		}

		public bool HasActiveDocument()
		{
			return EditorWindow.Documents.Count > 0;
		}

		public void ProcessMessages(List<Message> messages, bool skipTrace, bool resetPrevious)
		{
			IEnumerable<Message> toProcess = skipTrace 
				? messages.Where(m => m.Type != MessageType.Trace) : messages;

			if (resetPrevious)
			{
				EditorWindow.MarkupTestErrors.Items.Clear();
				EditorWindow.MarkupTestLog.Items.Clear();
			}

			foreach (var msg in toProcess)
			{
				ProcessMessage(msg);
			}
		}

		public void ProcessMessage(Message msg)
		{
			EditorWindow.MarkupTestLog.Items.Add(msg);

			if (msg.Type == MessageType.Error || msg.Type == MessageType.Warning)
			{
				EditorWindow.MarkupTestErrors.Items.Add(msg);
				EditorWindow.MarkupTestOutputTabs.SelectedItem = EditorWindow.MarkupTestErrorsTab;
			}
		}

		public void SetActiveDocumentAndOffset(string documentName, PointLocation location)
		{
			/// Получаем вкладку для заданного имени файла
			var newActive = EditorWindow.Documents
				.Where(d => d.Value.DocumentName == documentName)
				.Select(d => d.Key).FirstOrDefault();

			/// Фокусируемся на ней, если получили
			if (newActive != null)
				EditorWindow.DocumentTabs.SelectedItem = newActive;

			/// Получаем документ, если вкладки нет - открываем документ в новой вкладке
			var documentTab = newActive != null 
				? EditorWindow.Documents[newActive] : EditorWindow.OpenDocument(documentName);
			documentTab.Editor.Focus();

			if (location != null)
			{
				documentTab.Editor.CaretOffset = location.Offset;
				documentTab.Editor.ScrollTo(location.Line, location.Column);
			}
		}

		public void SetSegments(List<DocumentSegment> segments, Color color)
		{
			foreach(var group in segments.GroupBy(s=>s.FileName))
			{
				var documentTab = EditorWindow.Documents
					.Where(d => d.Value.DocumentName == group.Key)
					.Select(d => d.Value).FirstOrDefault();

				if(documentTab != null)
				{
					documentTab.SegmentsColorizer.SetSegments(
						group.ToList(), 
						color
					);
				}
			}
		}

		public void ResetSegments()
		{
			foreach(var document in EditorWindow.Documents)
			{
				document.Value.SegmentsColorizer.ResetSegments();
			}
		}

		public void SaveSettings(LandExplorerSettings settings, string defaultPath)
		{
			DataContractSerializer serializer = new DataContractSerializer(typeof(LandExplorerSettings), new Type[] { typeof(ParserSettingsItem) });

			using (FileStream fs = new FileStream(SettingsPath, FileMode.Create))
			{
				serializer.WriteObject(fs, settings);
			}
		}

		public LandExplorerSettings LoadSettings(string defaultPath)
		{
			if (File.Exists(SettingsPath))
			{
				DataContractSerializer serializer = new DataContractSerializer(typeof(LandExplorerSettings), new Type[] { typeof(ParserSettingsItem) });

				using (FileStream fs = new FileStream(SettingsPath, FileMode.Open))
				{
					return (LandExplorerSettings)serializer.ReadObject(fs);
				}
			}
			else
			{
				return null;
			}
		}

		public event Action ShouldLoadSettings;

		public void RegisterOnDocumentChanged(Action<string> callback)
		{
			EditorWindow.DocumentChangedCallback = callback;
		}

		public HashSet<string> GetWorkingSet()
		{
			return new HashSet<string>(
				EditorWindow.Documents.Select(d => d.Value.DocumentName)
			);
		}

		public void RegisterOnDocumentSaved(Action<string> callback)
		{
			throw new NotImplementedException();
		}

		#endregion

		#region methods

		private TabItem GetActiveDocumentTab()
		{
			return (TabItem)EditorWindow.DocumentTabs.SelectedItem;
		}

		#endregion
	}
}

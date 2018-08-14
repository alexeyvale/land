using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using System.IO;
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

		public EditorAdapter(MainWindow window, string settingsPath)
		{
			EditorWindow = window;
			SettingsPath = settingsPath;
		}

		private TabItem GetActiveTab()
		{
			return (TabItem)EditorWindow.DocumentTabs.SelectedItem;
		}

		public string GetActiveDocumentName()
		{
			var activeTab = GetActiveTab();

			return activeTab != null ? EditorWindow.Documents[activeTab].DocumentName : null;
		}

		public int? GetActiveDocumentOffset()
		{
			var activeTab = GetActiveTab();

			return activeTab != null ? EditorWindow.Documents[activeTab].Editor.CaretOffset : (int?)null;
		}

		public string GetActiveDocumentText()
		{
			var activeTab = GetActiveTab();

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

		public void ProcessMessages(List<Message> messages)
		{
			foreach (var msg in messages)
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

		public void ResetSegments()
		{

		}

		public void SetActiveDocumentAndOffset(string documentName, int offset)
		{
			var newActive = EditorWindow.Documents
				.Where(d => d.Value.DocumentName == documentName)
				.Select(d => d.Key).FirstOrDefault();

			if(newActive!=null)
			{
				EditorWindow.DocumentTabs.SelectedItem = newActive;
				EditorWindow.Documents[newActive].Editor.CaretOffset = offset;
				EditorWindow.Documents[newActive].Editor.Focus();

				var location = EditorWindow.Documents[newActive].Editor.Document.GetLocation(offset);
				EditorWindow.Documents[newActive].Editor.ScrollTo(location.Line, location.Column);
			}
		}

		public void SetSegments(List<DocumentSegment> segments)
		{

		}

		public void SaveSettings(LandExplorerSettings settings)
		{
			DataContractSerializer serializer = new DataContractSerializer(typeof(LandExplorerSettings), new Type[] { typeof(ExtensionGrammarPair) });

			using (FileStream fs = new FileStream(SettingsPath, FileMode.Create))
			{
				serializer.WriteObject(fs, settings);
			}
		}

		public LandExplorerSettings LoadSettings()
		{
			if (File.Exists(SettingsPath))
			{
				DataContractSerializer serializer = new DataContractSerializer(typeof(LandExplorerSettings), new Type[] { typeof(ExtensionGrammarPair) });

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
	}
}

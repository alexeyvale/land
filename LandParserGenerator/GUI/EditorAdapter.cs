using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using System.IO;
using System.Windows.Media;
using System.Runtime.Serialization;
using Land.Core;
using Land.Control;

namespace Land.GUI
{
	public class EditorAdapter : IEditorAdapter
	{
		private const string LINE_END_SYMBOLS = "\u000A\u000D\u0085\u2028\u2029";
		private const string DEFAULT_LINE_END = "\r\n";

		private MainWindow EditorWindow { get; set; }
		private string SettingsPath { get; set; }
		private Action<string> DocumentSavingCallback { get; set; }

		public EditorAdapter(MainWindow window, string settingsPath)
		{
			EditorWindow = window;
			SettingsPath = settingsPath;

			RefreshSettingsFileWatcher();
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

		public SegmentLocation GetActiveDocumentSelection(bool adjustByLine)
		{
			var activeTab = GetActiveDocumentTab();

			if (activeTab != null)
			{
				var editor = EditorWindow.Documents[activeTab].Editor;

				var startOffset = adjustByLine
					? editor.Document.GetLineByOffset(editor.SelectionStart).Offset
					: editor.SelectionStart;
				var startPoint = editor.Document.GetLocation(startOffset);

				var endOffset = adjustByLine
					? editor.Document.GetLineByOffset(editor.SelectionStart + editor.SelectionLength).EndOffset
					: editor.SelectionStart + editor.SelectionLength;
				var endPoint = editor.Document.GetLocation(endOffset);

				return new SegmentLocation
				{
					Start = new PointLocation(startPoint.Line, startPoint.Column, startOffset),
					End = new PointLocation(endPoint.Line, endPoint.Column, endOffset),
				};
			}

			return null;
		}

		public string GetActiveDocumentText()
		{
			var activeTab = GetActiveDocumentTab();

			return activeTab != null ? EditorWindow.Documents[activeTab].Editor.Text : null;
		}

		public string GetDocumentLineEnd(string documentName)
		{
			var text = String.Join("", GetDocumentText(documentName)
				?.SkipWhile(c=>!LINE_END_SYMBOLS.Contains(c))
				.TakeWhile(c=>LINE_END_SYMBOLS.Contains(c))
			);

			return !String.IsNullOrEmpty(text)
				? text : DEFAULT_LINE_END;
		}

		public string GetDocumentText(string documentName)
		{
			return EditorWindow.Documents.Where(d => d.Value.DocumentName == documentName)
				.Select(d => d.Value.Editor.Text).FirstOrDefault();
		}

		public void SetDocumentText(string documentName, string text)
		{
			var document = EditorWindow.Documents.Values
				.Where(d => d.DocumentName == documentName).FirstOrDefault();

			if(document != null)
				document.Editor.Text = text;
		}

		public void InsertText(string documentName, string text, PointLocation point)
		{
			var document = EditorWindow.Documents.Values
				.Where(d => d.DocumentName == documentName).FirstOrDefault();

			if (document != null)
				document.Editor.Document.Insert(point.Offset, text);
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
				ProcessMessage(msg);
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
				documentTab.Editor.ScrollTo(
					location.Line.Value, 
					location.Column.Value + MainWindow.COLUMN_CORRECTION_ITEM
				);
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
				SettingsWereSaved = true;
			}
		}

		public LandExplorerSettings LoadSettings(string defaultPath)
		{
			if (File.Exists(SettingsPath))
			{
				DataContractSerializer serializer = new DataContractSerializer(typeof(LandExplorerSettings), new Type[] { typeof(ParserSettingsItem) });

				using (FileStream fs = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read))
				{
					try
					{
						var settings = (LandExplorerSettings)serializer.ReadObject(fs);

						if (SettingsFileWatcher.Path != Path.GetDirectoryName(SettingsPath))
							SettingsFileWatcher.Path = Path.GetDirectoryName(SettingsPath);

						return settings;
					}
					catch
					{
						return null;
					}
				}
			}
			else
			{
				return null;
			}
		}

		private FileSystemWatcher SettingsFileWatcher { get; set; }

		private bool SettingsWereSaved { get; set; }

		private void RefreshSettingsFileWatcher()
		{
			SettingsFileWatcher?.Dispose();

			SettingsFileWatcher = new FileSystemWatcher(Path.GetDirectoryName(SettingsPath));
			SettingsFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
			SettingsFileWatcher.Changed += SettingsFileChanged_Handler;
			SettingsFileWatcher.EnableRaisingEvents = true;
		}

		private void SettingsFileChanged_Handler(object sender, FileSystemEventArgs e)
		{
			if (e.FullPath == SettingsPath)
			{
				var fileInfo = new FileInfo(SettingsPath);

				if (fileInfo.Length > 0)
				{
					if(!SettingsWereSaved)
						ShouldLoadSettings?.Invoke();
					else
						SettingsWereSaved = false;
				}
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

		#endregion

		#region methods

		private TabItem GetActiveDocumentTab()
		{
			return (TabItem)EditorWindow.DocumentTabs.SelectedItem;
		}

		#endregion
	}
}

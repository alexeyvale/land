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
	public class ColorsManager
	{
		private Color[] ColorsList { get; set; } = new Color[] {
			Color.FromArgb(60, 100, 200, 100),
			Color.FromArgb(60, Colors.Cyan.R, Colors.Cyan.G, Colors.Cyan.B),
			Color.FromArgb(60, Colors.HotPink.R, Colors.HotPink.G, Colors.HotPink.B),
			Color.FromArgb(60, Colors.Coral.R, Colors.Coral.G, Colors.Coral.B),
			Color.FromArgb(60, Colors.Gold.R, Colors.Gold.G, Colors.Gold.B),
			Color.FromArgb(60, Colors.LightSkyBlue.R, Colors.LightSkyBlue.G, Colors.LightSkyBlue.B),
			Color.FromArgb(60, Colors.Thistle.R, Colors.Thistle.G, Colors.Thistle.B)
		};

		private Random Generator { get; set; } = new Random();

		private int ColorsUsed { get; set; } = 0;

		public Color GetColor()
		{
			return ColorsUsed < ColorsList.Length 
				? ColorsList[ColorsUsed++]
				: Color.FromArgb(45, (byte)Generator.Next(100, 206), (byte)Generator.Next(100, 206), (byte)Generator.Next(100, 206));
		}

		public void Reset()
		{
			ColorsUsed = 0;
		}
	}

	public class EditorAdapter : IEditorAdapter
	{
		private MainWindow EditorWindow { get; set; }
		private string SettingsPath { get; set; }
		private ColorsManager ColorsManager { get; set; } = new ColorsManager(); 

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
				if (!location.Line.HasValue)
				{
					var locationFromEditor = documentTab.Editor.Document.GetLocation(location.Offset.Value);
					location.Line = locationFromEditor.Line;
					location.Column = locationFromEditor.Column;
				}

				documentTab.Editor.CaretOffset = location.Offset.Value;
				documentTab.Editor.ScrollTo(location.Line.Value, location.Column.Value);
			}
		}

		public Color SetSegments(List<DocumentSegment> segments)
		{
			var color = ColorsManager.GetColor();

			foreach(var group in segments.GroupBy(s=>s.FileName))
			{
				var documentTab = EditorWindow.Documents
					.Where(d => d.Value.DocumentName == group.Key)
					.Select(d => d.Value).FirstOrDefault();

				if(documentTab != null)
				{
					documentTab.SegmentsColorizer.SetSegments(group.ToList(), color);
				}
			}

			color.A = (byte)255;
			return color;
		}

		public void ResetSegments()
		{
			foreach(var document in EditorWindow.Documents)
			{
				document.Value.SegmentsColorizer.ResetSegments();
			}

			ColorsManager.Reset();
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

		#endregion


		#region methods

		private TabItem GetActiveDocumentTab()
		{
			return (TabItem)EditorWindow.DocumentTabs.SelectedItem;
		}

		#endregion
	}
}

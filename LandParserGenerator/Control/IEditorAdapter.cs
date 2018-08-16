using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using System.Runtime.Serialization;

using Land.Core;

namespace Land.Control
{
	public class DocumentSegment
	{
		public string FileName { get; set; }
		public int StartOffset { get; set; }
		public int EndOffset { get; set; }
		public bool CaptureWholeLine { get; set; }
	}

	[DataContract]
	public class ExtensionGrammarPair
	{
		[DataMember]
		public List<string> Extensions { get; set; } = new List<string>();

		[DataMember]
		public string GrammarPath { get; set; }

		public string ExtensionsString
		{
			get { return String.Join("; ", Extensions); }

			set
			{
				/// Разбиваем строку на отдельные расширения, добавляем точку, если она отсутствует
				Extensions = value.Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(ext => ext.StartsWith(".") ? ext : '.' + ext).ToList();
			}
		}

		public ExtensionGrammarPair Clone()
		{
			return new ExtensionGrammarPair()
			{
				Extensions = Extensions,
				GrammarPath = GrammarPath
			};
		}
	}

	[DataContract]
	public class LandExplorerSettings
	{
		[DataMember]
		public bool HighlightSelectedElement { get; set; }

		[DataMember]
		public ObservableCollection<ExtensionGrammarPair> Grammars { get; set; } = new ObservableCollection<ExtensionGrammarPair>();

		public LandExplorerSettings Clone()
		{
			return new LandExplorerSettings()
			{
				HighlightSelectedElement = HighlightSelectedElement,
				Grammars = new ObservableCollection<ExtensionGrammarPair>(Grammars.Select(g => g.Clone()))
			};
		}
	}

	public interface IEditorAdapter
	{
		#region Active document

		/// <summary>
		/// Ести ли активный документ
		/// </summary>
		bool HasActiveDocument();

		/// <summary>
		/// Получение имени активного документа
		/// </summary>
		string GetActiveDocumentName();

		/// <summary>
		/// Получение текста активного документа
		/// </summary>
		string GetActiveDocumentText();

		/// <summary>
		/// Получение позиции курсора в активном документе
		/// </summary>
		int? GetActiveDocumentOffset();

		#endregion


		#region Document by name

		/// <summary>
		/// Полчение текста документа
		/// </summary>
		string GetDocumentText(string documentName);

		/// <summary>
		/// Задание активного документа и установка курсора
		/// </summary>
		void SetActiveDocumentAndOffset(string documentName, PointLocation location);

		#endregion


		#region Text highlighting

		/// <summary>
		/// Выделить участки текста в файле
		/// </summary>
		Color SetSegments(List<DocumentSegment> segments);

		/// <summary>
		/// Сбросить выделение
		/// </summary>
		void ResetSegments();

		#endregion


		#region Messages

		void ProcessMessages(List<Message> messages, bool skipTrace, bool resetPrevious);

		void ProcessMessage(Message message);

		#endregion

		void SaveSettings(LandExplorerSettings settings);

		LandExplorerSettings LoadSettings();
	}
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using System.Runtime.Serialization;

using Land.Core;
using Land.Core.Parsing.Preprocessing;

namespace Land.Control
{
	public class DocumentSegment
	{
		public string FileName { get; set; }
		public int StartOffset { get; set; }
		public int EndOffset { get; set; }
		public bool CaptureWholeLine { get; set; }
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


		#region Settings

		void SaveSettings(LandExplorerSettings settings);

		LandExplorerSettings LoadSettings();

		#endregion


		#region Callbacks

		void RegisterOnDocumentSaved(Action<string> callback);

		void RegisterOnDocumentChanged(Action<string> callback);

		void RegisterOnDocumentsSetChanged(Action<HashSet<string>> callback);

		#endregion
	}
}

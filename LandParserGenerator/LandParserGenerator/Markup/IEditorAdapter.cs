using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Land.Core.Markup
{
	public class DocumentSegment
	{
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
		void SetActiveDocumentAndOffset(string documentName, int offset);

		#endregion


		#region Text highlighting

		/// <summary>
		/// Выделить участки текста в файле
		/// </summary>
		void SetSegments(List<DocumentSegment> segments);

		/// <summary>
		/// Сбросить выделение
		/// </summary>
		void ResetSegments();

		#endregion


		#region Messages

		void ProcessMessages(List<Message> messages);

		void ProcessMessage(Message message);

		#endregion
	}
}

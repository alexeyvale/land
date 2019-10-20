using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Land.Core.Specification;
using Land.Markup.CoreExtension;

namespace Land.Control
{
	public class LanguageSettings
	{
		public bool UseHorizontalContext { get; private set; } = false;

		public LanguageSettings(SymbolOptionsManager opts)
		{
			if (opts != null)
			{
				UseHorizontalContext = opts.IsSet(MarkupOption.USEHORIZONTAAL);
			}
		}
	}
}

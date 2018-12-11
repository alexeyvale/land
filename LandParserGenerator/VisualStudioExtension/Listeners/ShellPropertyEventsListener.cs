using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Shell.Interop;

namespace Land.VisualStudioExtension.Listeners
{
	internal sealed class ShellPropertyEventsListener : IVsShellPropertyEvents
	{
		public int OnShellPropertyChange(int propid, object var)
		{
			if (propid == (int)__VSSPROPID.VSSPROPID_Zombie)
			{
				var isZombie = (bool)var;
				if (!isZombie)
				{
					ServiceEventAggregator.Instance.OnShellLoaded();
				}
			}
			return VSConstants.S_OK;
		}
	}
}

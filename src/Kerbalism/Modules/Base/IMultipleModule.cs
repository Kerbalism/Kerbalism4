﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	interface IMultipleKsmModule
	{
		string KsmModuleId { get; }
		string ConfigValueIdName { get; }
	}
}

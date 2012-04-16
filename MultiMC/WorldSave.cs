﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MultiMC.Tasks;

namespace MultiMC
{
	public class WorldSave
	{
		public WorldSave(Instance inst, string path)
		{
			Instance = inst;
			Path = path;
		}

		public Instance Instance
		{
			get;
			protected set;
		}

		public string Path
		{
			get;
			protected set;
		}
	}
}

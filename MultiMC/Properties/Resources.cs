// 
//  Copyright 2012  Andrew Okin
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
using System;
using System.IO;
using System.Collections.Generic;

using Gtk;
using Gdk;

namespace MultiMC
{
	public class Resources
	{
		// Dropbox: http://dl.dropbox.com/u/52412912/
		
		// URLs
//		public const string DotNetZipURL = "http://multimc.tk/MultiMC/DotNetZip.dll";
//		public const string VInfoUrl = "http://multimc.tk/MultiMC/cs-version";
//		public const string LatestVersionURL = "http://multimc.tk/MultiMC/MultiMC.exe";
		
		public const string VInfoUrl = "http://dl.dropbox.com/u/52412912/MultiMC/cs-version";
		public const string LatestVersionURL = "http://dl.dropbox.com/u/52412912/MultiMC/MultiMC.exe";
		public const string DotNetZipURL = 
			"http://dl.dropbox.com/u/52412912/MultiMC/Ionic.Zip.Reduced.dll";
		public const string LauncherURL = 
			"https://s3.amazonaws.com/MinecraftDownload/launcher/minecraft.jar";
		public const string MinecraftDLUri = 
			"http://s3.amazonaws.com/MinecraftDownload/";
		
		// Other
		public const string InstanceXmlFile = "instance.xml";
		public const string InstDir = "instances";
		public const string NewVersionFileName = "update.exe";
		public const string ConfigFileName = "multimc.cfg";
		public const string LastLoginFileName = "lastlogin";
		public const string LastLoginKey = 
			"Bi[r;Yq'/FKM].@wgZoIBh~bkY}&W,0>)Gz%Jbusexx)&ijhXV}b^8m;&jfL73tx";
		
		/// <summary>
		/// Gets the version string for displaying to the user.
		/// </summary>
		/// <value>
		/// The version string.
		/// </value>
		public static string VersionString
		{
			get
			{
				Version v = AppUtils.GetVersion();
				return string.Format("{0}.{1}.{2}.{3}", v.Major, v.Minor, v.Build, v.Revision);
			}
		}

		public static Pixbuf GetInstIcon(string key)
		{
			if (iconDict.ContainsKey(key))
				return iconDict[key];
			else
				return iconDict["stone"];
		}
		
		public static string[] IconKeys
		{
			get
			{
				string[] keyArray = new string[iconDict.Keys.Count];
				iconDict.Keys.CopyTo(keyArray, 0);
				return keyArray;
			}
		}
		
		private static Dictionary<string, Pixbuf> iconDict = LoadIcons();
		
		public static Dictionary<string, Pixbuf> LoadIcons()
		{
			Dictionary<string, Pixbuf> pixBufDict = new Dictionary<string, Pixbuf>();
			
			if (Directory.Exists("icons"))
			{
				foreach (string f in Directory.GetFiles("icons"))
				{
					pixBufDict.Add(Path.GetFileNameWithoutExtension(f), new Pixbuf(f));
				}
			}
			
			foreach (string name in 
			        new string[]{ "stone", "brick", 
				"diamond", "dirt", "gold", "grass", "iron", "planks", "tnt" })
			{
				if (!pixBufDict.ContainsKey(name))
					pixBufDict.Add(name, Pixbuf.LoadFromResource("MultiMC.icons." + name));
			}
			
			return pixBufDict;
		}
	}
}


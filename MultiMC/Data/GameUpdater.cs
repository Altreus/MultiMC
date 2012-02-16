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
using System.Net;
using System.Security.Cryptography;

using MultiMC.Data;

using Ionic.Zip;

namespace MultiMC.Tasks
{
	public class GameUpdater : Task
	{
		string mainGameUrl;
		string latestVersion;
		Uri[] uriList;
		bool forceUpdate;
		bool shouldUpdate;
		int totalDownloadSize;
		int currentDownloadSize;
		
		public GameUpdater(Instance inst, 
		                   string latestVersion, 
		                   string mainGameUrl, 
		                   bool forceUpdate = false)
		{
			this.Inst = inst;
			this.latestVersion = latestVersion;
			this.mainGameUrl = mainGameUrl;
			this.forceUpdate = forceUpdate;
		}
		
		protected override void TaskStart()
		{
			OnStart();
			try
			{
				State = EnumState.CHECKING_CACHE;
				Progress = 5;
				
				// Get a list of URLs to download from
				LoadJarURLs();
				
				// Create the bin directory if it doesn't exist
				if (!Directory.Exists(Inst.BinDir))
					Directory.CreateDirectory(Inst.BinDir);
				
				string binDir = Inst.BinDir;
				if (this.latestVersion != null)
				{
					string versionFile = Path.Combine(binDir, "version");
					bool cacheAvailable = false;
				
					if (!forceUpdate && File.Exists(versionFile) && 
					    (latestVersion.Equals("-1") ||
					 latestVersion.Equals(File.ReadAllText(versionFile))))
					{
						cacheAvailable = true;
						Progress = 90;
					}
				
					if ((forceUpdate) || (!cacheAvailable))
					{
						shouldUpdate = true;
						if (!forceUpdate && File.Exists(versionFile))
							AskToUpdate();
						if (this.shouldUpdate)
						{
							WriteVersionFile(versionFile, latestVersion);
							
							try
							{
								DownloadJars();
							} catch (WebException e)
							{
								OnErrorMessage(
								string.Format("An error occurred when downloading packages.\n" +
									"Details:\n{0}", e.ToString()));
							}
							ExtractNatives();
							Progress = 100;
						}
					}
				}
			} catch (WebException e)
			{
				OnErrorMessage(string.Format("An error occurred when trying to " +
				                             "download Minecraft.\n" +
				                             "Details:\n{0}", e.ToString()));
				Cancel();
			}
			OnComplete();
		}

		protected void LoadJarURLs()
		{
			State = EnumState.DETERMINING_PACKAGES;
			string[] jarList = new string[]
			{ 
				this.mainGameUrl, "lwjgl_util.jar", "jinput.jar", "lwjgl.jar"
			};
			
			this.uriList = new Uri[jarList.Length + 1];
			Uri forkkBaseUri = new Uri(Resources.ForkkMCDLUri);
			Uri mojangBaseUri = new Uri(Resources.MojangMCDLUri);
			
			for (int i = 0; i < jarList.Length; i++)
			{
				// minecraft.jar should be downloaded from mojang
				if (i == 0)
					this.uriList[i] = new Uri(mojangBaseUri, jarList[i]);
				
				// The latest LWJGL should be downloaded from Forkk's dropbox 
				// since minecraft's version doesn't work with MultiMC
				else
					this.uriList[i] = new Uri(forkkBaseUri, jarList[i]);
			}
			
			string nativeJar = string.Empty;
			
			if (OSUtils.Windows)
				nativeJar = "windows_natives.jar";
			else if (OSUtils.Linux)
				nativeJar = "linux_natives.jar";
			else if (OSUtils.MacOSX)
				nativeJar = "macosx_natives.jar";
			else
			{
				OnErrorMessage("Your operating system is not supported.");
				Cancel();
			}
			
			this.uriList[this.uriList.Length - 1] = new Uri(forkkBaseUri, nativeJar);
		}
		
		protected void DownloadJars()
		{
			Properties md5s = new Properties();
			if (File.Exists(Path.Combine(Inst.BinDir, "md5s")))
				md5s.Load(Path.Combine(Inst.BinDir, "md5s"));
			State = EnumState.DOWNLOADING;
			
			int[] fileSizes = new int[this.uriList.Length];
			bool[] skip = new bool[this.uriList.Length];
			
			// Get the headers and decide what files to skip downloading
			for (int i = 0; i < uriList.Length; i++)
			{
				Console.WriteLine("Getting header " + uriList[i].ToString());
				
				HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uriList[i]);
				request.Timeout = 1000 * 15; // Set a 15 second timeout
				request.Method = "HEAD";
				
				string etagOnDisk = null;
				if (md5s.ContainsKey(GetFileName(uriList[i])))
					etagOnDisk = md5s[GetFileName(uriList[i])];
				
				if (!forceUpdate && !string.IsNullOrEmpty(etagOnDisk))
					request.Headers[HttpRequestHeader.IfNoneMatch] = etagOnDisk;
				
				using (HttpWebResponse response = ((HttpWebResponse)request.GetResponse()))
				{
					int code = (int)response.StatusCode;
					if (code == 300)
						skip[i] = true;
					
					fileSizes[i] = (int)response.ContentLength;
					this.totalDownloadSize += fileSizes[i];
					Console.WriteLine("Got response: " + code + " and file size of " + 
					                  fileSizes[i] + " bytes");
				}
			}
			
			int initialPercentage = Progress;
			
			byte[] buffer = new byte[1024 * 10];
			for (int i = 0; i < this.uriList.Length; i++)
			{
				if (skip[i])
				{
					Progress = (initialPercentage + fileSizes[i] * 
					            (100 - initialPercentage) / this.totalDownloadSize);
				}
				else
				{
					string currentFile = GetFileName(uriList[i]);
					
					if (currentFile == "minecraft.jar" && File.Exists("mcbackup.jar"))
						File.Delete("mcbackup.jar");
					
					md5s.Remove(currentFile);
					md5s.Save(Path.Combine(Inst.BinDir, "md5s"));
					
					int failedAttempts = 0;
					const int MAX_FAILS = 3;
					bool downloadFile = true;
					
					// Download the files
					while (downloadFile)
					{
						downloadFile = false;

						HttpWebRequest request = (HttpWebRequest) HttpWebRequest.Create(uriList[i]);
						request.Headers[HttpRequestHeader.CacheControl] = "no-cache";

						HttpWebResponse response = (HttpWebResponse) request.GetResponse();

						string etag = "";
						// If downloading from Mojang, use ETag.
						if (uriList[i].ToString().StartsWith(Resources.MojangMCDLUri))
						{
							etag = response.Headers[HttpResponseHeader.ETag];
							etag = etag.TrimEnd('"').TrimStart('"');
						}
						// If downloading from dropbox, ignore MD5s
						else
						{
							// TODO add a way to verify integrity of files downloaded from dropbox
						}

						Stream dlStream = response.GetResponseStream();
						using (FileStream fos =
							new FileStream(Path.Combine(Inst.BinDir, currentFile), FileMode.Create))
						{
							int fileSize = 0;

							using (MD5 digest = MD5.Create())
							{
								digest.Initialize();
								int readSize;
								while ((readSize = dlStream.Read(buffer, 0, buffer.Length)) > 0)
								{
									//							Console.WriteLine("Read " + readSize + " bytes");
									fos.Write(buffer, 0, readSize);

									this.currentDownloadSize += readSize;
									fileSize += readSize;

									digest.TransformBlock(buffer, 0, readSize, null, 0);

									//							Progress = fileSize / fileSizes[i];

									Progress = (initialPercentage + this.currentDownloadSize *
												(100 - initialPercentage) / this.totalDownloadSize);
								}
								digest.TransformFinalBlock(new byte[] { }, 0, 0);

								dlStream.Close();

								string md5 = DataUtils.HexEncode(digest.Hash).Trim();
								etag = etag.Trim();

								bool md5Matches = true;
								if (!string.IsNullOrEmpty(etag) && !string.IsNullOrEmpty(md5))
								{
									// This is temporarily disabled since dropbox doesn't use MD5s as etags
									md5Matches = md5.Equals(etag);
									//							Console.WriteLine(md5 + "\n" + etag + "\n");
								}

								if (md5Matches && fileSize == fileSizes[i] || fileSizes[i] <= 0)
								{
									md5s[(currentFile.Contains("natives") ?
										  currentFile : currentFile)] = etag;
									md5s.Save(Path.Combine(Inst.BinDir, "md5s"));
								}
								else
								{
									failedAttempts++;
									if (failedAttempts < MAX_FAILS)
									{
										downloadFile = true;
										this.currentDownloadSize -= fileSize;
									}
									else
									{
										OnErrorMessage("Failed to download " + currentFile +
													   " MD5 sums did not match.");
										Cancel();
									}
								}
							}
						}
					}
				}
			}
		}
		
		protected void ExtractNatives()
		{
			State = EnumState.EXTRACTING_PACKAGES;
			
			string nativesJar = 
				Path.Combine(Inst.BinDir, GetFileName(uriList[uriList.Length - 1]));
			string nativesDir = Path.Combine(Inst.BinDir, "natives");
			
			if (!Directory.Exists(nativesDir))
				Directory.CreateDirectory(nativesDir);

			using (ZipFile zf = new ZipFile(nativesJar))
			{
				Console.WriteLine(string.Format("Extracting natives from {0} to {1}", nativesJar, nativesDir));
				ExtractRecursive(zf, nativesDir);
			}
			
			if (Directory.Exists(Path.Combine(nativesDir, "META-INF")))
				Directory.Delete(Path.Combine(nativesDir, "META-INF"), true);
			
			File.Delete(nativesJar);
		}
		
		protected void ExtractRecursive(ZipFile zf, string dest, string pathinzip = "/")
		{
			foreach (ZipEntry entry in zf)
			{
				if (entry.FileName.Contains("META-INF"))
					continue;
				
				if (entry.IsDirectory)
				{
					string dir = Path.Combine(dest, entry.FileName);
					if (!Directory.Exists(dir))
						Directory.CreateDirectory(dir);
					ExtractRecursive(zf, dir, string.Format("{0}/{1}", pathinzip, entry.FileName));
				}
				else
				{
					string destFile = Path.Combine(dest, entry.FileName);
					Console.WriteLine("Extracting to " + destFile);
					
					if (!Directory.Exists(dest))
						Directory.CreateDirectory(dest);

					using (FileStream destStream = File.Open(destFile, FileMode.Create))
					{
						entry.Extract(destStream);
					}
				}
			}
		}
		
		protected void AskToUpdate()
		{
			AskUpdateEventArgs args = new AskUpdateEventArgs("Would you like to update Minecraft?");
			if (AskUpdate != null)
				AskUpdate(this, args);
			this.shouldUpdate = args.ShouldUpdate;
		}
		
		protected string GetFileName(Uri uri)
		{
			return Path.GetFileName(uri.LocalPath);
//			Console.WriteLine("str: " + uri.ToString() + " lp: " + uri.LocalPath);
//			string str = uri.ToString().Substring(uri.ToString().LastIndexOf("/"));
//			if (str.IndexOf("?") > 0)
//				str = str.Substring(0, str.IndexOf("?"));
//			return str;
		}
		
		public static string ReadVersionFile(string vfile)
		{
			if (!File.Exists(vfile))
				return null;
			string data = "";
			using (Stream stream = File.OpenRead(vfile))
			{
				BinaryReader binRead = new BinaryReader(stream);
				data = binRead.ReadString();
				return data;
			}
		}
		
		public static void WriteVersionFile(string vfile, string version)
		{
			using (Stream stream = File.Open(vfile, FileMode.Create))
			{
				BinaryWriter binWrite = new BinaryWriter(stream);
				binWrite.Write(version);
				binWrite.Flush();
			}
		}
		
		protected EnumState State
		{
			get { return state; }
			set
			{
				state = value;
				switch (value)
				{
				case EnumState.INIT:
					Status = "Initializing loader";
					break;
				case EnumState.DETERMINING_PACKAGES:
					Status = "Determining packages to load";
					break;
				case EnumState.CHECKING_CACHE:
					Status = "Checking cache for existing files";
					break;
				case EnumState.DOWNLOADING:
					Status = "Downloading packages";
					break;
				case EnumState.EXTRACTING_PACKAGES:
					Status = "Extracting downloaded packages";
					break;
				case EnumState.UPDATING_CLASSPATH:
					Status = "Updating classpath";
					break;
				case EnumState.SWITCHING_APPLET:
					Status = "Switching applet";
					break;
				case EnumState.INITIALIZE_REAL_APPLET:
					Status = "Initializing real applet";
					break;
				case EnumState.START_REAL_APPLET:
					Status = "Starting real applet";
					break;
				case EnumState.DONE:
					Status = "Done loading";
					break;
				}
			}
		}
		
		EnumState state;
		
		public Instance Inst
		{
			get;
			protected set;
		}
		
		public enum EnumState
		{
			INIT, // 1
			DETERMINING_PACKAGES, // 2
			CHECKING_CACHE, // 3
			DOWNLOADING, // 4
			EXTRACTING_PACKAGES, // 5
			UPDATING_CLASSPATH, // 6
			SWITCHING_APPLET, // 7
			INITIALIZE_REAL_APPLET, // 8
			START_REAL_APPLET, // 9
			DONE, // 10
		}
		
		/// <summary>
		/// Occurs when the task asks the user to update.
		/// </summary>
		public event EventHandler<AskUpdateEventArgs> AskUpdate;
	}
	
	public class AskUpdateEventArgs : EventArgs
	{
		public AskUpdateEventArgs(string message)
		{
			this.Message = message;
			ShouldUpdate = false;
		}
		
		public string Message
		{
			get;
			protected set;
		}
		
		public bool ShouldUpdate
		{
			get;
			set;
		}
	}
}


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

using MultiMC;

namespace MultiMC
{
	public sealed class Instance : IDisposable
	{
		#region Fields

		/// <summary>
		/// Invalid characters that aren't allowed in an instance's name.
		/// </summary>
		const string INVALID_NAME_CHARS = "< > \n \\ &";

		/// <summary>
		/// Name of the data file inside instance folders
		/// </summary>
		static string INST_DATA_FILENAME = "instance.cfg";

		/// <summary>
		/// The instance's data file
		/// </summary>
		ConfigFile cfgFile;

		/// <summary>
		/// The instance's root directory
		/// </summary>
		string rootDir;

		/// <summary>
		/// If true, config values are saved when changed.
		/// </summary>
		bool autosave;

		/// <summary>
		/// The process the instance is running in. If the instance isn't running, this will be null.
		/// </summary>
		Process instProc;

		#endregion

		#region Static Methods

		/// <summary>
		/// Loads all instances from the specified directory
		/// </summary>
		/// <param name="instDir">Directory containing the instances</param>
		/// <returns>The instances loaded</returns>
		public static Instance[] LoadInstances(string instDir)
		{
			if (!Directory.Exists(instDir))
			{
				return new Instance[0];
			}

			ArrayList instList = new ArrayList();
			foreach (string dir in Directory.GetDirectories(instDir))
			{
				Console.WriteLine("Loading instance from " + dir + "...");
				Instance inst = null;
				try
				{
					inst = LoadInstance(dir);
				} catch (InvalidInstanceException e)
				{
					Console.WriteLine(e.Message);
				}

				if (inst != null)
					instList.Add(inst);
			}
			return (Instance[])instList.ToArray(typeof(Instance));
		}

		/// <summary>
		/// Loads the instance from the specified directory.
		/// </summary>
		/// <param name="rootDir">The instance's root directory</param>
		/// <returns>The instance loaded or null if the instance isn't valid</returns>
		public static Instance LoadInstance(string rootDir)
		{
			// Verify the instance
			string configFileName = Path.Combine(rootDir, INST_DATA_FILENAME);
			if (!Directory.Exists(rootDir))
			{
				throw new InvalidInstanceException("Instance root directory '" + rootDir +
					"' not found!");
			}
			if (!File.Exists(configFileName))
			{
				if (File.Exists(Path.Combine(rootDir, "instance.xml")))
				{
					System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument();
					xmlDoc.Load(Path.Combine(rootDir, "instance.xml"));
					return new Instance(new OldInstance(xmlDoc, rootDir));
				}
				throw new InvalidInstanceException("Instance data file '" + configFileName + 
					"' not found!");
			}

			// Initialize a new instance from ini
			ConfigFile cfg = new ConfigFile();
			cfg.Load(configFileName);
			return new Instance(cfg, rootDir);
		}

		#endregion

		#region Methods

		public Instance(string name, string rootDir, bool autosave = true)
		{
			this.cfgFile = new ConfigFile();

			this.Name = name;
			this.RootDir = rootDir;

			this.autosave = autosave;
			
			InstMods = new InstanceMods(this);
			InstMods.Update();
			AutoSave();
		}

		/// <summary>
		/// Initializes a new instance from the given config file
		/// </summary>
		/// <param name="config">A config file containing the instance data</param>
		/// <pparam name="rootDir">The instance's root directory</pparam>
		public Instance(ConfigFile config, string rootDir, bool autosave = true)
		{
			this.cfgFile = config;
			this.rootDir = rootDir;

			this.autosave = autosave;
			
			InstMods = new InstanceMods(this);
			InstMods.Update();
		}

		public Instance(OldInstance oldInst)
		{
			this.cfgFile = new ConfigFile();
			this.rootDir = oldInst.RootDir;

			this.autosave = true;

			InstMods = new InstanceMods(this);
			InstMods.Update();

			this.IconKey = oldInst.IconKey;
			this.Name = oldInst.Name;
			this.NeedsRebuild = oldInst.NeedsRebuild;
			//this.Notes = oldInst.Notes;
		}

		/// <summary>
		/// Saves the instance's config file
		/// </summary>
		/// <param name="file">The file to save to.
		/// If null, the default config file will be used.</param>
		public void SaveData(string file = null)
		{
			if (file == null)
				file = Path.Combine(RootDir, INST_DATA_FILENAME);

			if (!Directory.Exists(RootDir))
			{
				Directory.CreateDirectory(RootDir);
			}
			cfgFile.Save(file);
		}

		private void AutoSave()
		{
			if (autosave)
				SaveData();
		}

		/// <summary>
		/// Checks if this is a valid instance
		/// </summary>
		/// <returns>True if instance is valid</returns>
		public bool IsValid()
		{
			if (!Directory.Exists(RootDir))
			{
				return false;
			}
			return true;
		}

		/// <summary>
		/// Launches the instance
		/// </summary>
		/// <returns>The process the instance is running in</returns>
		public Process Launch(string username, string sessionID)
		{
			using (FileStream output = File.Open("MultiMCLauncher.class", FileMode.Create))
			{
				using (Stream input = System.Reflection.Assembly.
						GetCallingAssembly().
							GetManifestResourceStream("MultiMC.Launcher.MultiMCLauncher.class"))
				{
					byte[] buffer = new byte[1024 * 2];
					int count = 0;
					while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
					{
						output.Write(buffer, 0, count);
					}
				}
			}
			
			int xms = AppSettings.Main.MinMemoryAlloc;
			int xmx = AppSettings.Main.MaxMemoryAlloc;
			string javaPath = AppSettings.Main.JavaPath;
			
			instProc = new Process();
			ProcessStartInfo mcProcStart = new ProcessStartInfo();

			//mcProcStart.FileName = "cmd";
			mcProcStart.FileName = javaPath;
			mcProcStart.Arguments = string.Format(
				"-Xmx{4}m -Xms{5}m " +
				"{0} \"{1}\" \"{2}\" {3}",
				"MultiMCLauncher", Path.GetFullPath(MinecraftDir), username, sessionID,
				xmx, xms);

			instProc.EnableRaisingEvents = true;
			mcProcStart.CreateNoWindow = true;
			mcProcStart.UseShellExecute = false;
			mcProcStart.RedirectStandardOutput = true;
			mcProcStart.RedirectStandardError = true;

			instProc.Exited += new EventHandler(ProcExited);
			instProc.Disposed += (o, args) => ProcessDisposed = true;

			instProc.StartInfo = mcProcStart;
			instProc.Start();

			if (InstLaunch != null)
				InstLaunch(this, EventArgs.Empty);

			return instProc;
		}

		void ProcExited(object sender, EventArgs e)
		{
			if (InstQuit != null)
				InstQuit(this, new InstQuitEventArgs((sender as Process).ExitCode,
				                                     (sender as Process).ExitTime));
		}

		public void Dispose()
		{
			if (this.Running)
				throw new InvalidOperationException("Cannot dispose an instance that is running!");
			InstMods.Dispose();
			if (instProc != null)
				instProc.Dispose();
		}

		#endregion

		#region Properties
		
		public InstanceMods InstMods
		{
			get;
			private set;
		}

		/// <summary>
		/// The instance's name
		/// </summary>
		public string Name
		{
			get { return cfgFile["name", RootDir]; }
			set
			{
				cfgFile["name"] = value;
				AutoSave();
			}
		}

		/// <summary>
		/// The image list key for this instance's icon ('grass' by default)
		/// </summary>
		public string IconKey
		{
			get { return cfgFile["iconKey", "default"]; }
			set
			{
				cfgFile["iconKey"] = value;
				AutoSave();
			}
		}

		/// <summary>
		/// User-made notes on the instance
		/// </summary>
		public string Notes
		{
			get { return cfgFile["notes", ""]; }
			set
			{
				cfgFile["notes"] = value;
				AutoSave();
			}
		}
		
		public bool NeedsRebuild
		{
			get { return bool.Parse(cfgFile["NeedsRebuild", "false"]); }
			set
			{
				DebugUtils.Print("Rebuild set to {0}", value.ToString());
				cfgFile["NeedsRebuild"] = value.ToString();
				AutoSave();
			}
		}

		#region Directories

		/// <summary>
		/// The root directory of the instance
		/// </summary>
		public string RootDir
		{
			get { return this.rootDir; }
			set
			{
				this.rootDir = value;
				AutoSave();
			}
		}

		/// <summary>
		/// The directory where mods will be stored
		/// </summary>
		public string InstModsDir
		{
			get { return Path.Combine(RootDir, "instMods"); }
		}
		
		/// <summary>
		/// Gets the mod list file. This file stores a list of all the mods installed in the
		/// order that they will be installed.
		/// </summary>
		/// <value>
		/// The mod list file.
		/// </value>
		public string ModListFile
		{
			get { return Path.Combine(RootDir, Properties.Resources.ModListFileName); }
		}

		/// <summary>
		/// The instance's .minecraft folder
		/// </summary>
		public string MinecraftDir
		{
			get
			{
				if (Directory.Exists(Path.Combine(RootDir, ".minecraft")) &&
				    !Directory.Exists(Path.Combine(RootDir, "minecraft")))
					return Path.Combine(RootDir, ".minecraft");
				else
					return Path.Combine(RootDir, "minecraft");
			}
		}

		/// <summary>
		/// The instance's bin folder (.minecraft\bin)
		/// </summary>
		public string BinDir
		{
			get { return Path.Combine(MinecraftDir, "bin"); }
		}
		
		/// <summary>
		/// The instance's minecraft.jar
		/// </summary>
		public string MCJar
		{
			get { return Path.Combine(BinDir, "minecraft.jar"); }
		}

		/// <summary>
		/// ModLoader's folder (.minecraft\mods)
		/// </summary>
		public string ModLoaderDir
		{
			get { return Path.Combine(MinecraftDir, "mods"); }
		}

		/// <summary>
		/// The texture packs folder (.minecraft\texturepacks)
		/// </summary>
		public string TexturePackDir
		{
			get { return Path.Combine(MinecraftDir, "texturepacks"); }
		}

		#endregion

		/// <summary>
		/// The process the instance is running in
		/// </summary>
		public Process InstProcess
		{
			get
			{
				if (Running)
				{
					return instProc;
				}
				else
				{
					return null;
				}
			}
		}
		
		public bool Running
		{
			get { return !(instProc == null || ProcessDisposed || instProc.HasExited); }
		}
		
		public bool CanPlayOffline
		{
			get
			{
				string vfile = Path.Combine(BinDir, "version");
				if (Directory.Exists(BinDir) && File.Exists(vfile))
				{
					string version = Tasks.GameUpdater.ReadVersionFile(vfile);
					if (version != null && version.Length > 0)
						return true;
				}
				return false;
			}
		}

		public bool ProcessDisposed
		{
			get;
			private set;
		}

		#endregion

		#region Events
		
		/// <summary>
		/// Occurrs when the instance quits.
		/// </summary>
		public event EventHandler<InstQuitEventArgs> InstQuit;
		
		/// <summary>
		/// Occurs when the instance launches.
		/// </summary>
		public event EventHandler InstLaunch;
		

		#endregion

		#region Utility Methods

		/// <summary>
		/// Checks if the given instance name is valid
		/// </summary>
		/// <param name="name">The instance name</param>
		/// <returns>true if the name is valid</returns>
		public static bool NameIsValid(string name)
		{
			foreach (char nameChar in INVALID_NAME_CHARS)
			{
				if (name.Contains(nameChar) && nameChar != ' ')
				{
					return false;
				}
			}
			return name.Length > 0;
		}

		/// <summary>
		/// Generates a valid directory name for an instance with the given name
		/// </summary>
		/// <param name="name">The name of the instance</param>
		/// <param name="instDir">The directory that will contain the instance</param>
		/// <returns>A valid directory name</returns>
		public static string GetValidDirName(string name, string instDir)
		{
			if (name.Length == 0)
				name = "Untitled";

			char[] nameChars = name.ToCharArray();
			for (int i = 0; i < nameChars.Length; i++)
			{
				char c = nameChars[i];
				if (Path.GetInvalidFileNameChars().Contains(c))
				{
					nameChars[i] = '-';
				}
				if (c == ' ')
				{
					nameChars[i] = '_';
				}
			}

			string validName;
			validName = new string(nameChars);

			if (instDir != null)
			{
				int i = 0;
				while (Directory.Exists(Path.Combine(instDir, validName)))
				{
					validName = new string(nameChars) + "_" + (++i);
				}
			}

			return validName;
		}

		#endregion
	}
	
	public sealed class InstanceMods : IEnumerable<string>, IDisposable
	{
		List<string> modList;
		FileSystemWatcher watcher;
		
		public InstanceMods(Instance inst)
		{
			modList = new List<string>();
			Inst = inst;
			
			if (!Directory.Exists(Inst.InstModsDir))
				Directory.CreateDirectory(Inst.InstModsDir);
			
			watcher = new FileSystemWatcher(Inst.InstModsDir);
			watcher.Changed += FileChanged;
			watcher.Deleted += FileChanged;
			watcher.Created += FileChanged;
			watcher.Renamed += FileRenamed;
			watcher.IncludeSubdirectories = true;
			watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite |
				NotifyFilters.FileName | NotifyFilters.DirectoryName;
			
			watcher.EnableRaisingEvents = true;
		}
		
		public void Dispose()
		{
			watcher.EnableRaisingEvents = false;
			watcher.Dispose();
		}
		
		void FileChanged(object sender, FileSystemEventArgs e)
		{
			string filePath = OSUtils.GetRelativePath(e.FullPath, Environment.CurrentDirectory);
			Console.WriteLine(filePath);
			switch (e.ChangeType)
			{
			case WatcherChangeTypes.Created:
				RecursiveAdd(filePath);
				Save();
				break;
			case WatcherChangeTypes.Deleted:
				Remove(filePath);
				Save();
				break;
			default:
				break;
			}
		}
		
		void FileRenamed(object sender, RenamedEventArgs e)
		{
			string oldPath = OSUtils.GetRelativePath(e.OldFullPath, 
			                                         Path.GetFullPath(Environment.CurrentDirectory));
			string path = 
				OSUtils.GetRelativePath(e.FullPath, Path.GetFullPath(
					Path.GetFullPath(Environment.CurrentDirectory)));
			int index = this[oldPath];
			this[index] = path;
			Save();
		}

		public Instance Inst
		{
			get;
			private set;
		}
		
		public string this[int i]
		{
			get  { return modList[i]; }
			private set { modList[i] = value; }
		}
		
		public int this[string s]
		{
			get { return modList.IndexOf(s); }
			set
			{
				if (!modList.Contains(s))
					throw new KeyNotFoundException("Can't change index of something " +
					                               "that isn't in the list!");
				Remove(s);
				modList.Insert(value, s);
			}
		}

		public void InsertMod(string file, int index)
		{
			RecursiveAdd(file, false, index);
		}
		
		public void Load()
		{
			modList.Clear();
			if (File.Exists(Inst.ModListFile))
			{
				foreach (string modFile in File.ReadAllLines(Inst.ModListFile))
				{
					Add(Path.Combine(Inst.InstModsDir, modFile), false);
				}
			}
		}
		
		public void Save()
		{
			try
			{
				List<string> writeList = new List<string>();
				writeList.AddRange(modList);
				for (int i = 0; i < writeList.Count; i++)
				{
					writeList[i] = 
						OSUtils.GetRelativePath(writeList[i], Path.GetFullPath(Inst.InstModsDir));
				}
				File.WriteAllLines(Inst.ModListFile, writeList);
			} catch (IOException e)
			{
				if (e.Message.ToLower().Contains("in use"))
				{
					Console.WriteLine("Failed to save mod list because " +
					                  "something else was using the file.");
				}
			}
		}
		
		public void Update()
		{
			Load();
			if (Directory.Exists(Inst.InstModsDir))
			{
				for (int i = 0; i < modList.Count; i++)
				{
					if (!File.Exists(modList[i]))
					{
						Remove(modList[i]);
						i--;
					}
				}
				RecursiveAdd(Inst.InstModsDir);
			}
			Save();
		}
		
		private void RecursiveAdd(string dir, bool triggerEvents = true, int index = -1)
		{
			if (File.Exists(dir) && !Directory.Exists(dir))
			{
				if (!modList.Contains(dir))
				{
					if (index == -1)
						Add(dir, triggerEvents);
					else
						Insert(index, dir, triggerEvents);
				}
				return;
			}

			foreach (string modFile in Directory.GetFileSystemEntries(dir))
			{
				if (Directory.Exists(modFile))
				{
					RecursiveAdd(Path.Combine(dir, Path.GetFileName(modFile)), triggerEvents, index);
				}
				else if (File.Exists(modFile))
				{
					if (!modList.Contains(modFile))
					{
						if (index == -1)
							Add(modFile, triggerEvents);
						else
							Insert(index, modFile, triggerEvents);
					}
				}
			}
		}

		#region Add and Remove

		private void Add(string modFile, bool triggerEvent = true)
		{
			if (!modList.Contains(modFile))
				modList.Add(modFile);
			if (triggerEvent) 
				OnModFileChanged(ModFileChangeTypes.ADDED, modFile);
		}

		private void Insert(int index, string modFile, bool triggerEvent = true)
		{
			if (!modList.Contains(modFile))
				modList.Insert(index, modFile);
			if (triggerEvent)
				OnModFileChanged(ModFileChangeTypes.ADDED, modFile);
		}

		private void Remove(string modFile, bool triggerEvent = true)
		{
			if (modList.Contains(modFile))
				modList.Remove(modFile);
			if (triggerEvent)
				OnModFileChanged(ModFileChangeTypes.REMOVED, modFile);
		}

		#endregion
		
		public IEnumerator<string> GetEnumerator()
		{
			return modList.GetEnumerator();
		}
		
		IEnumerator IEnumerable.GetEnumerator()
		{
			return modList.GetEnumerator();
		}
		
		public void OnModFileChanged(ModFileChangeTypes type, string modFile)
		{
			DebugUtils.Print("File {0} was changed ({1})", modFile, type.ToString());
			Inst.NeedsRebuild = true;
			if (ModFileChanged != null)
				ModFileChanged(this, new ModFileChangedEventArgs(type, modFile));
		}
		
		public event EventHandler<ModFileChangedEventArgs> ModFileChanged;
	}
	
	#region Event Args
	
	public class ModFileChangedEventArgs : EventArgs
	{
		public ModFileChangedEventArgs(ModFileChangeTypes type, string modFile)
		{
			ChangeType = type;
			ModFile = modFile;
		}
		
		public ModFileChangeTypes ChangeType
		{
			get;
			protected set;
		}
		
		public string ModFile
		{
			get;
			protected set;
		}
	}
	
	public enum ModFileChangeTypes
	{
		ADDED,
		REMOVED,
		RENAMED,
		OTHER
	}

	public class InstQuitEventArgs : EventArgs
	{
		public InstQuitEventArgs(int exitVal, DateTime quitTime)
		{
			ExitCode = exitVal;
			QuitTime = quitTime;
		}
			
		public DateTime QuitTime
		{
			get;
			protected set;
		}
			
		public int ExitCode
		{
			get;
			protected set;
		}
	}
	
	#endregion
	
	/// <summary>
	/// Thrown when trying to load an instance that is not valid
	/// </summary>
	[Serializable]
	class InvalidInstanceException : Exception
	{
		public InvalidInstanceException(string msg)
			: base(msg)
		{

		}
	}
}
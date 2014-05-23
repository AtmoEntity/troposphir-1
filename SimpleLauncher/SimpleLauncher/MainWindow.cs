//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18444
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Security.Cryptography;
using Gdk;

namespace SimpleLauncher
{
	public partial class MainWindow : Gtk.Window
	{
		String AtmosphirPath = Directory.GetCurrentDirectory();//"C:\\Adam\\Atmosphir\\Atmosphir\\";
		public MainWindow () : 
				base(Gtk.WindowType.Toplevel)
		{
			this.Build ();

			Color c = new Color(255,255,255);
			labelStatus.ModifyFg(Gtk.StateType.Normal, c);
			labelVersion.ModifyFg(Gtk.StateType.Normal, c);

			String serverPath = "http://onemoreblock.com/Atmosphir/";

			if(File.Exists ("launcher_request.txt"))
			{
				serverPath = File.ReadAllLines ("launcher_request.txt")[0];
			}

			Thread th = new Thread(new ThreadStart(() =>
			{
				ServerConnector serv = new ServerConnector(serverPath);
				SetStatus ("Checking for updates...");
				if(CheckVersions(serv)) // the version is out of date! (or some other trigger that requires an update)
				{
					SetStatus ("Downloading update...");
					CheckHashesAndUpdateOutdatedFiles(serv);
				}
				SetStatus ("Starting game...");

				StartGame();
			}));
			th.Start();
		}

		public Dictionary<String, String> ParseVersions(String[] lines)
		{
			Dictionary<String, String> ret = new Dictionary<string, string>();
			foreach(String line in lines)
			{
				String[] parts = line.Split(new char[] {':'});
				if(parts.Length < 2) continue; // Someone didn't format the file correctly...
				ret.Add(parts[0], parts[1]);
			}
			return ret;
		}

		public void StartGame()
		{
			Process atmosphirProcess = new Process();
			String p = System.IO.Path.Combine(AtmosphirPath, "Atmosphir_Data\\Atmosphir.exe");
			Console.WriteLine (p);
			atmosphirProcess.StartInfo.UseShellExecute = false;
			atmosphirProcess.StartInfo.FileName = p;
			atmosphirProcess.StartInfo.Arguments = "standalone";
//			atmosphirProcess.StartInfo.WorkingDirectory = System.IO.Path.Combine(AtmosphirPath, "Atmosphir_Data\\");
			Process.Start (System.IO.Path.Combine(AtmosphirPath, "Atmosphir_Data\\Atmosphir.exe"), "standalone");
//			atmosphirProcess.Start();

			Gtk.Application.Quit ();
		}

		public void Debug(String s)
		{
			Console.WriteLine (s);
		}

		public void SetStatus(String status)
		{
			Gtk.Application.Invoke ((o, e) =>
            {
				labelStatus.Text = status;
			});
		}

		public void SetProgress(double value, double max)
		{
			Gtk.Application.Invoke ((o, e) =>
            {
				double v = (value / max);
				Debug((v*100) + "%");
				progressbar1.Text = (int)(v*100) + "%";
				progressbar1.Fraction = v;
			});
		}

		public Boolean CheckVersions(ServerConnector serv)
		{
			Debug("Checking versions.");

			// See how the server's version.txt compares to the local one
			String[] serverVersions_ = serv.Request("updates/game/versions.txt").Replace("\r\n", "\n") // You can never be too safe
																	       .Split(new char[] {'\n'});
			Dictionary<String, String> serverVersions = ParseVersions(serverVersions_);

			String[] clientVersions_ = null;
			try
			{
				clientVersions_ = File.ReadAllLines(System.IO.Path.Combine (AtmosphirPath, "versions.txt"));
			}
			catch(FileNotFoundException)
			{
				Debug ("No versions.txt found.");
				return true;
			}
			Dictionary<String, String> clientVersions = ParseVersions(clientVersions_);

			foreach(KeyValuePair<String, String> kvp in serverVersions)
			{
				// kvp.Key = "launcher" or "game"
				// kvp.Value = "0.1.0" or "2.1.1" etc...
				String serverVersion = kvp.Value;

				if(clientVersions.ContainsKey(kvp.Key))
				{
					String clientVersion = clientVersions[kvp.Key];

					if(VersionNeedsUpdate (serverVersion, clientVersion))
					{
						// at least one thing is out of date... go ahead and update!
						Debug (kvp.Key + " on the server is " + serverVersion + ", on the client is " + clientVersion + ".");
						return true;
					}
				}
				else
				{
					Debug (kvp.Key + " wasn't found on the client.");
					return true; // Versions file has been tampered with... Needs update!
				}
			}

			Debug ("Client is up to date.");
			return false;
		} 

		public Boolean VersionNeedsUpdate(String serverVersion, String localVersion)
		{
			String[] server = serverVersion.Split (new char[] {'.'});
			String[] local  = localVersion.Split (new char[] {'.'});

			for(int i = 0; i < server.Length && i < local.Length; i++)
			{
				try
				{
					int serverNo = Int32.Parse(server[i]);
					int localNo = Int32.Parse(local[i]);

					if(localNo > serverNo) // The local version is more up to date? Preposterous! Update.
					{
						return true;
					}
					else if(localNo < serverNo) // The local version is out of date! We must share our beautiful work...
					{
						return true;					
					}
					else if(localNo == serverNo) // Well they have the latest version...
					{
						//return true; // MAKE THEM UPDATE ANYWAY
						return false; // jk
					}
				}
				catch(FormatException)
				{
					return false; // Invalid version format (lets hope it was the local version!)
				}
			}

			return false; // equal
		}

		public void UpdateFile(String path, ServerConnector serv)
		{
			try
			{
			//	if(!path.EndsWith(".txt")) return;

				Console.WriteLine ("Downloading file: " + path);

				String dir = System.IO.Path.GetDirectoryName(path);
				if(dir.Trim().Length > 0)
					Directory.CreateDirectory(dir); // ensure directories exist

				serv.Download("updates/game/" + path, System.IO.Path.Combine (AtmosphirPath, path)); // download file
			}
			catch(Exception)
			{
				Console.WriteLine ("Error in UpdateFile: Couldn't download file: " + path);
			}
		}

		public void CheckHashesAndUpdateOutdatedFiles(ServerConnector serv)
		{
			Debug ("Checking hashes.");

			// download hashes
			String[] lines = serv.Request("updates/hashes.txt").Split(new char[]{'\x1e'});

			List<String> download = new List<String>();

			int upToDate = 0;
			int needsUpdating = 0;

			foreach(String line in lines)
			{
				//if(!line.Contains(".txt"))continue;

				SetProgress(upToDate + needsUpdating, lines.Length);

				String[] parts = line.Split(new char[] {'\x1f'});
				
				String path = parts[0];
				String hash = parts[1];

				String hashFile = System.IO.Path.Combine (AtmosphirPath, path);
				String localHash = "";

				if(File.Exists (hashFile)) // If the file exists, hash it. If not, the file will always be downloaded.
					localHash = HashFile(hashFile);

				if(localHash.Equals(hash))
				{
					// file is up to date
					upToDate++;
				}
				else
				{
					// file needs to be updated
					UpdateFile(path, serv);
					needsUpdating++;
				}
			}		
			Debug (upToDate + " files up to date.");
			Debug (needsUpdating + " files updated.");
		}

		public String HashFile(String filename)
		{
			using (var md5 = MD5.Create())
			{
				using (var stream = File.OpenRead(filename))
				{
					return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-","").ToLower();
				}
			}
		}
	}
}


using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using System;
using System.Diagnostics;
using System.IO;
using MediaBrowser.Model.Diagnostics;
using MediaBrowser.Model.System;

namespace MediaBrowser.IsoMounter
{
	internal class LinuxMount : IIsoMount
	{
		private readonly string _umountELF;
		private readonly string _sudoELF;

		public string IsoPath { get; internal set; }

		public string MountedPath { get; internal set; }

		private readonly LinuxIsoManager _isoManager;

		private ILogger Logger { get; set; }
	    private readonly IFileSystem _fileSystem;
	    private IEnvironmentInfo _environment;
        private readonly IProcessFactory _processFactory;

        internal LinuxMount(string mountFolder, string isoPath, LinuxIsoManager isoManager, ILogger logger, IFileSystem fileSystem, IEnvironmentInfo environment, IProcessFactory processFactory, string umount, string sudo)
		{
			IsoPath = isoPath;
			_isoManager = isoManager;
			Logger = logger;

			MountedPath = mountFolder;
			_umountELF = umount;
			_sudoELF = sudo;
            _processFactory = processFactory;
            _environment = environment;
		    _fileSystem = fileSystem;

		    Logger.Info("{0} mounted to {1}", IsoPath, MountedPath);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool dispose)
		{
			UnMount();
		}

		private void UnMount()
		{
			Logger.Info("Unmounting {0}...", MountedPath);

			_isoManager.OnUnmount(this);

			string cmdFilename = _sudoELF;
			string cmdArguments = string.Format("\"{0}\" \"{1}\"", _umountELF, MountedPath);

			if (LinuxIsoManager.GetUid(_environment) == 0)
			{
				cmdFilename = _umountELF;
				cmdArguments = string.Format("\"{0}\"", MountedPath);
			}

		    var process = _processFactory.Create(new ProcessOptions
		    {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                FileName = cmdFilename,
                Arguments = cmdArguments,
                IsHidden = true,
                ErrorDialog = false,
                EnableRaisingEvents = true
            });

			Logger.Debug("{0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);

			StreamReader outputReader = null;
			StreamReader errorReader = null;

			try
			{
				process.Start();
				outputReader = process.StandardOutput;
				errorReader = process.StandardError;
				Logger.Debug("Unmount StdOut: " + outputReader.ReadLine());
				Logger.Debug("Unmount StdErr: " + errorReader.ReadLine());
			}
			catch (Exception)
			{
				throw new IOException("Unable to unmount path " + MountedPath);
				//TODO: Retry with -f
			}

			if (process.ExitCode != 0)
			{
				throw new IOException("Unable to unmount path " + MountedPath);
			}

			try
			{
                _fileSystem.DeleteDirectory(MountedPath, false);
			}
			catch (Exception)
			{
				throw new IOException("Unable to delete mount point " + MountedPath);
			}
		}
	}
}


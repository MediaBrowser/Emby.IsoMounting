using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Diagnostics;
using MediaBrowser.Model.System;

namespace MediaBrowser.IsoMounter
{
	public class LinuxIsoManager : IIsoMounter
	{
		private readonly string _tmpPath;
		private readonly string _mountELFName;
		private readonly string _umountELFName;
		private readonly string _sudoELFName;

		private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IEnvironmentInfo _environment;
	    private readonly IProcessFactory _processFactory;

        public LinuxIsoManager(ILogger logger, IFileSystem fileSystem, IEnvironmentInfo environment, IProcessFactory processFactory)
		{
			_logger = logger;
            _fileSystem = fileSystem;
            _environment = environment;
            _processFactory = processFactory;
            _tmpPath = _fileSystem.DirectorySeparatorChar + "tmp" + _fileSystem.DirectorySeparatorChar + "mediabrowser";
			_mountELFName = "mount";
			_umountELFName = "umount";
			_sudoELFName = "sudo";
		}

		public string Name
		{
			get { return "LinuxMount"; }
		}

		public bool RequiresInstallation
		{
			get
			{
				return false;
			}
		}

		public bool IsInstalled
		{
			get
			{
				return true;
			}
		}

		public Task Install(CancellationToken cancellationToken)
		{
			//TODO Clean up task(Remove mount point from previous mb3 run)
			return Task.FromResult(false);
		}

		private string GetELFPath(string name)
		{

			foreach (string test in (_environment.GetEnvironmentVariable("PATH") ?? "").Split(_fileSystem.PathSeparator))
			{

				string path = test.Trim();

				if (!String.IsNullOrEmpty(path) && _fileSystem.FileExists(path = Path.Combine(path, name)))
				{
					return _fileSystem.GetFullPath(path);
				}

			}

			throw new IOException("Missing "+name+". Unable to continue");

        }

        internal static int GetUid(IEnvironmentInfo environmentInfo)
        {
            var uidString = environmentInfo.GetUserId();
            int uid;
            if (string.IsNullOrWhiteSpace(uidString) || !int.TryParse(uidString, out uid))
            {
                uid = 0;
            }

            return uid;
        }

        public async Task<IIsoMount> Mount(string isoPath, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(isoPath))
			{
				throw new ArgumentNullException("isoPath");
			}

			var mountELF = GetELFPath(_mountELFName);
			var umountELF = GetELFPath(_umountELFName);
			var sudoELF = GetELFPath(_sudoELFName);

			string mountFolder = Path.Combine(_tmpPath, Guid.NewGuid().ToString());

			_logger.Debug("Creating mount point {0}", mountFolder);
			try
			{
                _fileSystem.CreateDirectory(mountFolder);
			}
			catch (UnauthorizedAccessException)
			{
				throw new IOException("Unable to create mount point(Permission denied) for " + isoPath);
			}
			catch (Exception)
			{
				throw new IOException("Unable to create mount point for " + isoPath);
			}

			_logger.Info("Mounting {0}...", isoPath);

			string cmdFilename = sudoELF;
			string cmdArguments = string.Format("\"{0}\" \"{1}\" \"{2}\"", mountELF, isoPath, mountFolder);

			if (GetUid(_environment) == 0)
			{
				cmdFilename = mountELF;
				cmdArguments = string.Format("\"{0}\" \"{1}\"", isoPath, mountFolder);
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

			_logger.Debug("{0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);

			StreamReader outputReader = null;
			StreamReader errorReader = null;
			try
			{
				process.Start();
				outputReader = process.StandardOutput;
				errorReader = process.StandardError;
				_logger.Debug("Mount StdOut: " + outputReader.ReadLine());
				_logger.Debug("Mount StdErr: " +errorReader.ReadLine());
			}
			catch (Exception)
			{
				try
				{
                    _fileSystem.DeleteDirectory(mountFolder, false);
				}
				catch (Exception)
				{
					throw new IOException("Unable to delete mount point " + mountFolder);
				}
				throw new IOException("Unable to mount file " + isoPath);
			}

			if (process.ExitCode == 0)
			{
				return new LinuxMount(mountFolder, isoPath, this, _logger, _fileSystem, _environment, _processFactory, umountELF, sudoELF);
			}

            try
            {
                _fileSystem.DeleteDirectory(mountFolder, false);
            }
            catch (Exception)
            {
                throw new IOException("Unable to delete mount point " + mountFolder);
            }
            throw new IOException("Unable to mount file " + isoPath);
        }

        public void Dispose()
		{
			Dispose(true);
		}

		protected virtual void Dispose(bool dispose)
		{
			if (dispose)
			{
				_logger.Info("Disposing LinuxMount");
			}
		}

		public bool CanMount(string path)
		{
		    if (_environment.OperatingSystem == OperatingSystem.Linux)
			{
				return string.Equals(Path.GetExtension(path), ".iso", StringComparison.OrdinalIgnoreCase);
			}

		    return false;
		}

	    internal void OnUnmount(LinuxMount mount)
		{
		}

		// From mono/mcs/class/Managed.Windows.Forms/System.Windows.Forms/XplatUI.cs
		[DllImport ("libc")]
		private static extern int uname (IntPtr buf);

		private static bool IsRunningOnMac()
		{
			IntPtr buf = IntPtr.Zero;
			try {
				buf = Marshal.AllocHGlobal (8192);
				// This is a hacktastic way of getting sysname from uname ()
				if (uname (buf) == 0) {
					string os = Marshal.PtrToStringAnsi (buf);
					if (os == "Darwin")
						return true;
				}
			} catch {
			} finally {
				if (buf != IntPtr.Zero)
					Marshal.FreeHGlobal (buf);
			}
			return false;
		}
	}
}


using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Diagnostics;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.System;
using System.Runtime.InteropServices;
using MediaBrowser.Controller.MediaEncoding;

namespace IsoMounter
{
    public class LinuxIsoManager : IMediaMounter
    {
        [DllImport("libc", SetLastError = true)]
        public static extern uint getuid();

        #region Private Fields

        private readonly IEnvironmentInfo EnvironmentInfo;
        private readonly bool ExecutablesAvailable;
        private readonly IFileSystem FileSystem;
        private readonly ILogger Logger;
        private readonly string MountCommand;
        private readonly string MountPointRoot;
        private readonly IProcessFactory ProcessFactory;
        private readonly string SudoCommand;
        private readonly string UmountCommand;
        private readonly IMediaEncoder mediaEncoder;

        #endregion

        #region Constructor(s)

        public LinuxIsoManager(ILogger logger, IFileSystem fileSystem, IEnvironmentInfo environment, IProcessFactory processFactory, IMediaEncoder mediaEncoder)
        {

            EnvironmentInfo = environment;
            FileSystem = fileSystem;
            Logger = logger;
            ProcessFactory = processFactory;
            this.mediaEncoder = mediaEncoder;

            MountPointRoot = FileSystem.DirectorySeparatorChar + "tmp" + FileSystem.DirectorySeparatorChar + "Emby";

            Logger.Debug(
                "[{0}] System PATH is currently set to [{1}].",
                Name,
                EnvironmentInfo.GetEnvironmentVariable("PATH") ?? ""
            );

            Logger.Debug(
                "[{0}] System path separator is [{1}].",
                Name,
                EnvironmentInfo.PathSeparator
            );

            Logger.Debug(
                "[{0}] Mount point root is [{1}].",
                Name,
                MountPointRoot
            );

            //
            // Get the location of the executables we need to support mounting/unmounting ISO images.
            //

            SudoCommand = GetFullPathForExecutable("sudo");

            Logger.Info(
                "[{0}] Using version of [sudo] located at [{1}].",
                Name,
                SudoCommand
            );

            MountCommand = GetFullPathForExecutable("mount");

            Logger.Info(
                "[{0}] Using version of [mount] located at [{1}].",
                Name,
                MountCommand
            );

            UmountCommand = GetFullPathForExecutable("umount");

            Logger.Info(
                "[{0}] Using version of [umount] located at [{1}].",
                Name,
                UmountCommand
            );

            if (!String.IsNullOrEmpty(SudoCommand) && !String.IsNullOrEmpty(MountCommand) && !String.IsNullOrEmpty(UmountCommand))
            {
                ExecutablesAvailable = true;
            }
            else
            {
                ExecutablesAvailable = false;
            }

        }

        #endregion

        #region Interface Implementation for IIsoMounter

        public bool IsInstalled
        {
            get
            {
                return true;
            }
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

        public bool CanMount(ReadOnlySpan<char> path, ReadOnlySpan<char> container)
        {

            if (EnvironmentInfo.OperatingSystem == MediaBrowser.Model.System.OperatingSystem.Linux)
            {
                var extension = Path.GetExtension(path.ToString());

                if (ExecutablesAvailable)
                {
                    return string.Equals(extension, ".iso", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

        }

        public Task Install(CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<IMediaMount> Mount(ReadOnlyMemory<char> isoPath, ReadOnlyMemory<char> container, CancellationToken cancellationToken)
        {

            LinuxMount mountedISO;

            if (MountISO(isoPath.ToString(), container.ToString(), out mountedISO))
            {

                return Task.FromResult<IMediaMount>(mountedISO);

            }
            else
            {

                throw new IOException(String.Format(
                    "An error occurred trying to mount image [$0].",
                    isoPath
                ));

            }

        }

        #endregion

        #region Interface Implementation for IDisposable

        // Flag: Has Dispose already been called?
        private bool disposed = false;

        public void Dispose()
        {

            // Dispose of unmanaged resources.
            Dispose(true);

            // Suppress finalization.
            GC.SuppressFinalize(this);

        }

        protected virtual void Dispose(bool disposing)
        {

            if (disposed)
            {
                return;
            }

            Logger.Info(
                "[{0}] Disposing [{1}].",
                Name,
                disposing.ToString()
            );

            if (disposing)
            {

                //
                // Free managed objects here.
                //

            }

            //
            // Free any unmanaged objects here.
            //

            disposed = true;

        }

        #endregion

        #region Private Methods

        private string GetFullPathForExecutable(string name)
        {

            foreach (string test in (EnvironmentInfo.GetEnvironmentVariable("PATH") ?? "").Split(EnvironmentInfo.PathSeparator))
            {

                string path = test.Trim();

                if (!String.IsNullOrEmpty(path) && FileSystem.FileExists(path = Path.Combine(path, name)))
                {
                    return FileSystem.GetFullPath(path);
                }

            }

            return String.Empty;

        }

        private uint GetUID()
        {

            var uid = getuid();

            Logger.Debug(
                "[{0}] Our current UID is [{1}], GetUserId() returned [{2}].",
                Name,
                uid.ToString(),
                uid
            );

            return uid;

        }

        private bool ExecuteCommand(string cmdFilename, string cmdArguments)
        {

            bool processFailed = false;

            var process = ProcessFactory.Create(
                new ProcessOptions
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
                }
            );

            try
            {

                process.Start();

                //StreamReader outputReader = process.StandardOutput.;
                //StreamReader errorReader = process.StandardError;

                Logger.Debug(
                    "[{0}] Standard output from process is [{1}].",
                    Name,
                    process.StandardOutput.ReadToEnd()
                );

                Logger.Debug(
                    "[{0}] Standard error from process is [{1}].",
                    Name,
                    process.StandardError.ReadToEnd()
                );

            }
            catch (Exception ex)
            {

                processFailed = true;

                Logger.Debug(
                    "[{0}] Unhandled exception executing command, exception is [{1}].",
                    Name,
                    ex.Message
                );

            }

            if (!processFailed && process.ExitCode == 0)
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        private bool MountISO(string isoPath, string container, out LinuxMount mountedISO)
        {

            string cmdArguments;
            string cmdFilename;
            string mountPoint = Path.Combine(MountPointRoot, Guid.NewGuid().ToString());

            if (!string.IsNullOrEmpty(isoPath))
            {

                Logger.Info(
                    "[{0}] Attempting to mount [{1}].",
                    Name,
                    isoPath
                );

                Logger.Debug(
                    "[{0}] ISO will be mounted at [{1}].",
                    Name,
                    mountPoint
                );

            }
            else
            {

                throw new ArgumentNullException(nameof(isoPath));

            }

            try
            {
                FileSystem.CreateDirectory(mountPoint);
            }
            catch (UnauthorizedAccessException)
            {
                throw new IOException("Unable to create mount point(Permission denied) for " + isoPath);
            }
            catch (Exception)
            {
                throw new IOException("Unable to create mount point for " + isoPath);
            }

            if (GetUID() == 0)
            {
                cmdFilename = MountCommand;
                cmdArguments = string.Format("\"{0}\" \"{1}\"", isoPath, mountPoint);
            }
            else
            {
                cmdFilename = SudoCommand;
                cmdArguments = string.Format("\"{0}\" \"{1}\" \"{2}\"", MountCommand, isoPath, mountPoint);
            }

            Logger.Debug(
                "[{0}] Mount command [{1}], mount arguments [{2}].",
                Name,
                cmdFilename,
                cmdArguments
            );

            if (ExecuteCommand(cmdFilename, cmdArguments))
            {

                Logger.Info(
                    "[{0}] ISO mount completed successfully.",
                    Name
                );

                mountedISO = new LinuxMount(this, mediaEncoder, isoPath, mountPoint, container);

            }
            else
            {

                Logger.Info(
                    "[{0}] ISO mount completed with errors.",
                    Name
                );

                try
                {

                    FileSystem.DeleteDirectory(mountPoint, false);

                }
                catch (Exception ex)
                {

                    Logger.Info(
                        "[{0}] Unhandled exception removing mount point, exception is [{1}].",
                        Name,
                        ex.Message
                    );

                }

                mountedISO = null;

            }

            return mountedISO != null;

        }

        private void UnmountISO(LinuxMount mount)
        {

            string cmdArguments;
            string cmdFilename;

            if (mount != null)
            {

                Logger.Info(
                    "[{0}] Attempting to unmount ISO [{1}] mounted on [{2}].",
                    Name,
                    mount.IsoPath,
                    mount.MountedFolderPath
                );

            }
            else
            {

                throw new ArgumentNullException(nameof(mount));

            }

            if (GetUID() == 0)
            {
                cmdFilename = UmountCommand;
                cmdArguments = string.Format("\"{0}\"", mount.MountedFolderPath);
            }
            else
            {
                cmdFilename = SudoCommand;
                cmdArguments = string.Format("\"{0}\" \"{1}\"", UmountCommand, mount.MountedFolderPath);
            }

            Logger.Debug(
                "[{0}] Umount command [{1}], umount arguments [{2}].",
                Name,
                cmdFilename,
                cmdArguments
            );

            if (ExecuteCommand(cmdFilename, cmdArguments))
            {

                Logger.Info(
                    "[{0}] ISO unmount completed successfully.",
                    Name
                );

            }
            else
            {

                Logger.Info(
                    "[{0}] ISO unmount completed with errors.",
                    Name
                );

            }

            try
            {

                FileSystem.DeleteDirectory(mount.MountedFolderPath, false);

            }
            catch (Exception ex)
            {

                Logger.Info(
                    "[{0}] Unhandled exception removing mount point, exception is [{1}].",
                    Name,
                    ex.Message
                );

            }

        }

        #endregion

        #region Internal Methods

        internal void OnUnmount(LinuxMount mount)
        {

            UnmountISO(mount);

        }

        #endregion

    }

}


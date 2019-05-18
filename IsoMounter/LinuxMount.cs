using System;
using MediaBrowser.Model.Diagnostics;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.MediaEncoding;

namespace IsoMounter
{
    internal class LinuxMount : IMediaMount
    {

        #region Private Fields

        private readonly LinuxIsoManager linuxIsoManager;
        private readonly IMediaEncoder mediaEncoder;

        #endregion

        #region Constructor(s)

        internal LinuxMount(LinuxIsoManager isoManager, IMediaEncoder mediaEncoder, string isoPath, string mountFolder, string container)
        {

            linuxIsoManager = isoManager;
            this.mediaEncoder = mediaEncoder;

            IsoPath = isoPath;
            MountedPath = mountFolder;
            MountedFolderPath = mountFolder;
            MountedProtocol = MediaProtocol.File;

            if (string.Equals(container, MediaContainer.DvdIso.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var files = mediaEncoder.GetDvdVobFiles(mountFolder);

                var mountedPath = string.Join("|", files);
            }
            else if (string.Equals(container, MediaContainer.BlurayIso.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var files = mediaEncoder.GetBlurayM2tsFiles(mountFolder);

                var mountedPath = string.Join("|", files);
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

            if (disposed) {
                return;
            }
            
            if (disposing) {

                //
                // Free managed objects here.
                //

                linuxIsoManager.OnUnmount(this);

            }

            //
            // Free any unmanaged objects here.
            //

            disposed = true;

        }

        #endregion

        #region Interface Implementation for IIsoMount

        public string IsoPath { get; private set; }
        public string MountedPath { get; private set; }
        public string MountedFolderPath { get; private set; }
        public MediaProtocol MountedProtocol { get; set; }

        #endregion

    }

}


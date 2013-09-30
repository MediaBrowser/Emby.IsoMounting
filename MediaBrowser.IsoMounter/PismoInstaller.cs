using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.IsoMounter
{
    public class PismoInstaller
    {
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _appPaths;
        private readonly ILogger _logger;
        private readonly IZipClient _zipClient;
        
        private readonly string[] _installUrls = new[]
                {
                    "https://github.com/MediaBrowser/MediaBrowser.Resources/raw/master/Pismo-Install/pfm-168-mediabrowser-win.zip",

                    "https://www.dropbox.com/s/kinern16sd3mtag/pfm-168-mediabrowser-win.zip?dl=1"
                };

        public PismoInstaller(IHttpClient httpClient, ILogger logger, IApplicationPaths appPaths, IZipClient zipClient)
        {
            _httpClient = httpClient;
            _logger = logger;
            _appPaths = appPaths;
            _zipClient = zipClient;
        }

        /// <summary>
        /// Installs this instance.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Install(CancellationToken cancellationToken)
        {
            var tempFile = await GetTempFile(cancellationToken).ConfigureAwait(false);

            if (tempFile == null)
            {
                throw new ApplicationException("Failed to install Pismo");
            }

            Extract(tempFile);
        }

        private void Extract(string tempFile)
        {
            _logger.Debug("Extracting Pismo from {0}", tempFile);

            var tempFolder = Path.Combine(_appPaths.TempDirectory, Guid.NewGuid().ToString());

            if (!Directory.Exists(tempFolder))
            {
                Directory.CreateDirectory(tempFolder);
            }

            _zipClient.ExtractAll(tempFile, tempFolder, true);

            var file = Directory.EnumerateFiles(tempFolder, "pfminst.exe", SearchOption.AllDirectories)
                .First();

            var processStartInfo = new ProcessStartInfo
            {
                Arguments = "install",
                FileName = file,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using (var process = Process.Start(processStartInfo))
            {
                process.WaitForExit();
            }
        }

        /// <summary>
        /// Gets the temp file.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{System.String}.</returns>
        private async Task<string> GetTempFile(CancellationToken cancellationToken)
        {
            foreach (var url in _installUrls)
            {
                try
                {
                    return await _httpClient.GetTempFile(new HttpRequestOptions
                    {
                        Url = url,
                        CancellationToken = cancellationToken,
                        Progress = new Progress<double>(),

                        // Make it look like a browser
                        // Try to hide that we're direct linking
                        UserAgent = "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/30.0.1599.47 Safari/537.36"

                    }).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error downloading from {0}", ex, url);
                }
            }

            return null;
        }
    }
}

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace StyleCopForVS
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("StyleCop Classic for Visual Studio", "StyleCop Classic 4.7 editor diagnostics", "0.0.1")]
    [Guid("6CBF7F9D-3292-45D1-B80A-5F8B9A05D7AD")]
    public sealed class StyleCopForVSPackage : AsyncPackage
    {
        protected override Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            return Task.CompletedTask;
        }
    }
}

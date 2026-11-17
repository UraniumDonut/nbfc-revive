using StagWare.FanControl.Plugins;
using StagWare.Hardware.LPC;
using StagWare.Hardware;
using System.ComponentModel.Composition;

namespace StagWare.Plugins
{
    [Export(typeof(IEmbeddedController))]
    [FanControlPluginMetadata(
        "StagWare.Plugins.ECWindows",
        SupportedPlatforms.Windows,
        SupportedCpuArchitectures.x86 | SupportedCpuArchitectures.x64,
        MinOSVersion = "5.0")]
    public class ECWindows : EmbeddedControllerBase, IEmbeddedController
    {
        #region Private Fields

        PawnIOEmbeddedController ec;

        #endregion

        #region IEmbeddedController implementation

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (!this.IsInitialized)
            {
                this.ec = new PawnIOEmbeddedController();
                this.IsInitialized = true;
            }
        }

        public bool AcquireLock(int timeout)
        {
            return this.ec.WaitEcMutex(timeout);
        }

        public void ReleaseLock()
        {
            this.ec.ReleaseEcMutex();
        }

        public void Dispose()
        {
            try
            {
                ReleaseLock();
            }
            catch
            {
            }

            if (this.ec != null)
            {
                this.ec.Dispose();
                this.ec = null;
            }

            this.IsInitialized = false;
        }

        #endregion

        #region EmbeddedControllerBase implementation

        protected override void WritePort(int port, byte value)
        {
            this.ec.WritePort(port, value);
        }

        protected override byte ReadPort(int port)
        {
            return this.ec.ReadPort(port);
        }

        #endregion
    }
}

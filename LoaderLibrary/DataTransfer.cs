using LoaderLibrary.Data;
using Microsoft.Extensions.Logging;

namespace LoaderLibrary
{
    public class DataTransfer : IDataTransfer
    {
        private readonly IWellData _wellData;
        private readonly ILogger<DataTransfer> _log;

        public DataTransfer(ILogger<DataTransfer> log, IWellData wellData)
        {
            _log = log;
            _wellData = wellData;
        }

        public async Task Transferdata(string path, string connectionString)
        {
            try
            {
                _log.LogInformation("Start Data Transfer and Copy");
                await _wellData.CopyWellbores(connectionString, path);
                _log.LogInformation("Data has been Copied");
            }
            catch (Exception ex)
            {
                string errors = "Error transferring data: " + ex.ToString();
                _log.LogError(errors);
            }
        }
    }
}

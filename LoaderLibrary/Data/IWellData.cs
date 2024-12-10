using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoaderLibrary.Data
{
    public interface IWellData
    {
        Task CopyWellbores(string connectionString, string path);
    }
}

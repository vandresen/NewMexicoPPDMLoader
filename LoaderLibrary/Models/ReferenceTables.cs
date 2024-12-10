using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoaderLibrary.Models
{
    public class ReferenceTables
    {
        public List<ReferenceTable> RefTables { get; }
        public ReferenceTables()
        {
            this.RefTables = new List<ReferenceTable>()
            {
                new ReferenceTable()
                { KeyAttribute = "STATUS", Table = "R_WELL_STATUS", ValueAttribute= "LONG_NAME"},
            };
        }
    }
}

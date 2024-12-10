using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoaderLibrary.Models
{
    public class WellHeader
    {
        public string UWI { get; set; }
        public string WELL_NAME { get; set; }
        public double? SURFACE_LONGITUDE { get; set; }
        public double? SURFACE_LATITUDE { get; set; }
        public string CURRENT_STATUS { get; set; }
        public decimal? FINAL_TD { get; set; }
        public string REMARK { get; set; }
        public DateTime? SPUD_DATE { get; set; }
    }
}

using LoaderLibrary.DataAccess;
using LoaderLibrary.Extensions;
using LoaderLibrary.Models;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace LoaderLibrary.Data
{
    public class WellData: IWellData
    {
        private readonly ILogger _log;
        private readonly IDataAccess _da;

        public WellData(ILogger<WellData> log, IDataAccess da)
        {
            _log = log;
            _da = da;
        }

        public async Task CopyWellbores(string connectionString, string path)
        {
            _log.LogInformation("Start CopyWellbores");
            DownloadDataFromWeb dlw = new DownloadDataFromWeb(_log);
            string filePath = dlw.DownloadWells();

            if (filePath == null) 
            {
                _log.LogInformation("No download file is available");
            }
            else
            {
                IEnumerable<TableSchema> tableAttributeInfo = await GetColumnInfo(connectionString, "WELL");
                List<WellHeader> wells = ConvertWellHeaderData(filePath, tableAttributeInfo);
                await SaveWellbores(wells, connectionString);
            }
            
            _log.LogInformation("End CopyWellbores");
        }

        private List<WellHeader> ConvertWellHeaderData(string filePath, IEnumerable<TableSchema> tableAttributeInfo)
        {
            TableSchema? dataProperty = tableAttributeInfo.FirstOrDefault(x => x.COLUMN_NAME == "WELL_NAME");
            int wellNameLength = dataProperty == null ? 4 : dataProperty.PRECISION;
            dataProperty = tableAttributeInfo.FirstOrDefault(x => x.COLUMN_NAME == "CURRENT_STATUS");
            int currentStatusLength = dataProperty == null ? 4 : dataProperty.PRECISION;
            dataProperty = tableAttributeInfo.FirstOrDefault(x => x.COLUMN_NAME == "REMARK");
            int remarkLength = dataProperty == null ? 4 : dataProperty.PRECISION;

            List<WellHeader> wells = new List<WellHeader>();
            int recordCount = 0;

            try
            {
                using (var parser = new TextFieldParser(filePath))
                {
                    
                    parser.TextFieldType = FieldType.Delimited;
                    parser.SetDelimiters(",");

                    if (!parser.EndOfData)
                    {
                        var headers = parser.ReadFields();
                        _log.LogInformation("Headers: " + string.Join(", ", headers));
                    }

                    while (!parser.EndOfData)
                    {
                        try
                        {
                            var fields = parser.ReadFields();
                            if (fields != null)
                            {
                                var wellHeader = new WellHeader
                                {
                                    UWI = fields[1]?.Replace("-", ""),
                                    WELL_NAME = fields[2],
                                    SURFACE_LONGITUDE = double.TryParse(fields[13], out var lon) ? lon : null,
                                    SURFACE_LATITUDE = double.TryParse(fields[14], out var lat) ? lat : null,
                                    CURRENT_STATUS = fields[4] + " " + fields[3],
                                    FINAL_TD = decimal.TryParse(fields[21], out var td) ? td : null,
                                    REMARK = fields[17] + "  " + fields[18],
                                    SPUD_DATE = ParseSpudDate(fields[19])
                                };
                                if (wellHeader.FINAL_TD != null) wellHeader.FINAL_TD = Math.Round((decimal)wellHeader.FINAL_TD, 5);
                                if (wellHeader.FINAL_TD < -99999.99999m || wellHeader.FINAL_TD > 99999.99999m)
                                {
                                    _log.LogWarning($"Final TD is outside the range: {wellHeader.FINAL_TD}");
                                    wellHeader.FINAL_TD = null;
                                }
                                wellHeader.CURRENT_STATUS = wellHeader.CURRENT_STATUS.Replace("(site released)", "").Trim();
                                if (wellHeader.WELL_NAME.Length > wellNameLength)
                                    wellHeader.WELL_NAME = wellHeader.WELL_NAME.Substring(0, wellNameLength);
                                if (wellHeader.CURRENT_STATUS.Length > currentStatusLength)
                                    wellHeader.CURRENT_STATUS = wellHeader.CURRENT_STATUS.Substring(0, currentStatusLength);
                                if (wellHeader.REMARK.Length > remarkLength)
                                    wellHeader.REMARK = wellHeader.REMARK.Substring(0, remarkLength);

                                recordCount++;
                                wells.Add(wellHeader);
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.LogWarning($"Error reading row: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogCritical($"Critical error: {ex.Message}");
                Exception error = new Exception(
                    "An error occurred: " + ex.Message
                    );
                throw error;
            }

            Console.WriteLine($"Number of records are {recordCount}");
            return wells;
        }

        private static DateTime? ParseSpudDate(string spudYearField)
        {
            if (int.TryParse(spudYearField, out var year))
            {
                if (year == 9999) return null;
                return new DateTime(year, 1, 1);
            }
            return null;
        }

        private async Task SaveWellbores(List<WellHeader> wellbores, string connectionString)
        {
            _log.LogInformation("Start SaveWellbores");
            wellbores.Where(c => string.IsNullOrEmpty(c.CURRENT_STATUS)).Select(c => { c.CURRENT_STATUS = "UNKNOWN"; return c; }).ToList();
            await SaveWellboreRefData(wellbores, connectionString);
            string sql = "IF NOT EXISTS(SELECT 1 FROM WELL WHERE UWI = @UWI) " +
                "INSERT INTO WELL (UWI, WELL_NAME, SPUD_DATE, CURRENT_STATUS,  " +
                "SURFACE_LONGITUDE, SURFACE_LATITUDE, FINAL_TD, REMARK) " +
                "VALUES(@UWI, @WELL_NAME,  @SPUD_DATE, @CURRENT_STATUS,  " +
                "@SURFACE_LONGITUDE, @SURFACE_LATITUDE, @FINAL_TD, @REMARK)";
            await _da.SaveData<IEnumerable<WellHeader>>(connectionString, wellbores, sql);
            _log.LogInformation("End SaveWellbores");
        }

        public async Task SaveWellboreRefData(List<WellHeader> wellbores, string connectionString)
        {
            Dictionary<string, List<ReferenceData>> refDict = new Dictionary<string, List<ReferenceData>>();
            ReferenceTables tables = new ReferenceTables();
            List<ReferenceData> refs = wellbores.Select(x => x.CURRENT_STATUS).Distinct().ToList().CreateReferenceDataObject();
            refDict.Add(tables.RefTables[0].Table, refs);
            foreach (var table in tables.RefTables)
            {
                refs = refDict[table.Table];
                string sql = "";
                if (table.Table == "R_WELL_STATUS")
                {
                    sql = $"IF NOT EXISTS(SELECT 1 FROM {table.Table} WHERE {table.KeyAttribute} = @Reference) " +
                        $"INSERT INTO {table.Table} " +
                        $"(STATUS_TYPE, {table.KeyAttribute}, {table.ValueAttribute}) " +
                        $"VALUES('STATUS', @Reference, @Reference)";
                }
                else
                {
                    sql = $"IF NOT EXISTS(SELECT 1 FROM {table.Table} WHERE {table.KeyAttribute} = @Reference) " +
                        $"INSERT INTO {table.Table} " +
                        $"({table.KeyAttribute}, {table.ValueAttribute}) " +
                        $"VALUES(@Reference, @Reference)";
                }
                await _da.SaveData(connectionString, refs, sql);
            }
        }

        public Task<IEnumerable<TableSchema>> GetColumnInfo(string connectionString, string table) =>
            _da.LoadData<TableSchema, dynamic>("dbo.sp_columns", new { TABLE_NAME = table }, connectionString);
    }
}

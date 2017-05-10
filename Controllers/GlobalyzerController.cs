using NLog;
using RadarBackend.Models.KPI;
using RadarBackend.Utility;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace RadarBackend.Controllers
{
    public class GlobalyzerController : ApiController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Send Globalyzer report data to the server
        /// </summary>
        /// <param name="Report">Report data in JSON format</param>
        /// <returns>
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 400 - if JSON received has no scan data
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 - if adding the Globalyzer results was successful
        /// </returns>
        /// <example>
        /// POST /api/Globalyzer/GlobalyzerResults
        /// </example>
        [HttpPost]
        public HttpResponseMessage GlobalyzerResults(GlobalyzerData Report)
        {
            if (Report == null)
            {
                string error = "Globalyzer JSON received is null or you have a syntax error - exiting";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(Report, Report);

            if (Report.Scans.Count == 0)
            {
                string error = "Globalyzer Report doesn't contain any scan data - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            GlobalyzerResult newGlobalyzerResultRecord = new GlobalyzerResult();
            newGlobalyzerResultRecord.BuildRecordId = (int)Report.BuildRecordId;
            newGlobalyzerResultRecord.GlobalyzerProjectName = Report.GlobalyzerProjectName;
            newGlobalyzerResultRecord.Url = Report.ReportURL;
            newGlobalyzerResultRecord.TotalIssues = (int)Report.TotalIssues;

            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                db.GlobalyzerResults.InsertOnSubmit(newGlobalyzerResultRecord);
                db.SubmitChanges();

                foreach (GlobalyzerScanData scanData in Report.Scans)
                {
                    // Submit scan data
                    GlobalyzerScan newGlobalyzerScanRecord = new GlobalyzerScan();
                    newGlobalyzerScanRecord.GlobalyzerResultId = newGlobalyzerResultRecord.Id;
                    newGlobalyzerScanRecord.EmbeddedStrings = (int)scanData.EmbeddedStrings;
                    newGlobalyzerScanRecord.Language = scanData.ProgrammingLanguage;
                    newGlobalyzerScanRecord.LocaleSensitiveMethods = (int)scanData.LocaleSensitiveMethods;
                    newGlobalyzerScanRecord.NumberOfFilesScanned = (int)scanData.NumberOfFilesScanned;
                    newGlobalyzerScanRecord.NumberOfLocScanned = (int)scanData.NumberOfLOCScanned;
                    newGlobalyzerScanRecord.RuleSet = scanData.RuleSet;
                    newGlobalyzerScanRecord.ScanName = scanData.ScanName;

                    db.GlobalyzerScans.InsertOnSubmit(newGlobalyzerScanRecord);
                }

                db.SubmitChanges();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Get the Globalyzer Results for the given build record ID
        /// </summary>
        /// <param name="BuildRecordId">The build record ID</param>
        /// <returns>The Globalyzer Results for the given build record ID</returns>
        [HttpGet]
        public object GlobalyzerResults(int BuildRecordId)
        {
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                return db.GlobalyzerResults.Where(l => l.BuildRecordId == BuildRecordId).Select(l => Formatters.FormatGlobalyzerResult(l));
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }
    }
}

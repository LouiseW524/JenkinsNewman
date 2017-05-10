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
    public class VerDirController : ApiController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Send VerDir scan data to the server
        /// </summary>
        /// <param name="Report">VerDir report data in JSON format</param>
        /// <returns>
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 400 - if the list of failure count is > 1 but the failure list contains no entries
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 - if adding the build step was successful
        /// </returns>
        /// <example>
        /// POST /api/VerDir/VerDirResults
        /// </example>
        [HttpPost]
        public HttpResponseMessage VerDirResults(VerDirData Report)
        {
            // Check if JSON received is valid and all properties are available
            if (Report == null)
            {
                string error = "VerDir Report JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(Report, Report);

            // Ensure we haven't received VerDir results for this build record ID before
            if(RadarDbHelper.VerDirResultsExist((int)Report.BuildRecordID))
            {
                string error = "VerDir results already exist for the specified Build Record ID: " + Report.BuildRecordID;
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            VerDirResult newVerDirRecord = new VerDirResult();
            newVerDirRecord.BuildRecordId = (int) Report.BuildRecordID;
            newVerDirRecord.BuildNumber = (int) Report.BuildNumber;
            newVerDirRecord.FileAnalyzed = (int) Report.FilesAnalyzed;
            newVerDirRecord.FailureCount = (int) Report.FailureCount;
            newVerDirRecord.Status = Report.Status;

            // Submit data to database (we need the ID of the new row to link to any info about failures)
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();

                db.VerDirResults.InsertOnSubmit(newVerDirRecord);
                db.SubmitChanges();

                // If there are failures, we need to submit this information also
                if (newVerDirRecord.FailureCount != 0)
                {
                    // Check that we actually have a list of failures
                    if (Report.FailureList == null)
                    {
                        string error = "FailureCount appears to be non-zero but FailuresList is missing from JSON.";
                        logger.Log(LogLevel.Error, error);
                        return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
                    }

                    foreach (VerDirFailureDetail verDirFailure in Report.FailureList)
                    {
                        // Inserts a new row for each failure on a file, e.g. if a file has two errors, two rows get added
                        foreach (string error in verDirFailure.Errors)
                        {
                            VerDirFailure newVerDirFailureRecord = new VerDirFailure();
                            newVerDirFailureRecord.VerDirId = newVerDirRecord.VerDirId;
                            newVerDirFailureRecord.FilePath = verDirFailure.File;
                            newVerDirFailureRecord.Error = error;

                            db.VerDirFailures.InsertOnSubmit(newVerDirFailureRecord);
                        }           
                    }

                    db.SubmitChanges();
                }

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch(Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }



        /// <summary>
        /// Get the VerDir results for the specified build
        /// </summary>
        /// <param name="BuildId">The Build ID to query</param>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON object representing the VerDir results for the Build Record ID passed if successful
        /// </returns>
        /// <example>
        /// GET /api/VerDir/VerDirResults?BuildRecordId=1
        /// </example>
        [HttpGet]
        public object VerDirResults(int BuildRecordId)
        {
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                return db.VerDirResults.Where(l => l.BuildRecordId == BuildRecordId).Select(l => Formatters.FormatVerDirResult(l));
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }

    }
}
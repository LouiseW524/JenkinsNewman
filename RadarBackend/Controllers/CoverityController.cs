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
    public class CoverityController : ApiController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Send Coverity scan data to the server
        /// </summary>
        /// <param name="Data">Coverity scan data in JSON format</param>
        /// <returns>
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 - if adding the build step was successful
        /// </returns>
        /// <example>
        /// POST /api/Coverity/CoverityResults
        /// </example>
        [HttpPost]
        public HttpResponseMessage CoverityResults(CoverityData Data)
        {
            // Validate that all necessary data is received from the endpoint
            if (Data == null)
            {
                string error = "Coverity Results JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(Data, Data);

            // Ensure we haven't received Coverity results for this build record ID before
            if (RadarDbHelper.CoverityResultsExist((int)Data.BuildRecordID))
            {
                string error = "Coverity results already exist for the specified Build Record ID: " + Data.BuildRecordID;
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            CoverityResult newCoverityRecord = new CoverityResult();
            newCoverityRecord.BuildRecordId = (int) Data.BuildRecordID;
            newCoverityRecord.FilesAnalyzed = (int) Data.FilesAnalyzed;
            newCoverityRecord.TotalLOCAnalyzed = (int) Data.TotalLOCAnalyzed;
            newCoverityRecord.FunctionsAnalyzed = (int) Data.FunctionsAnalyzed;
            newCoverityRecord.PathsAnalyzed = (int) Data.PathsAnalyzed;
            newCoverityRecord.Duration = Data.Duration;
            newCoverityRecord.URL = Data.URL;
            newCoverityRecord.NewDefectsFound = (int) Data.NewDefectsFound;
            newCoverityRecord.OutstandingDefects = (int) Data.OutstandingDefects;

            // Submit data to database (we need the ID of the new row to link to any info about failures)
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                db.CoverityResults.InsertOnSubmit(newCoverityRecord);
                db.SubmitChanges();

                // Submit failures to the database (if any sepecified)
                if( Data.DefectsList != null)
                {
                    foreach (CoverityDefect cd in Data.DefectsList)
                    {
                        CoverityFailure newCoverityFailureRecord = new CoverityFailure();
                        newCoverityFailureRecord.CoverityResultId = newCoverityRecord.CoverityResultId;
                        newCoverityFailureRecord.Cid = (int) cd.Cid;
                        newCoverityFailureRecord.Severity = cd.Severity;
                        newCoverityFailureRecord.Classification = cd.Classification;
                        newCoverityFailureRecord.Impact = cd.Impact;
                        newCoverityFailureRecord.File = cd.File;
                        newCoverityFailureRecord.Category = cd.Category;
                        newCoverityFailureRecord.Type = cd.Type;

                        db.CoverityFailures.InsertOnSubmit(newCoverityFailureRecord);
                    }

                    db.SubmitChanges();
                }

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }

        }


        /// <summary>
        /// Get the Coverity results for the specified build
        /// </summary>
        /// <param name="BuildRecordId">The Build ID to query</param>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON object representing the Coverity results for the Build Record ID passed if succesful
        /// </returns>
        /// <example>
        /// GET /api/Coverity/CoverityResults?BuildRecordId=3
        /// </example>
        [HttpGet]
        public object CoverityResults(int BuildRecordId)
        {
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                return db.CoverityResults.Where(l => l.BuildRecordId == BuildRecordId).Select(l => Formatters.FormatCoverityResult(l));
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }
    }
}

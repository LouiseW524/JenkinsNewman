using NLog;
using RadarBackend.Models.Build;
using RadarBackend.Utility;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace RadarBackend.Controllers
{
    public class QualityController : ApiController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Create a new Quality Gate
        /// </summary>
        /// <param name="QualityGateInfo">Quality Gate data in JSON format</param>
        /// <returns>
        /// HttpResponseMessage:
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 400 - if a Quality Gate with the given name already exists
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 - if adding the build step was successful
        /// </returns>
        /// <example>
        /// POST /api/Quality/QualityGate
        /// </example>
        [HttpPost]
        public HttpResponseMessage QualityGate(Quality QualityGateInfo)
        {
            // Check expected JSON is present and has all required fields
            if (QualityGateInfo == null)
            {
                string error = "Quality Gate JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(QualityGateInfo, QualityGateInfo);

            // Check if a quality gate with this name already exists and is active - if so exit
            if (RadarDbHelper.DoesQualityGateExist(QualityGateInfo.Name) && RadarDbHelper.IsQualityGateActive(QualityGateInfo.Name))
            {
                string error = "An active Quality Gate with this name already exists - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            QualityGate qualityGateRecord = new QualityGate();
            qualityGateRecord.Name = QualityGateInfo.Name;
            qualityGateRecord.Description = QualityGateInfo.Description;
            qualityGateRecord.Type = QualityGateInfo.Type;
            qualityGateRecord.Pass = (int) QualityGateInfo.Pass;
            qualityGateRecord.Fail = (int) QualityGateInfo.Fail;
            qualityGateRecord.Min = (int) QualityGateInfo.Min;
            qualityGateRecord.Max = (int) QualityGateInfo.Max;
            qualityGateRecord.Active = (int) QualityGateInfo.Active;

            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                db.QualityGates.InsertOnSubmit(qualityGateRecord);
                db.SubmitChanges();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Update a new Quality Gate
        /// </summary>
        /// <param name="QualityGateInfo">Quality Gate data in JSON format</param>
        /// <returns>
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 400 - if the given Quality Gate to update doesn't exist
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 - if adding the build step was successful
        /// </returns>
        /// <example>
        /// POST /api/Quality/QualityGateUpdate
        /// </example>
        [HttpPost]
        public HttpResponseMessage QualityGateUpdate(Quality QualityGateInfo)
        {
            // Check expected JSON is present and has all required fields
            if (QualityGateInfo == null)
            {
                string error = "Quality Gate JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(QualityGateInfo, QualityGateInfo);

            // Each time a Quality Gate is updated, we'll need to add a new row in the QualityGates table
            // We'll want to retain a record of what a Quality Gate looked like when each build was run

            // Check if a quality gate with this name already exists and is active - if not exit
            if (!RadarDbHelper.DoesQualityGateExist(QualityGateInfo.Name))
            {
                string error = "The Quality Gate specified to update doesn't seem to exist - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            try
            {
                RadarDbDataContext db = new RadarDbDataContext();

                // Make sure an existing rows relating to this QualityGate are inactive - need to retain as previous builds could have used these
                // Only one row for any QualityGate should be active at any time.
                var existingRecords = from q in db.QualityGates
                                      where QualityGateInfo.Name.ToLower().Equals(q.Name.ToLower())
                                      select q;

                foreach (QualityGate qg in existingRecords)
                {
                    qg.Active = 0;
                }

                db.SubmitChanges();

                // Add a new row
                QualityGate newQualityGateRecord = new QualityGate();

                newQualityGateRecord.Name = QualityGateInfo.Name;
                newQualityGateRecord.Description = QualityGateInfo.Description;
                newQualityGateRecord.Type = QualityGateInfo.Type;
                newQualityGateRecord.Pass = (int)QualityGateInfo.Pass;
                newQualityGateRecord.Fail = (int)QualityGateInfo.Fail;
                newQualityGateRecord.Min = (int)QualityGateInfo.Min;
                newQualityGateRecord.Max = (int)QualityGateInfo.Max;
                newQualityGateRecord.Active = (int)QualityGateInfo.Active;

                db.QualityGates.InsertOnSubmit(newQualityGateRecord);
                db.SubmitChanges();

                return new HttpResponseMessage(HttpStatusCode.OK);

            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }

        }


        /// <summary>
        /// Get Active Quality Gate Info
        /// </summary>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON object representing the Active Quality Gate Info if successful
        /// </returns>
        /// <example>
        /// GET /api/Quality/QualityGates
        /// </example>
        [HttpGet]
        public object QualityGates()
        {
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();

                var result = db.QualityGates
                    .OrderByDescending(item => item.QualityGateId) 
                    .GroupBy(x => x.Name)
                    .Select(x => x.First());

                return result;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }
    }
}

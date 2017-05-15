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
    public class TestController : ApiController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Create a new test record in the database
        /// </summary>
        /// <param name="Data">Test results data in JSON format</param>
        /// <returns>
        /// HttpResponseMessage:
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON containing new TestRecordId if successful
        /// </returns>
        /// <example>
        /// POST /api/Test/TestRecord
        /// </example>
        [HttpPost]
        public HttpResponseMessage TestRecord(TestRecordData Data)
        {
            if (Data == null)
            {
                string error = "Test Record JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(Data, Data);

            TestRecord newTestRecord = new TestRecord();
            newTestRecord.BuildRecordId = (int) Data.BuildRecordID;
            newTestRecord.TestType = Data.TestType;

            // Check Platform and Architecture passed are allowed values
            if (!Util.CheckForValidPlatformValue(Data.Platform))
            {
                string error = "Invalid test platform value passed: '" + Data.Platform + "'";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }
            newTestRecord.Platform = Data.Platform;

            if(!Util.CheckForValidArchitectureValue(Data.Architecture))
            {
                string error = "Invalid test architecture value passed: '" + Data.Architecture + "'";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }
            newTestRecord.Architecture = Data.Architecture;

            newTestRecord.URL = Data.URL;
            newTestRecord.Result = "";

            // Insert this to test records to get a new TestRecordID
            // This is need to add the test suites and to return to the caller

            try
            {
                RadarDbDataContext db = new RadarDbDataContext();

                db.TestRecords.InsertOnSubmit(newTestRecord);
                db.SubmitChanges();

                // Submit Test Suites
                foreach (TestSuiteData ts in Data.Suites)
                {
                    TestSuite newTestSuiteRecord = new TestSuite();
                    newTestSuiteRecord.TestRecordId = newTestRecord.TestRecordId;
                    newTestSuiteRecord.TestSuiteName = ts.TestSuiteName;
                    newTestSuiteRecord.TotalNumberOfTests = ts.TotalNumberOfTests;

                    db.TestSuites.InsertOnSubmit(newTestSuiteRecord);
                }

                db.SubmitChanges();

                // Return new TestRecordId to user (used in subsequent API calls)
                return Util.GenerateResponse(HttpStatusCode.OK, "{\"TestRecordId\":\"" + newTestRecord.TestRecordId + "\"}");

            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Updated the result of a test record
        /// </summary>
        /// <param name="Data">Test record result data in JSON format</param>
        /// <returns>
        /// HttpResponseMessage:
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 400 - if an invalid test result string was passed
        /// HTTP 400 - if the test record ID passed can't be found
        /// HTTP 200 - if successful
        /// </returns>
        /// <example>
        /// POST /api/Test/TestRecordResult
        [HttpPost]
        public HttpResponseMessage TestRecordResult(TestRecordResult Data)
        {
            if (Data == null)
            {
                string error = "Test Record Result JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(Data, Data);

            // Check for valid test results - can only be passed, failed or pending
            if (!Util.CheckForValidTestResult(Data.TestResult))
            {
                string error = "Invalid test record result passed (Passed, Failed or Pending are valid): '" + Data.TestResult + "'";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                TestRecord rowToUpdate = db.TestRecords.Where(tr => tr.TestRecordId == Data.TestRecordID).SingleOrDefault();

                if (rowToUpdate == null)
                {
                    string error = "Can't find a test record for the test record ID passed:'" + Data.TestRecordID + "'";
                    logger.Log(LogLevel.Error, error);
                    return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
                }

                rowToUpdate.Result = Data.TestResult;
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
        /// Add results of individual test cases
        /// </summary>
        /// <param name="Data">Test case results data in JSON format</param>
        /// <returns>
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 400 - if the test suite specified does not exist
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 - if the test case result was added successfully
        /// </returns>
        /// <example>
        /// POST /api/Test/TestCaseResult
        /// </example>
        [HttpPost]
        public HttpResponseMessage TestCaseResult(TestCaseResultData Data)
        {
            if (Data == null)
            {
                string error = "Test Case Result JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(Data, Data);

            TestCaseResult newTestCaseResultRecord = new TestCaseResult();

            int testSuiteId = RadarDbHelper.LookupTestSuiteId((int)Data.TestRecordId, Data.TestSuiteName);
            if (testSuiteId == -1)
            {
                string error = "Test Suite specified was not found. Make sure it exists. Exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            newTestCaseResultRecord.TestSuiteId = testSuiteId;
            newTestCaseResultRecord.Name = Data.TestCaseName;
            newTestCaseResultRecord.Result = Data.TestCaseResult;

            try
            {
                RadarDbDataContext db = new RadarDbDataContext();

                db.TestCaseResults.InsertOnSubmit(newTestCaseResultRecord);
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
        /// Get the test results for the given build record ID
        /// </summary>
        /// <param name="BuildRecordId">The build record ID for which to get the test results</param>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON object with test record results for the given build record if successful
        /// </returns>
        /// <example>
        /// GET /api/Test/TestResults
        /// </example>
        [HttpGet]
        public object TestResults(int BuildRecordId)
        {
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                return db.TestRecords.Where(l => l.BuildRecordId == BuildRecordId).Select(l => Formatters.FormatTestRecord(l));
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }
    }
}

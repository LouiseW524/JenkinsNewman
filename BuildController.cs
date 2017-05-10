using NLog;
using RadarBackend.Models.Build;
using RadarBackend.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace RadarBackend.Controllers
{
    public class BuildController : ApiController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Send build record data to the server
        /// </summary>
        /// <param name="Build">Build metadata in JSON format</param>
        /// <returns>
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 400 - if a record already exists for the build specified
        /// HTTP 400 - if the meta-data passed (solution etc.) is not valid
        /// HTTP 500 - if adding the build record to the database fails
        /// HTTP 200 - if adding the build record was successful
        /// </returns>
        /// <example>
        /// POST /api/Build/BuildRecord
        /// </example>
        [HttpPost]
        public HttpResponseMessage BuildRecord(BuildData Build)
        {
            // Check expected JSON is present and has all required fields
            if (Build == null)
            {
                string error = "Build JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error); 
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(Build, Build);

            // Check that we haven't received results for this particular build before
            if (RadarDbHelper.DoesBuildRecordExist(Build))
            {
                string error = "A record already seems to exist for this build - exiting:" + Util.GenerateBuildInfoAsString(Build);
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + "A record already seems to exist for this build" + "\"}");
            }

            // Check that the data passed is valid - error out if not
            if (!RadarDbHelper.IsBuildDataValid(Build))
            {
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + "Build JSON contains invalid data. Please check the logs." + "\"}");
            }
            
            // Data is valid - commit results to database
            int newBuildRecordId = RadarDbHelper.AddNewBuildRecord(Build);

            if (newBuildRecordId != -1)
            {
                return Util.GenerateResponse(HttpStatusCode.OK, "{\"BuildRecordID\":\"" + newBuildRecordId + "\"}");
            }
            else
            {
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + "Could not add new build record. Please check the logs." + "\"}");
            }
        }


        /// <summary>
        /// Send build step to the server
        /// </summary>
        /// <param name="Step">Build step metadata in JSON format</param>
        /// <returns>
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 - if adding the build step was successful
        /// </returns>
        /// <example>
        /// POST /api/Build/BuildStep
        /// </example>
        [HttpPost]
        public HttpResponseMessage BuildStep(BuildStepDetail Step)
        {
            if (Step == null)
            {
                string error = "Build Step JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(Step, Step);

            // We need to check if this build step already exists for this Build Record Id
            // If it does, we'll update the result rather than adding a new build step record

            int buildStepId = RadarDbHelper.LookupBuildStepId(Step);

            if (buildStepId == -1)
            {
                // This build step does not exist, we need to add a new row for it in the build steps table
                BuildStep newBuildStepRecord = new BuildStep();
                newBuildStepRecord.BuildRecordId = (int)Step.BuildRecordID;
                newBuildStepRecord.StepName = Step.StepName;
                newBuildStepRecord.StepResult = Step.StepResult;
                newBuildStepRecord.TimeStamp = Util.GetTimestamp(DateTime.Now);

                try
                {
                    RadarDbDataContext db = new RadarDbDataContext();

                    db.BuildSteps.InsertOnSubmit(newBuildStepRecord);
                    db.SubmitChanges();

                    return new HttpResponseMessage(HttpStatusCode.OK);

                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Fatal, ex);
                    return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
                }
            }
            else
            {
                // This step already exists, we'll just update the result
                RadarDbHelper.UpdateBuildStepResult(buildStepId, Step.StepResult);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }


        /// <summary>
        /// Update a build to the specified milestone
        /// </summary>
        /// <param name="Data">Build milestone metadata in JSON format</param>
        /// <returns>
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 400 - if the Build Milestone specified doesn't exist in the database
        /// HTTP 400 - if you try to move backwards from the current build milestone
        /// HTTP 400 - if we can't find a build record for the build record ID passed
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 - if adding the build step was successful
        /// </returns>
        /// <example>
        /// POST /api/Build/BuildMilestone
        /// </example>
        [HttpPost]
        public HttpResponseMessage BuildMilestone(BuildMilestoneData Data)
        {
            if (Data == null)
            {
                string error = "Build Milestone JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            //Util.CheckProperties(Data, Data); FIXME: Allow CheckProperties to handle optional fields

            // Check that we have a valid BuildMilestone passed
            int newbuildMileStoneId = RadarDbHelper.LookupBuildMilestoneId(Data.BuildMilestone);
            if (newbuildMileStoneId == -1)
            {
                string error = "Invalid BuildMilestone specified - does not exist in the database: '" + Data.BuildMilestone + "'";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }
            else
            {          
                try
                {
                    RadarDbDataContext db = new RadarDbDataContext();

                    // Check that the build milestone is above the current build milestone
                    int currentBuildMilestoneId = RadarDbHelper.GetCurrentBuildMilestone((int)Data.BuildRecordID);

                    if (currentBuildMilestoneId == -1)
                    {
                        string error = "Could not get the build milestone ID currently assigned to build record ID: " + (int)Data.BuildRecordID;
                        logger.Log(LogLevel.Error, error);
                        return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
                    }

                    if (!RadarDbHelper.CheckBuildMilestoneProgressionIsValid(currentBuildMilestoneId, newbuildMileStoneId))
                    {
                        string error = "You cannot progress from milestone '" + RadarDbHelper.GetBuildMilestoneName(currentBuildMilestoneId) + "' to build milestone '" + Data.BuildMilestone + "'";
                        logger.Log(LogLevel.Error, error);
                        return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
                    }

                    // Update the build milestone for this build to the value specified in the build records table
                    BuildRecord buildRecordToUpdate = db.BuildRecords.Where(br => br.BuildRecordID == Data.BuildRecordID).SingleOrDefault();

                    if (buildRecordToUpdate == null)
                    {
                        string error = "Can't find a build record for the build record ID passed:'" + Data.BuildRecordID + "'";
                        logger.Log(LogLevel.Error, error);
                        return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
                    }

                    buildRecordToUpdate.BuildMilestone = newbuildMileStoneId;

                    // Update the table that tracks the build record history through the milestones also
                    BuildMilestonesRecord bmRecord = new BuildMilestonesRecord();
                    bmRecord.BuildRecordId = (int) Data.BuildRecordID;
                    bmRecord.PreviousBuildMilestoneId = currentBuildMilestoneId;
                    bmRecord.NewBuildMilestoneId = newbuildMileStoneId;
                    bmRecord.Username = Data.Username;
                    bmRecord.Comment = Data.Comment;
                    bmRecord.Timestamp = Util.GetTimestamp(DateTime.Now);

                    db.BuildMilestonesRecords.InsertOnSubmit(bmRecord);
                    db.SubmitChanges();

                    // Update the mocked record for this build in ECM also

                    // Get the master record ID (eCM primary key) of the mocked record in eCM (stored in the CMDB)
                    string ecmMockedMasterRecordId = CmdbApiHelper.GetEcmMockMasterRecordId((int)Data.BuildRecordID);

                    if (string.IsNullOrEmpty(ecmMockedMasterRecordId))
                    {
                        logger.Log(LogLevel.Info, "Not updating build milestone status for mocked record in eCM - Could not find an eCM Mocked Master Record ID in the CMDB for Build Record ID: " + Data.BuildRecordID);
                    }
                    else
                    {
                        // Make a call to eCM to update the build status of the mocked eCM record
                        EcmApiHelper.UpdateEcmBuildMilestone(ecmMockedMasterRecordId, Data.BuildMilestone, Data.Comment);
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Fatal, ex);
                    return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
                }          
            }
        }


        /// <summary>
        /// Get the list of active build milestones from the database
        /// </summary>
        /// <returns>
        /// HTTP 500 - if exception occurs
        /// HTTP 200 + JSON list of active milestones if successful
        /// </returns>
        [HttpGet]
        public object BuildMilestone()
        {
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                return db.BuildMilestones.Where(bm => bm.Active == 1).Select(bm => Formatters.FormatBuildMilestone(bm));
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }   
        }


        /// <summary>
        /// Returns a JSON representation of the build milestone history for the given build record ID
        /// </summary>
        /// <param name="BuildRecordId">The build record ID to query</param>
        /// <returns>
        /// HTTP 500 - if exception occurs
        /// HTTP 200 + JSON if successful
        /// </returns>
        [HttpGet]
        public object BuildMilestoneHistory(int BuildRecordId)
        {
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                return db.BuildMilestonesRecords.Where(bmr => bmr.BuildRecordId == BuildRecordId).Select(bmr => Formatters.FormatBuildMilestoneHistory(bmr));
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }   
        }


        /// <summary>
        /// Returns a JSON representation of the build milestones to which this build record ID is allowed to progress from its current milestone.
        /// </summary>
        /// <param name="BuildRecordId">The build record ID to query</param>
        /// <returns>
        /// HTTP 500 - if exception occurs
        /// HTTP 200 + JSON if successful
        /// </returns>
        [HttpGet]
        public object BuildMilestoneProgressions(int BuildRecordId)
        {
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                
                // Get the milestone the build record is at currently
                BuildRecord buildRecord = db.BuildRecords.Where(br => br.BuildRecordID == BuildRecordId).SingleOrDefault();
                int currentMilestoneId = buildRecord.BuildMilestone;

                BuildMilestone currentBuildMilestone = db.BuildMilestones.Where(bm => bm.BuildMilestoneId == currentMilestoneId).SingleOrDefault();

                // Return all active milestones that are above the current milestone
                return db.BuildMilestones.Where(bm => bm.BuildMilestoneLevel >= currentBuildMilestone.BuildMilestoneLevel && bm.Active == 1).Select(bm => Formatters.FormatBuildMilestone(bm));
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }   
        }


        /// <summary>
        /// Update the link to build artefacts for a particual build
        /// </summary>
        /// <param name="Data">Build artefacts link in JSON format</param>
        /// <returns>
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 400 - if we can't find a build record for the build record ID passed
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 - if adding the build step was successful
        /// </returns>
        /// <example>
        /// POST /api/Build/ArtifactoryUrl
        /// </example>
        [HttpPost]
        public HttpResponseMessage ArtifactoryUrl(ArtifactoryData Data)
        {
            if (Data == null)
            {
                string error = "Artifactory Data JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(Data, Data);

            // Update the artifactory URL for the given build
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();

                BuildRecord buildRecordToUpdate = db.BuildRecords.Where(br => br.BuildRecordID == Data.BuildRecordID).SingleOrDefault();

                if (buildRecordToUpdate == null)
                {
                    string error = "Can't find a build record for the build record ID passed:'" + Data.BuildRecordID + "'";
                    logger.Log(LogLevel.Error, error);
                    return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
                }

                buildRecordToUpdate.ArtifactoryURL = Data.ArtifactoryURL;
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
        /// Update the final build result for the given build
        /// </summary>
        /// <param name="Result">Build result info as JSON</param>
        /// <returns>
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 400 - if we can't find a build record for the build record ID passed
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 - if adding the build step was successful
        /// </returns>
        /// <example>
        /// POST /api/Build/BuildResult
        /// </example>
        [HttpPost]
        public HttpResponseMessage BuildResult(BuildResultData Result)
        {
            if (Result == null)
            {
                string error = "Build Result Data JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            //Util.CheckProperties(Result, Result); FIXME - need to determine longer term how to handle optional fields

            // Update the build result for the given build
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                BuildRecord buildRecordToUpdate = db.BuildRecords.Where(br => br.BuildRecordID == Result.BuildRecordID).SingleOrDefault();

                if (buildRecordToUpdate == null)
                {
                    string error = "Can't find a build record for the build record ID passed:'" + Result.BuildRecordID + "'";
                    logger.Log(LogLevel.Error, error);
                    return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
                }

                buildRecordToUpdate.BuildResult = Result.BuildResult;
                buildRecordToUpdate.BuildComment = Result.BuildComment;
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
        /// Update the code coverage result for the given build
        /// </summary>
        /// <param name="Coverage">Code coverage result info as JSON</param>
        /// <returns>
        /// HTTP 400 - if JSON received is null or has a syntax error
        /// HTTP 400 - if we can't find a build record for the build record ID passed
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 - if adding the build step was successful
        /// </returns>
        /// <example>
        /// POST /api/Build/CodeCoverage
        /// </example>
        [HttpPost]
        public HttpResponseMessage CodeCoverage(CodeCoverage Coverage)
        {
            if (Coverage == null)
            {
                string error = "Code Coverage Result Data JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(Coverage, Coverage);

            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                BuildRecord buildRecordToUpdate = db.BuildRecords.Where(br => br.BuildRecordID == Coverage.BuildRecordID).SingleOrDefault();

                if (buildRecordToUpdate == null)
                {
                    string error = "Can't find a build record for the build record ID passed:'" + Coverage.BuildRecordID + "'";
                    logger.Log(LogLevel.Error, error);
                    return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
                }

                buildRecordToUpdate.CodeCoverage = Coverage.Coverage;
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
        /// Get a high level summary of the build KPI's
        /// </summary>
        /// <param name="BuildRecordId">Build record ID for the build</param>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON response with build KPI summary if successful</returns>
        /// <example>
        /// GET /api/Build/HighLevelBuildReport?BuildRecordId=1
        /// </example>
        [HttpGet]
        public object HighLevelBuildReport(int BuildRecordId)
        {
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                return db.BuildRecords.Where(b => b.BuildRecordID == BuildRecordId).Select(b => RadarDbHelper.GetHighLevelBuildReport(b));
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Get a high level summary of the build KPI's
        /// </summary>
        /// <param name="solutionName">The solution name</param>
        /// <param name="solutionVersion">The solution version</param>
        /// <param name="productName">The product name</param>
        /// <param name="productVersion">The product version</param>
        /// <param name="componentName">The component name</param>
        /// <param name="componentVersion">The component version</param>
        /// <param name="buildNumber">The build number of the build to format</param>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON response with build KPI summary if successful</returns>
        /// <example>
        /// GET /api/Build/HighLevelBuildReport
        /// </example>
        [HttpGet]
        public object HighLevelBuildReport(string solutionName, string solutionVersion, string productName, string productVersion, string componentName, string componentVersion, int buildNumber, string branch)
        {
            try
            {
                return RadarDbHelper.GetHighLevelBuildReport(solutionName, solutionVersion, productName, productVersion, componentName, componentVersion, buildNumber, branch);
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Get a summary of a build
        /// </summary>
        /// <param name="buildRecordId">Build record ID for the build</param>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON response with build result summary if successful
        /// </returns>
        /// <example>
        /// GET /api/Build/BuildRecord?buildRecordId=1
        /// </example>
        [HttpGet]
        public object BuildRecord(int buildRecordId)
        {
            try
            {
                RadarDbDataContext db = new RadarDbDataContext();
                return db.BuildRecords.Where(br => br.BuildRecordID == buildRecordId).Select(br => Formatters.FormatBuildRecord(br));
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Gets a high level list of builds related to a particular component for display on the UI
        /// </summary>
        /// <param name="solutionName">The solution name</param>
        /// <param name="solutionVersion">The solution version</param>
        /// <param name="productName">The product name</param>
        /// <param name="productVersion">The product version</param>
        /// <param name="componentName">The component name</param>
        /// <param name="componentVersion">The component version</param>
        /// <returns>A high level list of builds related to a particular component for display on the UI</returns>
        [HttpGet]
        public object HighLevelBuildReportsForComponent(string solutionName, string solutionVersion, string productName, string productVersion, string componentName, string componentVersion)
        {
            try
            {
                int componentId = TaxonomyApiHelper.GetComponentID(solutionName, solutionVersion, productName, productVersion, componentName, componentVersion);

                if (componentId == -1)
                {
                    string error = "Could not find the specified component - exiting.";
                    logger.Log(LogLevel.Error, error);
                    return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
                }

                RadarDbDataContext db = new RadarDbDataContext();

                var buildRecords = from br in db.BuildRecords
                                   where br.ComponentID == componentId
                                   select br;

                List<object> list = new List<object>();

                foreach (BuildRecord br in buildRecords)
                {
                    list.Add(RadarDbHelper.GetHighLevelBuildReport(br));
                }

                return list;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }



        /// <summary>
        /// Used by Orbit to search for particular builds
        /// </summary>
        /// <param name="solutionName">The solution name</param>
        /// <param name="solutionVersion">The solution version</param>
        /// <param name="productName">The product name</param>
        /// <param name="productVersion">The product version</param>
        /// <param name="componentName">The component name</param>
        /// <param name="componentVersion">The component version</param>
        /// <param name="milestone">The minimum build milestone under which to search</param>
        /// <param name="useExactMilestone">If true, use the milestone paramater. If false, return the build at the highest milestone</param>
        /// <returns>Details on the requested build</returns>
        [HttpGet]
        public object BuildSearch(string solutionName, string solutionVersion, string productName, string productVersion, string componentName, string componentVersion, string milestone, string useExactMilestone)
        {
            try
            {
                BuildSearchRequest request = new BuildSearchRequest();
                request.Solution = solutionName;
                request.SolutionVersion = solutionVersion;
                request.Product = productName;
                request.ProductVersion = productVersion;
                request.Component = componentName;
                request.ComponentVersion = componentVersion;
                request.Milestone = milestone;
                request.UseExactMilestone = Convert.ToBoolean(useExactMilestone);

                BuildSearchResponse response = RadarDbHelper.SearchLatestBuild(request);

                if (response != null)
                {
                    return response;
                }

                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + "Could not find build. Please check the logs." + "\"}");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        // TODO - Search with a build number

        [HttpGet]
        public object BuildSearch(string solutionName, string solutionVersion, string productName, string productVersion, string componentName, string componentVersion, string milestone, int buildNumber)
        {
            throw new NotImplementedException();
        }
    }
}
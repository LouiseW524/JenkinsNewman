using NLog;
using RadarCMDB.Models;
using RadarCMDB.Utility;
using RadarTaxonomy.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace RadarCMDB.Controllers
{
    public class BomController : ApiController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Add a BOM record to the database
        /// </summary>
        /// <param name="BomInfo">The initial BOM information</param>
        /// <returns>
        /// HTTP 400 - if syntax error in JSON or JSON missing
        /// HTTP 400 - if BOM record already exists for the specified build record ID
        /// HTTP 500 - if a server error occurs
        /// HTTP 200 - if the record was added successfully
        /// </returns>
        [HttpPost]
        public HttpResponseMessage Bom(BomDetails BomInfo)
        {
            // Validate that all necessary data is received from the endpoint
            if (BomInfo == null)
            {
                string error = "BOM JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(BomInfo, BomInfo);

            // A BOM can only be submitted once for any BuildRecordId
            if (BomUtil.DoesBomExist((int)BomInfo.BuildRecordId))
            {
                string error = "A BOM record already exists for this BuildRecordId - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            BOM newBomRecord = new BOM();
            newBomRecord.BuildRecordId = (int)BomInfo.BuildRecordId;
            newBomRecord.BomType = BomInfo.BomType;
            newBomRecord.BuildSystem = BomInfo.BuildSystem;
            newBomRecord.BuildSystemVersion = BomInfo.BuildSystemVersion;
            
            try
            {
                CMDBDataContext db = new CMDBDataContext();
                db.BOMs.InsertOnSubmit(newBomRecord);
                db.SubmitChanges();

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
        /// Called to update the AV DAT version for a BOM
        /// </summary>
        /// <param name="AvInfo">The AV DAT info</param>
        /// <returns>
        /// HTTP 400 - if syntax error in JSON or JSON missing
        /// HTTP 400 - if BOM record can't be found for the given build record ID
        /// HTTP 400 - if BOM record is locked
        /// HTTP 500 - if a server error occurs
        /// HTTP 200 - if the record was added successfully
        /// </returns>
        [HttpPost]
        public HttpResponseMessage AntiVirusDatVer(AntiVirusDatVerInfo AvInfo)
        {
            // Validate that all necessary data is received from the endpoint
            if (AvInfo == null)
            {
                string error = "AntiVirDatVer JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(AvInfo, AvInfo);

            // Check a BOM record exists for this Build Record ID (it needs to exist)
            if (!BomUtil.DoesBomExist((int)AvInfo.BuildRecordId))
            {
                string error = "A BOM record doesn't seem to exist for the Build Record Id passed - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            // Ensure the BOM isn't locked
            if (BomUtil.IsBomLocked((int)AvInfo.BuildRecordId))
            {
                string error = "The BOM appears to be locked for this BuildRecordId - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            try
            {
                CMDBDataContext db = new CMDBDataContext();

                // Find the row we need to update
                BOM bomRecord = db.BOMs.Where(b => b.BuildRecordId == AvInfo.BuildRecordId).SingleOrDefault();

                if (bomRecord == null)
                {
                    string error = "Could not find a BOM record for the Build Record Id passed.";
                    logger.Log(LogLevel.Error, error);
                    return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
                }

                bomRecord.AntiVirusDatVer = AvInfo.AntiVirusDatVer;
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
        /// Add the eCM master record ID of a mocked record in eCM
        /// </summary>
        /// <param name="details">The mocked record ID etc.</param>
        /// <returns>
        /// HTTP 400 - if syntax error in JSON or JSON missing
        /// HTTP 400 - if BOM record can't be found for the given build record ID
        /// HTTP 400 - if BOM record is locked
        /// HTTP 500 - if a server error occurs
        /// HTTP 200 - if the record was added successfully
        /// </returns>
        [HttpPost]
        public HttpResponseMessage EcmMockMasterRecord(EcmMockMasterRecord details)
        {
            // Validate that all necessary data is received from the endpoint
            if (details == null)
            {
                string error = "EcmMockMasterRecord JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(details, details);

            // Check a BOM record exists for this Build Record ID (it needs to exist)
            if (!BomUtil.DoesBomExist((int)details.BuildRecordID))
            {
                string error = "A BOM record doesn't seem to exist for the Build Record Id passed - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            // Ensure the BOM isn't locked
            if (BomUtil.IsBomLocked((int)details.BuildRecordID))
            {
                string error = "The BOM appears to be locked for this BuildRecordId - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            try
            {
                CMDBDataContext db = new CMDBDataContext();

                // Find the row we need to update
                BOM bomRecord = db.BOMs.Where(b => b.BuildRecordId == details.BuildRecordID).SingleOrDefault();

                if (bomRecord == null)
                {
                    string error = "Could not find a BOM record for the Build Record Id passed.";
                    logger.Log(LogLevel.Error, error);
                    return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
                }

                bomRecord.EcmMockMasterRecordId = details.EcmMockMasterRecordId;
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
        /// Get the eCM master record ID of a mocked record in eCM
        /// </summary>
        /// <param name="BuildRecordId">The Build Record ID for which to get the eCM master record ID</param>
        /// <returns>The eCM master record ID of the mocked record in eCM we have stored for this Build Record ID</returns>
        [HttpGet]
        public StringQueryResult EcmMockMasterRecord(int BuildRecordId)
        {
            StringQueryResult result = new StringQueryResult();

            if (!BomUtil.DoesBomExist(BuildRecordId))
            {
                logger.Log(LogLevel.Error, "A BOM record doesn't seem to exist for the Build Record Id passed - exiting.");
                return result;
            }

            try
            {
                CMDBDataContext db = new CMDBDataContext();

                BOM bom = db.BOMs.Where(b => b.BuildRecordId == BuildRecordId).SingleOrDefault();  // There should only ever be one BOM for a BuildRecordId

                result.Result = Convert.ToString(bom.EcmMockMasterRecordId);
                return result;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return result;
            }      
        }


        /// <summary>
        /// Add build agents to a BOM record
        /// </summary>
        /// <param name="AgentInfo">The build agent info</param>
        /// <returns>
        /// HTTP 400 - if syntax error in JSON or JSON missing
        /// HTTP 400 - if BOM record can't be found for the given build record ID
        /// HTTP 400 - if BOM record is locked
        /// HTTP 500 - if a server error occurs
        /// HTTP 200 - if the record was added successfully
        /// </returns>
        [HttpPost]
        public HttpResponseMessage Agent(AgentInfo AgentInfo)
        {
            // Validate that all necessary data is received from the endpoint
            if (AgentInfo == null)
            {
                string error = "AgentInfo JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(AgentInfo, AgentInfo);

            // Check a BOM record exists for this Build Record ID (it needs to exist)
            if (!BomUtil.DoesBomExist((int)AgentInfo.BuildRecordId))
            {
                string error = "A BOM record doesn't seem to exist for the Build Record Id passed - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            // Ensure the BOM isn't locked
            if (BomUtil.IsBomLocked((int)AgentInfo.BuildRecordId))
            {
                string error = "The BOM appears to be locked for this BuildRecordId - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            try
            {
                CMDBDataContext db = new CMDBDataContext();

                // Find the BOM record related to this build record ID
                BOM bomRecord = db.BOMs.Where(b => b.BuildRecordId == AgentInfo.BuildRecordId).SingleOrDefault();

                // Now add the Agents
                foreach (AgentLabel a in AgentInfo.Agents)
                {
                    BuildAgent ba = new BuildAgent();
                    ba.AgentLabel = a.Label;
                    ba.BomId = bomRecord.BomId;
                    db.BuildAgents.InsertOnSubmit(ba);
                }

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
        /// Creates a code export record in the database
        /// </summary>
        /// <param name="CodeExportInfo">Code Export details</param>
        /// <returns>
        /// HTTP 400 - if syntax error in JSON or JSON missing
        /// HTTP 400 - if a BOM record doesn't exist for the Build Record Id passed
        /// HTTP 400 - if the BOM is locked for the Build Record Id passed
        /// HTTP 500 - if a server error occurs
        /// HTTP 200 - if the record was added successfully
        /// </returns>
        [HttpPost]
        public HttpResponseMessage CodeExport(CodeExportDetails CodeExportInfo)
        {
            // Validate that all necessary data is received from the endpoint
            if (CodeExportInfo == null)
            {
                string error = "Code Export JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(CodeExportInfo, CodeExportInfo);

            // Check a BOM record exists for this Build Record ID (it needs to exist)
            if (!BomUtil.DoesBomExist((int)CodeExportInfo.BuildRecordId))
            {
                string error = "A BOM record doesn't seem to exist for the Build Record Id passed - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            // Ensure the BOM isn't locked
            if (BomUtil.IsBomLocked((int)CodeExportInfo.BuildRecordId))
            {
                string error = "The BOM appears to be locked for this BuildRecordId - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            CodeExport newCodeExportRecord = new CodeExport();
            newCodeExportRecord.BomId = BomUtil.LookupBomId((int)CodeExportInfo.BuildRecordId);
            newCodeExportRecord.BuildRecordId = (int)CodeExportInfo.BuildRecordId;
            newCodeExportRecord.BomType = CodeExportInfo.BomType;
            newCodeExportRecord.ScmType = CodeExportInfo.ScmType;
            newCodeExportRecord.ScmVersion = CodeExportInfo.ScmVersion;
            newCodeExportRecord.ScmProtocol = CodeExportInfo.ScmProtocol;
            newCodeExportRecord.ScmServer = CodeExportInfo.ScmServer;
            newCodeExportRecord.ScmPath = CodeExportInfo.ScmPath;
            newCodeExportRecord.ScmTag = CodeExportInfo.ScmTag;
            newCodeExportRecord.ScmCommit = CodeExportInfo.ScmCommit;
            newCodeExportRecord.Credential = CodeExportInfo.Credential;
            newCodeExportRecord.ScmOrder = (int)CodeExportInfo.ScmOrder;

            try
            {
                CMDBDataContext db = new CMDBDataContext();
                db.CodeExports.InsertOnSubmit(newCodeExportRecord);
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
        /// Creates a record of an ECM dependency in the database
        /// </summary>
        /// <param name="EcmDependencyInfo">Ecm Dependency details</param>
        /// <returns>
        /// HTTP 400 - if syntax error in JSON or JSON missing
        /// HTTP 400 - if a BOM record doesn't exist for the Build Record Id passed
        /// HTTP 400 - if the BOM is locked for the Build Record Id passed
        /// HTTP 500 - if a server error occurs
        /// HTTP 200 - if the record was added successfully
        /// </returns>
        [HttpPost]
        public HttpResponseMessage EcmDependency(EcmDependencyDetails EcmDependencyInfo)
        {
            // Validate that all necessary data is received from the endpoint
            if (EcmDependencyInfo == null)
            {
                string error = "Ecm Dependency JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(EcmDependencyInfo, EcmDependencyInfo);

            // Check a BOM record exists for this Build Record ID (it needs to exist)
            if (!BomUtil.DoesBomExist((int)EcmDependencyInfo.BuildRecordId))
            {
                string error = "A BOM record doesn't seem to exist for the Build Record Id passed - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            // Ensure the BOM isn't locked
            if (BomUtil.IsBomLocked((int)EcmDependencyInfo.BuildRecordId))
            {
                string error = "The BOM appears to be locked for this BuildRecordId - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            EcmDependency newEcmDependencyRecord = new EcmDependency();
            newEcmDependencyRecord.BomId = BomUtil.LookupBomId((int)EcmDependencyInfo.BuildRecordId);
            newEcmDependencyRecord.BuildRecordId = (int)EcmDependencyInfo.BuildRecordId;
            newEcmDependencyRecord.BomType = EcmDependencyInfo.BomType;
            newEcmDependencyRecord.EcmMasterId = EcmDependencyInfo.EcmMasterId;
            newEcmDependencyRecord.EcmProjectName = EcmDependencyInfo.EcmProjectName;
            newEcmDependencyRecord.EcmBuildNumber = (int)EcmDependencyInfo.EcmBuildNumber;
            newEcmDependencyRecord.EcmPackageNumber = EcmDependencyInfo.EcmPackageNumber;
            newEcmDependencyRecord.EcmVersion = EcmDependencyInfo.EcmVersion;
            newEcmDependencyRecord.ScmOrder = (int)EcmDependencyInfo.ScmOrder;

            try
            {
                CMDBDataContext db = new CMDBDataContext();
                db.EcmDependencies.InsertOnSubmit(newEcmDependencyRecord);
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
        /// Creates a record of an Orbit dependency in the database
        /// </summary>
        /// <param name="OrbitDependencyInfo">Orbit Dependency details</param>
        /// <returns>
        /// HTTP 400 - if syntax error in JSON or JSON missing
        /// HTTP 400 - if a BOM record doesn't exist for the Build Record Id passed
        /// HTTP 400 - if the BOM is locked for the Build Record Id passed
        /// HTTP 500 - if a server error occurs
        /// HTTP 200 - if the record was added successfully
        /// </returns>
        [HttpPost]
        public object OrbitDependency(OrbitDependencyDetails OrbitDependencyInfo)
        {
            // Validate that all necessary data is received from the endpoint
            if (OrbitDependencyInfo == null)
            {
                string error = "Orbit Dependency JSON received is null or you have a syntax error - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            Util.CheckProperties(OrbitDependencyInfo, OrbitDependencyInfo);

            // Check a BOM record exists for this Build Record ID (it needs to exist)
            if (!BomUtil.DoesBomExist((int)OrbitDependencyInfo.BuildRecordId))
            {
                string error = "A BOM record doesn't seem to exist for the Build Record Id passed - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            // Ensure the BOM isn't locked
            if (BomUtil.IsBomLocked((int)OrbitDependencyInfo.BuildRecordId))
            {
                string error = "The BOM appears to be locked for this BuildRecordId - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            OrbitDependency newOrbitDependencyRecord = new OrbitDependency();
            newOrbitDependencyRecord.BomId = BomUtil.LookupBomId((int)OrbitDependencyInfo.BuildRecordId);
            newOrbitDependencyRecord.BuildRecordId = (int)OrbitDependencyInfo.BuildRecordId;
            newOrbitDependencyRecord.BomType = OrbitDependencyInfo.BomType;
            newOrbitDependencyRecord.DependencyBuildRecordId = (int)OrbitDependencyInfo.DependencyBuildRecordId;
            newOrbitDependencyRecord.ScmOrder = (int)OrbitDependencyInfo.ScmOrder;

            try
            {
                CMDBDataContext db = new CMDBDataContext();
                db.OrbitDependencies.InsertOnSubmit(newOrbitDependencyRecord);
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
        /// Set a BOM record to locked so that it can't be edited further
        /// </summary>
        /// <param name="BuildRecordId">The build record ID of the BOM to lock</param>
        /// <returns>
        /// HTTP 400 - if a BOM record doesn't exist for the Build Record Id passed
        /// HTTP 400 - if the BOM is already locked
        /// HTTP 500 - if a server error occurs
        /// HTTP 200 - if the record was added successfully
        /// </returns>
        [HttpPost]
        public HttpResponseMessage LockBom(int BuildRecordId)
        {
            // Check a BOM record exists for this Build Record ID (it needs to exist)
            if (!BomUtil.DoesBomExist(BuildRecordId))
            {
                string error = "A BOM record doesn't seem to exist for the Build Record Id passed - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            // Check that this Bom isn't already locked
            if (BomUtil.IsBomLocked(BuildRecordId))
            {
                string error = "The BOM for the specified Build Record Id already seems to be locked - exiting.";
                logger.Log(LogLevel.Error, error);
                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
            }

            try
            {
                CMDBDataContext db = new CMDBDataContext();
                BOM bomRecord = db.BOMs.Where(b => b.BuildRecordId == BuildRecordId).SingleOrDefault(); // Should only be one BOM per BuildRecordId

                if (bomRecord == null)
                {
                    string error = "Could not find a BOM record for the Build Record Id passed.";
                    logger.Log(LogLevel.Error, error);
                    return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + error + "\"}");
                }

                // Set BOM to locked
                bomRecord.BomLocked = 1;
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
        /// Return a JSON representation of a BOM record
        /// </summary>
        /// <param name="BuildRecordId">The build record ID for which we want to return the details</param>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + A JSON representation of a BOM record if successful
        /// </returns>
        [HttpGet]
        public object Bom(int BuildRecordId)
        {
            try
            {
                CMDBDataContext db = new CMDBDataContext();
                return db.BOMs.Where(b => b.BuildRecordId == BuildRecordId).Select(b => Formatters.FormatBom(b));
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }
    

        /// <summary>
        /// Return a JSON representation of the build dependency details
        /// This is used to generate the build dependency graph on the client
        /// </summary>
        /// <param name="BuildRecordId">The build record ID for which we want to return the details</param>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + A JSON representation of the build dependency details
        /// </returns>
        [HttpGet]
        public object BuildDependencyGraph(int BuildRecordId)
        {
            try
            {
                CMDBDataContext db = new CMDBDataContext();

                //List<OrbitDependency> orbitDependencies = BomUtil.GetAllChildOrbitDependencies(BuildRecordId); // FIXME: Recursive version
                List<OrbitDependency> orbitDependencies = db.OrbitDependencies.Where(od => od.BuildRecordId == BuildRecordId).ToList();
                List<EcmDependency> ecmDependencies = db.EcmDependencies.Where(ed => ed.BuildRecordId == BuildRecordId).ToList();

                return BomUtil.GenerateBuildDependencyGraphDetails(BuildRecordId, orbitDependencies, ecmDependencies);
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Return a JSON representation of the build dependency details
        /// This is use for a tabular representation of dependencies on the client
        /// </summary>
        /// <param name="BuildRecordId">The build record ID for which we want to return the details</param>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + A JSON representation of the build dependency details
        /// </returns>
        [HttpGet]
        public object BuildDependencyTable(int BuildRecordId)
        {
            try
            {
                return Formatters.FormatBuildDependencies(BuildRecordId);
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }
    }
}

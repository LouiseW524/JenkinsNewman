using NLog;
using RadarTaxonomy.Models;
using RadarTaxonomy.Utility;
using System;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace RadarTaxonomy.Controllers
{
    public class QueryController : ApiController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        // TODO: Make the controller methods thinner here

        /// <summary>
        /// Get the list of solutions from the database
        /// </summary>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON containing solutions list if successful
        /// </returns>
        /// <example>
        /// GET /api/Taxonomy/Solutions
        /// </example>
        [HttpGet]
        public object Solutions()
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                return db.Solutions.Where(s => s.Active == 1).Select(l => Formatters.FormatSolution(l));
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Get the list of products and associated components from the database
        /// </summary>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON containing products list if successful
        /// </returns>
        /// <example>
        /// GET /api/Taxonomy/Products
        /// </example>
        [HttpGet]
        public object Products()
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                return db.Products.Where(p => p.Active == 1).Select(l => Formatters.FormatProduct(l));
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Gets the list of products/version components/versions in the given solution/version
        /// </summary>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON containing solution taxonomy if sucessful
        /// </returns>
        /// <example>
        /// GET /api/Taxonomy/SolutionTaxonomy
        /// </example>
        [HttpGet]
        public object SolutionTaxonomy(string solutionName, string solutionVersion)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();

                Solution solution = db.Solutions.Where(s => s.SolutionName.ToLower() == solutionName.ToLower() &&
                                                       s.SolutionVersion.ToLower() == solutionVersion.ToLower() &&
                                                       s.Active == 1).SingleOrDefault();

                if (solution != null)
                {
                    return Formatters.FormatSolutionTaxonomy(solution);
                }

                return Util.GenerateResponse(HttpStatusCode.BadRequest, "{\"Error\":\"" + "Could not find solution/version specified or it is inactive in the database." + "\"}");

            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Return the full taxonomy for the client to consume
        /// Used in the navigation menu on the client
        /// </summary>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON with full taxonomy
        /// </returns>
        /// <example>
        /// GET /api/Taxonomy/TaxonomyForClient
        /// </example>
        [HttpGet]
        public object TaxonomyForClient()
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                return Formatters.FormatTaxonomyForClient();
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Get the solution ID of the given solution/version
        /// </summary>
        /// <param name="solutionName">The name of the solution</param>
        /// <param name="solutionVersion">The version of the solution e.g. 1.0.0</param>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON containing solution ID if successful
        /// </returns>
        [HttpGet]
        public object SolutionID(string solutionName, string solutionVersion)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                IntQueryResult result = new IntQueryResult();

                // Find the solution with this name and version
                Solution solution = db.Solutions.Where(s => s.SolutionName.ToLower() == solutionName.ToLower() &&
                                                  s.SolutionVersion.ToLower() == solutionVersion.ToLower() &&
                                                  s.Active == 1).SingleOrDefault();

                // Check that solution was found
                if (solution == null)
                {
                    result.Result = -1;
                    return result;
                }

                result.Result = solution.SolutionID;

                return result;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Get the product ID of the given product
        /// </summary>
        /// <param name="solutionName">The name of the solution the product is contained in</param>
        /// <param name="solutionVersion">The solution version</param>
        /// <param name="productName">The name of the product</param>
        /// <param name="productVersion">The version of the product e.g. 11.0</param>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON containing product ID if successful
        /// </returns>
        [HttpGet]
        public object ProductID(string solutionName, string solutionVersion, string productName, string productVersion)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                IntQueryResult result = new IntQueryResult();

                // First find the solution with this name and version
                Solution solution = db.Solutions.Where(s => s.SolutionName.ToLower() == solutionName.ToLower() &&
                                                  s.SolutionVersion.ToLower() == solutionVersion.ToLower() &&
                                                  s.Active == 1).SingleOrDefault();

                // Check that solution was found
                if (solution == null)
                {
                    result.Result = -1;
                    return result;
                }

                // Get the product with the given name in the given solution
                Product product = db.Products.Where(p =>
                                                p.ProductShortName.ToLower() == productName.ToLower() &&
                                                p.SolutionID == solution.SolutionID &&
                                                p.ProductVersion.ToLower() == productVersion &&
                                                p.Active == 1).SingleOrDefault();

                // Check the product was found
                if (product == null)
                {
                    result.Result = -1;
                    return result;
                }

                result.Result = product.ProductID;

                return result;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Get the component ID of the given component
        /// </summary>
        /// <param name="solutionName">The name of the solution the product is contained in</param>
        /// <param name="solutionVersion">The solution version</param>
        /// <param name="productName">The name of the product</param>
        /// <param name="productVersion">The product version</param>
        /// <param name="componentName">The name of the component</param>
        /// <param name="componentVersion">The version of the component e.g. 11.0</param>
        /// <returns>
        /// HTTP 500 - if an exception occurs
        /// HTTP 200 + JSON containing component ID if successful
        /// </returns>
        [HttpGet]
        public object ComponentID(string solutionName, string solutionVersion, string productName, string productVersion, string componentName, string componentVersion)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                IntQueryResult result = new IntQueryResult();

                // First find the solution with this name and version
                Solution solution = db.Solutions.Where(s => s.SolutionName.ToLower() == solutionName.ToLower() &&
                                                  s.SolutionVersion.ToLower() == solutionVersion.ToLower() &&
                                                  s.Active == 1).SingleOrDefault();

                // Check that solution was found
                if (solution == null)
                {
                    result.Result = -1;
                    return result;
                }

                // Get the product with the given name is in the given solution
                Product product = db.Products.Where(p =>
                                                p.ProductShortName.ToLower() == productName.ToLower() &&
                                                p.SolutionID == solution.SolutionID &&
                                                p.ProductVersion.ToLower() == productVersion &&
                                                p.Active == 1).SingleOrDefault();

                // Check the product was found
                if (product == null)
                {
                    result.Result = -1;
                    return result;
                }


                // Get the component
                Component component = db.Components.Where(c => c.ComponentName.ToLower() == componentName.ToLower() &&
                                                         c.ProductID == product.ProductID &&
                                                         c.ComponentVersion.ToLower() == componentVersion.ToLower() &&
                                                         c.Active == 1).SingleOrDefault();

                if (component == null)
                {
                    result.Result = -1;
                    return result;
                }

                result.Result = component.ComponentID; ;            
                return result;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Get the short name for a component
        /// </summary>
        /// <param name="componentID">The component ID</param>
        /// <returns>The short name for a component</returns>
        [HttpGet]
        public object ComponentShortName(int componentID)
         {
             try
             {
                 TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                 StringQueryResult result = new StringQueryResult();

                 Component component = db.Components.Where(c => c.ComponentID == componentID).SingleOrDefault();

                 if (component == null)
                 {
                     result.Result = "Not Found";
                     return result;
                 }

                 result.Result = component.ComponentName;
                 return result;
             }
             catch (Exception ex)
             {
                 logger.Log(LogLevel.Fatal, ex);
                 return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
             }
         }


        /// <summary>
        /// Get the long name for a component
        /// </summary>
        /// <param name="componentID">The component ID</param>
        /// <returns>The long name for a component</returns>
        [HttpGet]
        public object ComponentLongName(int componentID)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                StringQueryResult result = new StringQueryResult();

                Component component = db.Components.Where(c => c.ComponentID == componentID).SingleOrDefault();

                if (component == null)
                {
                    result.Result = "Not Found";
                    return result;
                }

                result.Result = component.ComponentLongName;
                return result;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Get the component version of the given component ID as a string
        /// </summary>
        /// <param name="componentID">The component ID</param>
        /// <returns>The component version of the given component ID as a string</returns>
        [HttpGet]
        public object ComponentVersionAsString(int componentID)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                StringQueryResult result = new StringQueryResult();

                Component component = db.Components.Where(c => c.ComponentID == componentID).SingleOrDefault();

                if (component == null)
                {
                    result.Result = "Not Found";
                    return result;
                }

                result.Result = component.ComponentVersion;
                return result;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Get the long name for a product
        /// </summary>
        /// <param name="productID">The product ID</param>
        /// <returns>The long name for a product</returns>
        [HttpGet]
        public object ProductLongName(int productID)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                StringQueryResult result = new StringQueryResult();

                Product product = db.Products.Where(p => p.ProductID == productID).SingleOrDefault();

                if (product == null)
                {
                    result.Result = "Not Found";
                    return result;
                }

                result.Result = product.ProductLongName;
                return result;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Get the product version of the given product ID as a string
        /// </summary>
        /// <param name="productID">The product ID</param>
        /// <returns>The product version of the given product ID as a string</returns>
        [HttpGet]
        public object ProductVersionAsString(int productID)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                StringQueryResult result = new StringQueryResult();

                Product product = db.Products.Where(p => p.ProductID == productID).SingleOrDefault();

                if (product == null)
                {
                    result.Result = "Not Found";
                    return result;
                }

                result.Result = product.ProductVersion;
                return result;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Gets the name of the product in Bugzilla for the given product ID
        /// </summary>
        /// <param name="productId">The product ID</param>
        /// <returns>The name of the product in Bugzilla for the given product ID</returns>
        [HttpGet]
        public object ProductBugzillaName(int productId)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                StringQueryResult result = new StringQueryResult();

                Product product = db.Products.Where(p => p.ProductID == productId).SingleOrDefault();

                if (product == null)
                {
                    result.Result = "Not Found";
                    return result;
                }

                result.Result = product.ProductBugzillaName;
                return result;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Get the name of the solution with the given solution ID
        /// </summary>
        /// <param name="solutionID">The solution ID</param>
        /// <returns>The name of the solution with the given solution ID</returns>
        [HttpGet]
        public object SolutionName(int solutionID)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                StringQueryResult result = new StringQueryResult();

                Solution solution = db.Solutions.Where(s => s.SolutionID == solutionID).SingleOrDefault();

                if (solution == null)
                {
                    result.Result = "Not Found";
                    return result;
                }

                result.Result = solution.SolutionName;
                return result;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Get the solution version of the given solution ID as a string
        /// </summary>
        /// <param name="solutionID">The solution ID</param>
        /// <returns>The solution version of the given solution ID as a string</returns>
        [HttpGet]
        public object SolutionVersionAsString(int solutionID)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                StringQueryResult result = new StringQueryResult();

                Solution solution = db.Solutions.Where(s => s.SolutionID == solutionID).SingleOrDefault();

                if (solution == null)
                {
                    result.Result = "Not Found";
                    return result;
                }

                result.Result = solution.SolutionVersion;
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
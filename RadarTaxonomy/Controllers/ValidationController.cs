using NLog;
using RadarTaxonomy.Models;
using RadarTaxonomy.Utility;
using System;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace RadarTaxonomy.Controllers
{
    public class ValidationController : ApiController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        // TODO: Make the controller methods thinner here

        /// <summary>
        /// Checks if the given solution name exists in the taxonomy database
        /// </summary>
        /// <returns>
        /// True if the solution name exists, false otherwise
        /// </returns>
        /// <example>
        /// GET /api/validation/SolutionNameValidation
        /// </example>
        [HttpGet]
        public object SolutionNameValidation(string name)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                ValidationResult validation = new ValidationResult();

                var value = db.Solutions.Where(s => s.SolutionName.ToLower() == name.ToLower()).Any();

                if (value == true)
                    validation.IsValid = true;
                else
                    validation.IsValid = false;

                return validation;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Checks if the given solution with the given version exists in the taxonomy database
        /// </summary>
        /// <returns>
        /// True if the solution name with the given version exists, false otherwise
        /// </returns>
        /// <example>
        /// GET /api/validation/SolutionVersionValidation
        /// </example>
        [HttpGet]
        public object SolutionVersionValidation(string name, string version)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                ValidationResult validation = new ValidationResult();

                Solution solution = db.Solutions.Where(s => s.SolutionName.ToLower() == name.ToLower() &&
                                                       s.SolutionVersion.ToLower() == version.ToLower()).SingleOrDefault();

                if (solution == null)
                {
                    // Solution name/version given is not found, so this version is not valid
                    validation.IsValid = false;
                    return validation;
                }

                validation.IsValid = true;

                return validation;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");;
            }
        }


        /// <summary>
        /// Checks if the given product name exists and is in the specified solution
        /// </summary>
        /// <returns>
        /// True if the given product name exists and is in the specified solution, false otherwise
        /// </returns>
        /// <example>
        /// GET /api/validation/ProductNameValidation
        [HttpGet]
        public object ProductNameValidation(string solutionName, string solutionVersion, string productName)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                ValidationResult validation = new ValidationResult();

                // First check if this is a valid solution
                Solution solution = db.Solutions.Where(s => s.SolutionName.ToLower() == solutionName.ToLower() &&
                                                  s.SolutionVersion.ToLower() == solutionVersion.ToLower()).SingleOrDefault();

                if (solution == null)
                {
                    // Solution name/version given is not found
                    validation.IsValid = false;
                    return validation;
                }

                // Solution name given exists, does the given product of this solution given exist?
                if (db.Products.Any(p => (p.ProductShortName == productName) && (p.SolutionID == solution.SolutionID)))
                {
                    // This solution exists, and the specified product of this solution exists also - valid product
                    validation.IsValid = true;
                }

                return validation;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Checks if the given product with the given version exists in the taxonomy database
        /// </summary>
        /// <returns>
        /// True if the product name with the given version exists, false otherwise
        /// </returns>
        /// <example>
        /// GET /api/validation/ProductVersionValidation
        [HttpGet]
        public object ProductVersionValidation(string solutionName, string solutionVersion, string productName, string productVersion)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                ValidationResult validation = new ValidationResult();

                // First check if this is a valid solution
                Solution solution = db.Solutions.Where(s => s.SolutionName.ToLower() == solutionName.ToLower() &&
                                                       s.SolutionVersion.ToLower() == solutionVersion.ToLower()).SingleOrDefault(); 

                if (solution == null)
                {
                    // Solution name given is not found
                    validation.IsValid = false;
                    return validation;
                }

                // Check this product/version is in this solution
                Product product = db.Products.Where(
                    p => p.ProductShortName.ToLower() == productName.ToLower() && 
                    p.SolutionID == solution.SolutionID &&
                    p.ProductVersion.ToLower() == productVersion.ToLower()
                    ).SingleOrDefault();

                if (product == null)
                {
                    // Product given is not found, so this version can't exist
                    validation.IsValid = false;
                    return validation;
                }

                validation.IsValid = true;

                return validation;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Checks if the given component name exists and is in the specified product
        /// </summary>
        /// <returns>
        /// True if the given component name exists and is in the specified product, false otherwise
        /// </returns>
        /// <example>
        /// GET /api/validation/ComponentNameValidation
        [HttpGet]
        public object ComponentNameValidation(string solutionName, string solutionVersion, string productName, string productVersion, string componentName)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                ValidationResult validation = new ValidationResult();

                // First check if this is a valid solution/version
                Solution solution = db.Solutions.Where(s => s.SolutionName.ToLower() == solutionName.ToLower() &&
                                                    s.SolutionVersion.ToLower() == solutionVersion.ToLower()).SingleOrDefault();

                if (solution == null)
                {
                    // Solution name given is not found
                    validation.IsValid = false;
                    return validation;
                }

                // Check if this is a valid product/version
                Product product = db.Products.Where(p => p.ProductShortName.ToLower() == productName.ToLower() &&
                                                p.SolutionID == solution.SolutionID &&
                                                p.ProductVersion.ToLower() == productVersion.ToLower()
                                                ).SingleOrDefault();

                if (product == null)
                {
                    // Product name/version given is not found, so this version can't exist
                    validation.IsValid = false;
                    return validation;
                }

                // Product name given exists, does the given component of this product exist?
                if (db.Components.Any(c => (c.ComponentName == componentName) && (c.ProductID == product.ProductID)))
                {
                    // This product exists, and the specified component of this product exists also - valid component
                    validation.IsValid = true;
                }

                return validation;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }


        /// <summary>
        /// Checks if the given component with the given version exists in the taxonomy database
        /// </summary>
        /// <returns>
        /// True if the component name with the given version exists, false otherwise
        /// </returns>
        /// <example>
        /// GET /api/validation/ComponentVersionValidation
        [HttpGet]
        public object ComponentVersionValidation(string solutionName, string solutionVersion, string productName, string productVersion, string componentName, string componentVersion)
        {
            try
            {
                TaxonomyDbDataContext db = new TaxonomyDbDataContext();
                ValidationResult validation = new ValidationResult();

                // First check if this is a valid solution
                Solution solution = db.Solutions.Where(s => s.SolutionName.ToLower() == solutionName.ToLower() &&
                                                  s.SolutionVersion.ToLower() == solutionVersion.ToLower()
                                                  ).SingleOrDefault();

                if (solution == null)
                {
                    // Solution name/version not found
                    validation.IsValid = false;
                    return validation;
                }

                // Check if this is a valid product
                Product product = db.Products.Where(p => p.ProductShortName.ToLower() == productName.ToLower() &&
                                                p.SolutionID == solution.SolutionID &&
                                                p.ProductVersion.ToLower() == productVersion.ToLower()).SingleOrDefault();

                if (product == null)
                {
                    // Product name/version/not found
                    validation.IsValid = false;
                    return validation;
                }

                // Component
                Component component = db.Components.Where(c => c.ComponentName.ToLower() == componentName.ToLower() &&
                                                         c.ProductID == product.ProductID &&
                                                         c.ComponentVersion.ToLower() == componentVersion.ToLower()
                                                         ).SingleOrDefault();

                if (component == null)
                {
                    // Component name/version not found
                    validation.IsValid = false;
                    return validation;
                }

                validation.IsValid = true;

                return validation;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Fatal, ex);
                return Util.GenerateResponse(HttpStatusCode.InternalServerError, "{\"Error\":\"" + ex.Message + "\"}");
            }
        }
    }
}

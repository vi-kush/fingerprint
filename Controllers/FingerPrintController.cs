using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using FingerPrint.Models;
using FingerPrint.Services;
using FingerPrint.Exceptions;

namespace FingerPrint.Controllers
{
    [RoutePrefix("api/fingerprint")]
    public class FingerPrintController : ApiController
    {
        private static readonly ConcurrentDictionary<string, FingerPrintModel> StoredFingerprints = new ConcurrentDictionary<string, FingerPrintModel>();

        public FingerPrintController()
        {
            // Constructor
        }

        // GET: api/fingerprint/
        [HttpGet]
        [Route("")]
        public IHttpActionResult ApiCheck()
        {
            return Ok(new { message = "Fingerprint API is working!" });
        }

        // POST: api/fingerprint/seed
        [HttpPost]
        [Route("seed")]
        public IHttpActionResult SeedFingerprints([FromBody] List<FingerPrintModel> FingerprintList)
        {
            int size = 0;
            
            if (FingerprintList == null || FingerprintList.Count == 0)
            {
                return BadRequest("No fingerprint data provided");
            }
            
            foreach (var fingerprint in FingerprintList)
            {
                if (!string.IsNullOrEmpty(fingerprint.Template) && !string.IsNullOrEmpty(fingerprint.UserName))
                {
                    // Store the fingerprint template with the username as the key
                    FingerPrintModel fp = new() {
                        Template = fingerprint.Template,
                        UserName = fingerprint.UserName
                    };

                    if (StoredFingerprints.TryAdd(fingerprint.UserName, fp))
                    {
                        size++;
                    }
                    else
                    {
                        // If the user already exists, update their fingerprint
                        StoredFingerprints[fingerprint.UserName] = fp;
                        size++;
                    }
                }
            }
    
            return Ok(new { 
                message = $"{size} FingerPrint(s) Saved",
                totalStored = StoredFingerprints.Count
            });

        }

        // Get: api/fingerprint/all
        [HttpGet]
        [Route("all")]
        public IHttpActionResult GetAllFingerprints()
        {
            
            List<FingerPrintModel> AllFingerPrint = new List<FingerPrintModel>();

            foreach (var fingerprint in StoredFingerprints)
            {
                AllFingerPrint.Add(fingerprint.Value);
            }
    
            return Ok(new { 
                FingerPrints = AllFingerPrint
            });

        }

        // GET: api/fingerprint/start-enrollment
        [HttpGet]
        [Route("start-enrollment")]
        public async Task<IHttpActionResult> StartEnrollment()
        {
            uint TIMEOUT = 120;
            try
            {
                // Execute enrollment on the STA thread
                var result = await STAThreadService.Instance.ExecuteAsync(() => 
                {
                    using (EnrollmentService service = new EnrollmentService())
                    {
                        // Configure form
                        service.Visible = false;
                        
                        // Create a flag to track completion
                        bool enrollmentComplete = false;
                        string template = string.Empty;
                        List<string> fingerPrints = new List<string>();
                        
                        // Handle template creation
                        service.OnTemplate += (t) => 
                        {
                            if (t != null)
                            {
                                template = service.GetBase64Template();
                                fingerPrints = service.GetFingerPrints();
                                enrollmentComplete = true;
                                service.Close();
                            }
                        };
                        
                        Console.WriteLine("Starting enrollment process...");
                        service.StartEnrollment();
                        
                        // Wait for completion or timeout
                        DateTime startTime = DateTime.Now;
                        while (!enrollmentComplete && (DateTime.Now - startTime).TotalSeconds < TIMEOUT)
                        {
                            Thread.Sleep(100);
                            System.Windows.Forms.Application.DoEvents();
                            if(service.ForceClosed && !enrollmentComplete){
                                service.Close();
                                throw new FormClosedException("Form was closed by user");
                            }
                        }

                        if (!enrollmentComplete)
                        {
                            service.Close();
                            throw new TimeoutException("Fingerprint enrollment timed out after 30 seconds");
                        }
                        
                        return new { Template = template, FingerPrints = fingerPrints };
                    }
                });
                
                return Ok(new { 
                    message = "Enrollment Completed", 
                    template = result.Template, 
                    fingerPrints = result.FingerPrints
                });
            }
            catch (Exception ex)
            {
                Exception actualException = ex;
                if (ex is AggregateException aggEx && aggEx.InnerExceptions.Count > 0)
                {
                    actualException = aggEx.InnerException;
                }

                // Now handle the actual exception based on its type
                if (actualException is FormClosedException)
                {
                    return Content(HttpStatusCode.Gone, new {
                        error = "Form closed by user."
                    }); 
                }
                else if (actualException is TimeoutException)
                {
                    return Content(HttpStatusCode.RequestTimeout, new {
                        error = "Fingerprint enrollment timed out. Please try again."
                    });
                }
                else
                {
                    // Log the full exception details for debugging
                    Console.WriteLine($"Exception in StartEnrollment: {ex}");
                    
                    return Content(HttpStatusCode.InternalServerError, new {
                        error = $"Error during enrollment: {actualException.Message}"
                    });
                }
            }
        }

        // POST: api/fingerprint/verify
        [HttpPost]
        [Route("verify")]
        public async Task<IHttpActionResult> VerifyTemplatePost([FromBody] FingerPrintModel FpModel)
        {
            
            string template = FpModel?.Template ?? null;
            uint TIMEOUT = 60; // Timeout Seconds
            
            try
            {
                // Execute verification on the STA thread
                var result = await STAThreadService.Instance.ExecuteAsync(() => 
                {
                    using (VerificationService service = new VerificationService())
                    {
                        // Configure form
                        service.Visible = false;
                        
                        // Deserialize template
                        service.Log("Starting verification. Please place your finger on the reader...");
                        
                        DPFP.Template fingerTemplate = null;
                        
                        if(template != null){
                            fingerTemplate = service.DeSerializeEnrollment(template);
                            if (fingerTemplate == null)
                            {
                                throw new InvalidOperationException("Failed to load fingerprint template");
                            }
                        }else{
                            service.AllBase64Fingerprint = StoredFingerprints;
                        }

                        service.Verify(fingerTemplate);
                        
                        // Wait for completion or timeout
                        DateTime startTime = DateTime.Now;
                        while (!service.VerificationDone && (DateTime.Now - startTime).TotalSeconds < TIMEOUT)
                        {
                            Thread.Sleep(100);
                            System.Windows.Forms.Application.DoEvents();
                            if(service.ForceClosed){
                                break;
                            }
                        }

                        if(service.ForceClosed){
                            service.Close();
                            throw new FormClosedException("Form was closed by user");
                        }
                        
                        if (!service.VerificationDone)
                        {
                            service.Close();
                            throw new TimeoutException("Fingerprint verification timed out");
                        }
                        
                        return new {result = service.GetVerificationResult(), User = service.User ?? null};
                    }
                });
                
                if (result.result)
                {
                    return Ok(new {
                        message = $"Fingerprint verified.",
                        status = true,
                        user = result.User
                    });
                }
                else
                {
                    return Ok(new {
                        message = "Fingerprint verification failed",
                        status = false
                    });
                }
            }
            catch (Exception ex)
            {
                Exception actualException = ex;
                if (ex is AggregateException aggEx && aggEx.InnerExceptions.Count > 0)
                {
                    actualException = aggEx.InnerException;
                }

                // Now handle the actual exception based on its type
                if (actualException is FormClosedException)
                {
                    return Content(HttpStatusCode.Gone, new {
                        error = "Form closed by user."
                    }); 
                }
                else if (actualException is TimeoutException)
                {
                    return Content(HttpStatusCode.RequestTimeout, new {
                        error = "Fingerprint enrollment timed out. Please try again."
                    });
                }
                else
                {
                    // Log the full exception details for debugging
                    Console.WriteLine($"Exception in StartEnrollment: {ex}");
                    
                    return Content(HttpStatusCode.InternalServerError, new {
                        error = $"Error during enrollment: {actualException.Message}"
                    });
                }
            }
        }
    }
}
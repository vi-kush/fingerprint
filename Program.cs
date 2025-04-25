using System;
using System.Windows.Forms;
using System.Web.Http;
using Newtonsoft.Json.Serialization;
using FingerPrint.Controllers;
using System.Net.Http;
using Microsoft.Owin.Hosting;
using Owin;
using FingerPrint.Services;
using System.Linq.Expressions;

namespace FingerPrint
{
    public class Program
    {
        // This provides the entry point required for VS Code with .NET Framework

        [STAThread]
        public static void Main(string[] args)
        {
            MainWebApi(args);
            // MainConsole(args);
        }

        public static void MainWebApi(string[] args)
        {
            System.Threading.Thread.CurrentThread.SetApartmentState(System.Threading.ApartmentState.STA);
            
            string baseAddress = "http://localhost:9000/";

            Console.WriteLine("Starting web server at {0}", baseAddress);
            
            using (WebApp.Start<Startup>(url: baseAddress))
            {
                Console.WriteLine("Server running at {0}", baseAddress);
                Console.WriteLine("Test your API with: {0}api/fingerprint", baseAddress);
                Console.WriteLine("Press Enter to quit.");
                Console.ReadLine();
            }
        }

        public static void MainConsole(string[] args){

            try{    
                Console.WriteLine("Enrollment 1 Start");
                string template = Enroll();
                Console.WriteLine(template);

                // Console.WriteLine("Enrollment 2 Start");
                // string template2 = Enroll();
                // Console.WriteLine(template2);

                // String template = "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";
                System.Threading.Thread.Sleep(500); 

                Console.WriteLine("Verification Start 1");
                bool verified = Verify(template);
                Console.WriteLine("Template 1 Verification result: " + (verified ? "Verified" : "Not Verified"));

                // Console.WriteLine("Verification Start 2");
                // bool verified2 = Verify(template);
                // Console.WriteLine("Template 2 Verification result: " + (verified2 ? "Verified" : "Not Verified"));
            

            } catch (Exception ex){
                Console.WriteLine("Exception:" + ex.Message);
                Console.WriteLine(ex);
            }
        }

        public static string Enroll()
        {
            // Create an instance of the enrollment service
            Console.WriteLine("Initializing fingerprint device...");
            
            // Use a using statement to ensure proper disposal
            using (EnrollmentService service = new EnrollmentService())
            {
                // Configure the form properties
                service.Visible = false;
                service.ShowInTaskbar = false;

                // Create a flag to track when enrollment is complete
                bool enrollmentComplete = false;
                string template = string.Empty;
                
                // Add event handler for when template is created
                service.OnTemplate += (t) => 
                {
                    if (t != null)
                    {
                        template = service.GetBase64Template();
                        enrollmentComplete = true;
                        
                        // Signal to exit
                        service.BeginInvoke(new Action(() => 
                        {
                            service.Stop();
                        }));
                    }
                };

                Console.WriteLine("Starting enrollment process...");
                
                // Start the enrollment
                service.StartEnrollment();
                
                // Wait for enrollment to complete or for user to cancel
                Console.WriteLine("Waiting for fingerprint enrollment to complete...");
                Console.WriteLine("Press Enter to cancel...");
                
                // Check periodically for completion or cancellation
                while (!enrollmentComplete)
                {
                    // Check if user wants to cancel
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine("Enrollment canceled by user.");
                        service.Stop();
                        break;
                    }
                    
                    // Sleep a little to avoid high CPU usage
                    System.Threading.Thread.Sleep(100);
                }
                
                Console.WriteLine("Enrollment completed.");
                return template;
            }
        }

        public static bool Verify(string Template)
        {
            try{
                Console.WriteLine("Starting verification process...");
            
                // Use a using statement to ensure proper disposal
                using (VerificationService verificationService = new VerificationService())
                {
                    // Configure the form properties
                    verificationService.Visible = false;
                    verificationService.ShowInTaskbar = false;
                    
                    // Deserialize the template
                    var template = verificationService.DeSerializeEnrollment(Template);
                    if (template == null)
                    {
                        Console.WriteLine("Failed to load template for verification.");
                        return false;
                    }
                    
                    Console.WriteLine("Starting verification. Please place your finger on the reader...");
                    
                    // Start verification process
                    verificationService.Verify(template);

                    // Wait for verification to complete or for user to cancel
                    Console.WriteLine("Waiting for fingerprint verification to complete...");
                    Console.WriteLine("Press Enter to cancel...");
                
                    // Check periodically for completion or cancellation
                    while (!verificationService.VerificationDone)
                    {
                        // Sleep a little to avoid high CPU usage
                        System.Threading.Thread.Sleep(100);

                        // Check if user wants to cancel
                        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter)
                        {
                            Console.WriteLine("Verification canceled by user.");
                            verificationService.Stop();
                            break;
                        }
                        
                    }
                    
                    Console.WriteLine("Verification completed.");
                    
                    // Return the verification result
                    return verificationService.GetVerificationResult();
                }
            } catch (Exception ex){
                Console.WriteLine(ex);
            }
            return false;
        }
    }

    // Startup class for OWIN self-hosting
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Create configuration
            var config = new HttpConfiguration();
            
            // Apply the standard Web API configuration
            WebApiConfig.Register(config);
            
            // IMPORTANT: Ensure the configuration is initialized
            config.EnsureInitialized();
            
            // Add the WebAPI middleware to the OWIN pipeline
            app.UseWebApi(config);
            
            Console.WriteLine("API routes configured:");
            foreach (var route in config.Routes)
            {
                Console.WriteLine($"  Route: {route.RouteTemplate}");
            }
        }
    }
}
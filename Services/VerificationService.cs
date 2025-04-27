using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using FingerPrint.Models;

namespace FingerPrint.Services
{
    public class VerificationService : EnrollmentService
    {
        public VerificationService() : base()
        {
            Verificator = new DPFP.Verification.Verification();     // Create a fingerprint template verificator
            this.Text = "FingerPrint Verification";
            UpdateStatus(0);
            Result = false;
            User = string.Empty;
            VerificationDone = false;
        }

        public void Verify(DPFP.Template template)
        {
           try{ 
             // Reset state for a new verification
                Result = false;
                Template = template;
                
                // Start the verification process
                StartVerification();
            } catch (Exception ex){
                Console.WriteLine("Verify Exception:" + ex.Message);
                Console.WriteLine(ex);
            }
        }

        public override void OnComplete(object Capture, string ReaderSerialNumber, DPFP.Sample Sample)
        {
            Log("The fingerprint sample was captured.");
            this.Picture1.Image = Image.FromFile("Assets/fingerprint_accepted.png");
            if(Template != null) Process(Sample);
            else ProcessAll(Sample);
        }

        protected override void PostInitializeComponents(){
            this.Picture1 = new PictureBox();
			this.Picture1.Location = new Point(210, 170);
            this.Picture1.Size = new Size(50, 60);
            this.Picture1.TabIndex = 0;
			this.Picture1.TabStop = false;
            this.Picture1.Name = "Finger Print 1";
            this.Picture1.Image = Image.FromFile("Assets/fingerprint.png");
            this.Picture1.SizeMode = PictureBoxSizeMode.Zoom;
            this.Controls.Add(this.Picture1);
        }

        public void StartVerification()
        {
            try{
                // Make sure we're not already capturing
                Console.WriteLine("IsCapturing: "+ IsCapturing);
                if (IsCapturing)
                {
                    Stop();
                    System.Threading.Thread.Sleep(100); // Give it a moment to stop
                }
                            
                // Create a new enrollment object just to reuse the processing pipeline
                Enroller = new DPFP.Processing.Enrollment();
                
                // Start capturing
                Start();
                
                // Show the form to start the message loop
                if (!this.Visible)
                {
                    // Show the form without blocking
                    this.Show();
                    
                    // Start a background thread to process messages if needed
                    if (!Application.MessageLoop)
                    {
                        System.Threading.Thread formThread = new System.Threading.Thread(() =>
                        {
                            Application.Run(this);
                        });
                        formThread.IsBackground = true;
                        formThread.Start();
                    }
                }
            }catch (Exception ex){
                Console.WriteLine("Start Verification "+ex.Message);
                Console.WriteLine(ex);
            }
        }
        
        public DPFP.Template DeSerializeEnrollment(string base64Template)
        {
            try
            {
                DPFP.Template template = new DPFP.Template();
                // Deserialize the template from the byte array
                template.DeSerialize(Convert.FromBase64String(base64Template));
                // Process the loaded template
                
                // Console.WriteLine("Template loaded successfully");
                return template;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading template: " + ex.Message);
                // Console.WriteLine(ex);
            }

            return null;
        }

        private void UpdateStatus(int FAR)
        {
            // Show "False accept rate" value
            Log(String.Format("False Accept Rate (FAR) = {0}", FAR));
        }

        protected override void Process(DPFP.Sample Sample)
        {
            try{

                // Process the sample and create a feature set for the verification purpose
                DPFP.FeatureSet features = ExtractFeatures(Sample, DPFP.Processing.DataPurpose.Verification);
                
                // Check quality of the sample and start verification if it's good
                if (features != null)
                {
                    // Compare the feature set with our template
                    DPFP.Verification.Verification.Result result = new DPFP.Verification.Verification.Result();
                    Verificator.Verify(features, Template, ref result);
                    UpdateStatus(result.FARAchieved);
                    Result = result.Verified;
                    VerificationDone = true;
                    if (Result)
                        Log("The fingerprint was VERIFIED.");
                    else
                        Log("The fingerprint was NOT VERIFIED.");
                    
                    // Stop capture and hide form instead of exiting application
                    Stop();
                }else{
                    Console.WriteLine("Unable to get Feature");
                }
            }catch(Exception ex){
                Console.WriteLine("Process Verification Ex: "+ex.Message);
                Console.WriteLine(ex);
            }
        }

        protected void ProcessAll(DPFP.Sample Sample)
        {
            try{

                // Process the sample and create a feature set for the verification purpose
                DPFP.FeatureSet features = ExtractFeatures(Sample, DPFP.Processing.DataPurpose.Verification);
                
                // Check quality of the sample and start verification if it's good
                if (features != null)
                {
                    // Compare the feature set with our template
                    DPFP.Verification.Verification.Result result = new DPFP.Verification.Verification.Result();

                    foreach(var FingerPrint in AllBase64Fingerprint){
                        string User = FingerPrint.Value.UserName;
                        string Base64Template = FingerPrint.Value.Template;
                        string Base64TemplateTwo = FingerPrint.Value.TemplateTwo;

                        DPFP.Template TemplateTemp = DeSerializeEnrollment(Base64Template);
                        if(TemplateTemp != null){
                            Verificator.Verify(features, TemplateTemp, ref result);
                            Result = result.Verified;
                            if(!Result && !string.IsNullOrEmpty(Base64TemplateTwo)){
                                TemplateTemp = DeSerializeEnrollment(Base64TemplateTwo);
                                if(TemplateTemp != null){
                                    Verificator.Verify(features, TemplateTemp, ref result);
                                    Result = result.Verified;
                                }
                            }
                        }


                        if(Result){
                            this.User = User;
                            break;
                        }

                    }
                    UpdateStatus(result.FARAchieved);
                    VerificationDone = true;
                    if (Result)
                        Log("The fingerprint was VERIFIED.");
                    else
                        Log("The fingerprint was NOT VERIFIED.");
                    
                    // Stop capture and hide form instead of exiting application
                    Stop();
                }else{
                    Console.WriteLine("Unable to get Feature");
                }
            }catch(Exception ex){
                Console.WriteLine("Process Verification Ex: "+ex.Message);
                Console.WriteLine(ex);
            }
        }

        // Override the Stop method to ensure proper shutdown
        public new void Stop()
        {
            // Call the base Stop method to handle the capture device
            base.Stop();
            
            // Ensure the form UI is properly hidden (on the UI thread)
            try 
            {
                if (!this.IsDisposed && this.InvokeRequired) 
                {
                    // this.BeginInvoke(new Action(() => this.Hide()));
                    this.BeginInvoke(new Action(() => this.Close()));
                }
                else if (!this.IsDisposed)
                {
                    // this.Hide();
                    this.Close();
                }
                VerificationDone = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while stopping verification: {ex.Message}");
            }
        }

        public bool GetVerificationResult()
        {
            return Result;
        }
        
        public bool IsCapturing
        {
            get { return isRunning; }
        }

        public bool VerificationDone = false;

        // Override Dispose to ensure we clean up properly
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Clean up verification-specific resources
                Template = null;
                Verificator = null;
            }
            
            // Call base dispose
            base.Dispose(disposing);
        }

        private DPFP.Template Template;
        private DPFP.Verification.Verification Verificator;
        private bool Result;

        public string User;

        public ConcurrentDictionary<string, FingerPrintModel> AllBase64Fingerprint;

        ~VerificationService()
        {
            Dispose(true);
        }
    }
}
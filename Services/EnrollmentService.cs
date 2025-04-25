using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FingerPrint.Services
{

	// [System.Runtime.InteropServices.ComVisible(true)]
	// [System.Runtime.InteropServices.ClassInterface(System.Runtime.InteropServices.ClassInterfaceType.None)]
    public class EnrollmentService : Form, DPFP.Capture.EventHandler, IDisposable
    {
        public EnrollmentService()
        {
            Init();
            
            // Since we're inheriting from Form now, we'll configure this form instead of creating a dummy
            this.Text = "FingerPrint Enrollment";
            this.ForceClosed = false;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            // this.Visible = false;
            // this.FormBorderStyle = FormBorderStyle.None;
            // this.Size = new System.Drawing.Size(1, 1);
            this.Deactivate += (object sender, EventArgs e) =>{
                // Bring the form back to focus
                Console.WriteLine("Deactivate");
                if (isRunning){
                    this.Activate();
                    this.BringToFront();
                }
            };
            this.LostFocus += (object sender, EventArgs e) =>{
                // Bring the form back to focus
                Console.WriteLine("LostFocus");
                if (isRunning){
                    this.Focus();
                }
            };;
			this.FormClosing += (sender, e) => {
				// Ensure capture is stopped
                Console.WriteLine("Closing");
				if (isRunning && Capturer != null)
				{
					try
					{
                        ForceClosed = true;
						Capturer.StopCapture();
						isRunning = false;
					}
					catch { /* Ignore errors during closing */ }
				}

			};
            InitializeComponents();
        }

        private TextBox logTextBox;
        
        private void InitializeComponents()
        {
            // Create log text box
            logTextBox = new TextBox();
            logTextBox.Multiline = true;
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.ReadOnly = true;
            logTextBox.Dock = DockStyle.Top;
            logTextBox.Height = 250;
            // logTextBox.BackColor = Color.Black;
            logTextBox.ForeColor = Color.Black;
            logTextBox.Font = new Font("Consolas", 9);
            
            // Add the control to the form
            this.Controls.Add(logTextBox);
            
            // Resize the form to accommodate the log box
            this.Width = 500;
            this.Height = 300;
        }

        // Method to log messages to both console and text box
        public void Log(string message)
        {
            Console.WriteLine(message);
            
            // Append to the text box on the UI thread
            if (logTextBox != null && !this.IsDisposed)
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() => 
                    {
                        logTextBox.AppendText(DateTime.Now.ToString("[HH:mm:ss] ") + message + Environment.NewLine);
                        // Auto-scroll to the bottom
                        logTextBox.SelectionStart = logTextBox.Text.Length;
                        logTextBox.ScrollToCaret();
                    }));
                }
                else
                {
                    logTextBox.AppendText(DateTime.Now.ToString("[HH:mm:ss] ") + message + Environment.NewLine);
                    // Auto-scroll to the bottom
                    logTextBox.SelectionStart = logTextBox.Text.Length;
                    logTextBox.ScrollToCaret();
                }
            }
        }
        
        public delegate void OnTemplateEventHandler(DPFP.Template template);
        
        public event OnTemplateEventHandler OnTemplate;
        
        protected bool isRunning = false;
        // No need for a separate dummyForm as we now inherit from Form
        private DPFP.Capture.Capture Capturer;
        protected DPFP.Processing.Enrollment Enroller;
        private DPFP.Template Template;
        private string base64Template = string.Empty;
        private List<string> base64FingerPrint = new List<string>();
        private bool disposed = false;

        protected void Init()
        {
            try
            {
                if (Capturer == null)
                {
                    Capturer = new DPFP.Capture.Capture();
                    
                    if (Capturer != null)
                        Capturer.EventHandler = this;
                    else
                        Log("Can't initiate capture operation!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Can't initiate capture operation! {ex.Message}");
            }
        }

        protected virtual void Process(DPFP.Sample Sample)
        {
            // Draw fingerprint sample image.
            Bitmap fingerprintBitmap = ConvertSampleToBitmap(Sample);
            Log($"Features: {Enroller.FeaturesNeeded}");
            base64FingerPrint.Add(BitmapToBase64(fingerprintBitmap));
        }

        private string BitmapToBase64(Bitmap bitmap)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                // Save the bitmap to memory stream in the original format
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                
                // Convert to base64
                byte[] imageBytes = memoryStream.ToArray();
                string base64String = Convert.ToBase64String(imageBytes);
                
                return base64String;
            }
        }

        protected Bitmap ConvertSampleToBitmap(DPFP.Sample Sample)
        {
            DPFP.Capture.SampleConversion Convertor = new DPFP.Capture.SampleConversion();
            Bitmap bitmap = null;
            Convertor.ConvertToPicture(Sample, ref bitmap);
            return bitmap;
        }

        protected DPFP.FeatureSet ExtractFeatures(DPFP.Sample Sample, DPFP.Processing.DataPurpose Purpose)
        {
            DPFP.Processing.FeatureExtraction Extractor = new DPFP.Processing.FeatureExtraction();
            DPFP.Capture.CaptureFeedback feedback = DPFP.Capture.CaptureFeedback.None;
            DPFP.FeatureSet features = new DPFP.FeatureSet();
            Extractor.CreateFeatureSet(Sample, Purpose, ref feedback, ref features);
            if (feedback == DPFP.Capture.CaptureFeedback.Good)
                return features;
            else
                return null;
        }

        public void Start()
        {
            this.BringToFront();
            if (Capturer != null && !isRunning)
            {
                try
                {
                    Capturer.StartCapture();
                    isRunning = true;
                    Log("Using the fingerprint reader, scan your fingerprint.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Can't initiate capture! {ex.Message}");
                }
            }
            else if (isRunning)
            {
                Console.WriteLine("Capture is already running.");
            }
            else
            {
                Console.WriteLine("Capturer is null.");
            }
        }

        public void Stop()
        {
            if (Capturer != null && isRunning)
            {
                try
                {
                    Capturer.StopCapture();
                    isRunning = false;
                    Console.WriteLine("Capture terminated");
                    
                    // Don't exit the application, just close the form if it's running
                    if (!this.IsDisposed)
                    {
                        // this.BeginInvoke(new Action(() => this.Hide()));
                        this.BeginInvoke(new Action(() => this.Close()));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Can't terminate capture: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Capturer is null or not running");
            }
        }

        public virtual void OnComplete(object Capture, string ReaderSerialNumber, DPFP.Sample Sample)
        {
            Log("The fingerprint sample was captured.");
            Log("Scan the same fingerprint again.");
            ProcessEnrollment(Sample);
        }

        public void OnFingerGone(object Capture, string ReaderSerialNumber)
        {
            Log("The finger was removed from the fingerprint reader.");
        }

        public void OnFingerTouch(object Capture, string ReaderSerialNumber)
        {
            Log("The fingerprint reader was touched.");
        }

        public void OnReaderConnect(object Capture, string ReaderSerialNumber)
        {
            Log($"{ReaderSerialNumber}: The fingerprint reader was connected.");
        }

        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber)
        {
            Log($"{ReaderSerialNumber}: The fingerprint reader was disconnected.");
        }

        public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback)
        {
            if (CaptureFeedback == DPFP.Capture.CaptureFeedback.Good)
                Log("The quality of the fingerprint sample is good.");
            else
                Log("The quality of the fingerprint sample is poor.");
        }

        public void StartEnrollment()
        {
            // Clear previous enrollment data
            base64Template = string.Empty;
            base64FingerPrint.Clear();
            Template = null;
            
            // Create a new enrollment object
            Enroller = new DPFP.Processing.Enrollment();
            UpdateStatus();
            
            // Start capturing
            Start();
            
            // Since we now inherit from Form, we'll just show this form
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
        }

        public void ProcessEnrollment(DPFP.Sample Sample)
        {
            Process(Sample);

            // Process the sample and create a feature set for the enrollment purpose.
            DPFP.FeatureSet features = ExtractFeatures(Sample, DPFP.Processing.DataPurpose.Enrollment);

            // Check quality of the sample and add to enroller if it's good
            if (features != null)
            {
                try
                {
                    Console.WriteLine("The fingerprint feature set was created.");
                    Enroller.AddFeatures(features);
                    Console.WriteLine("Sample Received.");
                }
                finally
                {
                    UpdateStatus();

                    // Check if template has been created.
                    switch (Enroller.TemplateStatus)
                    {
                        case DPFP.Processing.Enrollment.Status.Ready:
                            Template = Enroller.Template;
                            OnTemplateHandler(Template);
                            base64Template = SerializeEnrollment(Enroller);
                            Log("Enrollment Completed.");
                            OnTemplate?.Invoke(Template);
                            Stop();
                            break;

                        case DPFP.Processing.Enrollment.Status.Failed:
                            Enroller.Clear();
                            Stop();
                            UpdateStatus();
                            OnTemplateHandler(null);
                            OnTemplate?.Invoke(null);
                            // Don't automatically restart, let the client decide
                            Console.WriteLine("Enrollment failed. You may need to restart the enrollment process.");
                            break;
                    }
                }
            }
        }

        private void UpdateStatus()
        {
            // Show number of samples needed.
            Log(String.Format("Fingerprint samples needed: {0}", Enroller.FeaturesNeeded));
        }

        public string SerializeEnrollment(DPFP.Processing.Enrollment enrollment)
        {
            byte[] serializedTemplate = null;
            enrollment.Template.Serialize(ref serializedTemplate);
            // Convert to Base64 string for display or storage
            string base64Template = Convert.ToBase64String(serializedTemplate);
            Console.WriteLine("Serialized Template (Base64): " + base64Template);
            return base64Template;
        }

        private void OnTemplateHandler(DPFP.Template template)
        {
            Console.WriteLine("Template Handler");
            Template = template;
        }

        public bool ForceClosed = false;

        public string GetBase64Template()
        {
            return base64Template;
        }

        public List<string> GetFingerPrints()
        {
            return base64FingerPrint;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    if (isRunning)
                    {
                        Stop();
                    }
                    
                    if (Capturer != null)
                    {
                        // Remove event handler first
                        Capturer.EventHandler = null;
                        Capturer = null;
                    }
                    
                    // Don't dispose of this form since we are the form
                    if (!this.IsDisposed)
                    {
                        // this.Hide();
                        this.Close();
                    }
                    
                    Template = null;
                    Enroller = null;
                }

                // Free unmanaged resources

                disposed = true;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Console.WriteLine("OnLoad");
            
            // Force to foreground when loaded
            this.Focus();
            this.Activate();
            this.BringToFront();
        }

        public void WriteMessage(string msg){

        }

        ~EnrollmentService()
        {
            Dispose(false);
        }
    }
}
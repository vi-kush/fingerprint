using System;

namespace FingerPrint.Exceptions
{
    
    public class FormClosedException : Exception
    {
        public FormClosedException(string message) : base(message) { }
    }
}
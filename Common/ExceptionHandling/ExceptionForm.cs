using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Common.ExceptionHandling
{
    public partial class ExceptionForm : Form
    {
        string comment;
        Exception ex;
        string errType;
        string errMessage;
        string stackTrace;
        public ExceptionForm(string comment, Exception ex = null)
        {
            InitializeComponent();

            this.comment = comment;
            this.ex = ex;


            commentTextBox.Text += comment;
            PrintException(ex);
        }

        private void PrintException(Exception ex)
        {
            if (ex != null)
            {
                if (ex.InnerException != null)
                {
                    PrintException(ex.InnerException);
                    commentTextBox.Text += "\r\n\r\nInnerException for";
                }
                errType = ex.GetType().ToString();
                errMessage = ex.Message;
                stackTrace = ex.StackTrace;
            }

            commentTextBox.Text += String.Format("\r\n\r\nExceptionType: {0}\r\n\r\nMessage: {1}\r\nStackTrace: {2}",
                ex.GetType().ToString(), errMessage, stackTrace);
        }
    }
}

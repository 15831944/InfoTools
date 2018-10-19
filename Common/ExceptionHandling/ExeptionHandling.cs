using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Common.ExceptionHandling
{
    public static class ExeptionHandlingProcedures
    {
        public static void CommonException(Exception ex, string comment)
        {
            ExceptionForm exF = new ExceptionForm(comment, ex);
            exF.StartPosition = FormStartPosition.CenterScreen;
            //exF.TopMost = true;
            exF.ShowDialog();
        }

        public static void AccessException(IOException ex)
        {
            MessageBox.Show(ex.Message
                + " Убедитесь, что данный файл не открыт у вас или другого пользователя.",
                "Файл заблокирован");
        }
    }
}

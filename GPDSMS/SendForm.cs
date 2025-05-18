using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GPDSMS
{
    public partial class SendForm : Form
    {
        public SendForm(Form1 mainForm, String phoneNo, String message)
        {
            InitializeComponent();
            this.mainForm = mainForm;

            phoneNoText.Text = phoneNo;
            orimessageText.Text = message;

            phoneNoText.ReadOnly = !phoneNo.Equals("");

            if (phoneNo.Equals("-"))
            {
                phoneNoText.Text = "";
                phoneNoText.ReadOnly = false;
            }

        }

        Form1 mainForm = null;

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            mainForm.SendMSM(phoneNoText.Text, messageText.Text);
            mainForm.HistoryFormRefreshList();
            this.Close();
        }
    }
}

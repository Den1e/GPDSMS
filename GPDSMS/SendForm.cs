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
        public SendForm(MainForm mainForm, String phoneNo, String message)
        {
            InitializeComponent();
            this.mainForm = mainForm;

            phoneNoText.Text = phoneNo;
            orimessageText.Text = message.Replace("\r", Environment.NewLine);

            phoneNoText.ReadOnly = !phoneNo.Equals("");

            if (phoneNo.Equals("-"))
            {
                phoneNoText.Text = "";
                phoneNoText.ReadOnly = false;
            }
            this.BringToFront();
        }

        MainForm mainForm = null;

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            this.Hide();
            mainForm.SendMSM(phoneNoText.Text, messageText.Text);
            mainForm.HistoryFormRefreshList();
            this.Close();
        }

        private void SendForm_Load(object sender, EventArgs e)
        {

        }
    }
}

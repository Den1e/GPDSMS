using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GPDSMS
{
    public partial class HistoryForm : Form
    {

        MainForm form1 = null;

        public HistoryForm(MainForm form1)
        {
            InitializeComponent();
            this.form1 = form1;
        }

        public void RefreshList()
        {

            listView1.Items.Clear();
            listView1.Groups.Clear();

            listView1.BeginUpdate();

            SQLiteConnection conn = null;
            try
            {
                conn = new SQLiteConnection("data source=" + Application.StartupPath + @"/storage.db");
                conn.Open();

                SQLiteCommand cmd = new SQLiteCommand();
                cmd.Connection = conn;

                cmd.CommandText = "Select * from History order by id desc, order_no desc Limit 100";
                SQLiteDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {

                    //ListViewGroup group = null;

                    //if (reader.GetInt16(5) > 0)
                    //{
                    //    foreach(ListViewGroup _group in listView1.Groups)
                    //    {
                    //        if(_group.Tag.Equals(reader.GetDateTime(3).ToString("yyyy年MM月dd HH时mm分")))
                    //        {
                    //            group = _group;
                    //            break;
                    //        }
                    //    }
                    //}

                    //if (group == null)
                    //{
                    //    group = new ListViewGroup();

                    //    group.Header = reader.GetDateTime(3).ToString("yyyy年MM月dd HH时mm分");

                    //    group.Tag = group.Header;

                    //    //if (reader.GetInt16(5) > 0)
                    //    //{
                    //    //    group.Tag = reader.GetInt16(5);
                    //    //}
                    //    //else
                    //    //{
                    //    //    group.Tag = reader.GetInt16(0);
                    //    //}

                    //    listView1.Groups.Add(group);
                    //}

                    ListViewItem item = new ListViewItem();

                    //item.Group = group;

                    item.ImageIndex = 0;

                    item.Tag = reader.GetInt16(0);

                    item.Text = reader.GetDateTime(3).ToString();
                    item.Name = reader.GetInt16(0) + "";

                    String phoneNo = reader.GetString(1);
                    if (phoneNo.StartsWith("86"))
                    {
                        phoneNo = phoneNo.Substring(2);
                    }

                    String phoneTo = reader.GetString(7);

                    if (phoneTo.StartsWith("86"))
                    {
                        phoneTo = phoneTo.Substring(2);
                    }

                    item.SubItems.Add(phoneNo);
                    item.SubItems.Add(phoneTo);
                    item.SubItems.Add(reader.GetString(2));

                    listView1.Items.Add(item);

                    
                }
                reader.Close();

            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (conn != null) conn.Close();
                listView1.EndUpdate();
            }

        }

        private void HistoryForm_Load(object sender, EventArgs e)
        {

        }

        private void 刷新ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.RefreshList();
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if(listView1.SelectedItems.Count > 0)
            {
                SendForm sendForm = new SendForm(this.form1, listView1.SelectedItems[0].SubItems[1].Text, listView1.SelectedItems[0].SubItems[3].Text);
                sendForm.Show();
            }
        }

        private void 写短信ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SendForm sendForm = new SendForm(this.form1, "", "");
            sendForm.Show();
        }
    }
}

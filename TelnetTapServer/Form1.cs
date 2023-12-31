using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TelnetTapServer
{
    public partial class Form1 : Form
    {
        TelnetServer server = null;
        public Form1()
        {
            InitializeComponent();
            this.server = new TelnetServer(IPAddress.Any, 12345);
            this.server.StartAsync();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //this.server.Send(this.textBox1.Text);
        }
    }
}

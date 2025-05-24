using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace M2Mod
{
    public partial class GetMappingsForm : Form
    {
        private const string nonClickablePart = "Download latest community listfile at: ";
        private const string downloadUrl = "https://github.com/wowdev/wow-listfile/releases";

        public GetMappingsForm()
        {
            InitializeComponent();

            this.Icon = Properties.Resources.Icon;

            linkLabel.Text = nonClickablePart + downloadUrl;
            linkLabel.Links.Add(nonClickablePart.Length, downloadUrl.Length);
        }

        private void linkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(downloadUrl);
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }
    }
}

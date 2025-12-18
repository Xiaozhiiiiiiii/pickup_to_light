// Form1.Designer.cs
namespace SmartCap.Setup
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        // ---- 基本控制項 ----
        private System.Windows.Forms.ComboBox cmbHubList;
        private System.Windows.Forms.Button btnScan;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.ListBox lstLog;

        private System.Windows.Forms.Label lblAddress;
        private System.Windows.Forms.TextBox txtAddress;
        private System.Windows.Forms.Button btnAssign;

        private System.Windows.Forms.Button btnPre;
        private System.Windows.Forms.Button btnPost;
        private System.Windows.Forms.Button btnPolling;

        // ---- PRE / POST 外觀選擇控件 ----
        private System.Windows.Forms.ComboBox cmbPreColor;
        private System.Windows.Forms.ComboBox cmbPreEffect;
        private System.Windows.Forms.TextBox txtPreText;
        private System.Windows.Forms.Label lblPreColor;
        private System.Windows.Forms.Label lblPreText;
        private System.Windows.Forms.Label lblPreEffect;

        private System.Windows.Forms.ComboBox cmbPostColor;
        private System.Windows.Forms.ComboBox cmbPostEffect;
        private System.Windows.Forms.TextBox txtPostText;
        private System.Windows.Forms.Label lblPostColor;
        private System.Windows.Forms.Label lblPostText;
        private System.Windows.Forms.Label lblPostEffect;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            this.cmbHubList = new System.Windows.Forms.ComboBox();
            this.btnScan = new System.Windows.Forms.Button();
            this.btnConnect = new System.Windows.Forms.Button();
            this.lstLog = new System.Windows.Forms.ListBox();

            this.lblAddress = new System.Windows.Forms.Label();
            this.txtAddress = new System.Windows.Forms.TextBox();
            this.btnAssign = new System.Windows.Forms.Button();

            this.btnPre = new System.Windows.Forms.Button();
            this.btnPost = new System.Windows.Forms.Button();
            this.btnPolling = new System.Windows.Forms.Button();

            this.cmbPreColor = new System.Windows.Forms.ComboBox();
            this.cmbPreEffect = new System.Windows.Forms.ComboBox();
            this.txtPreText = new System.Windows.Forms.TextBox();
            this.lblPreColor = new System.Windows.Forms.Label();
            this.lblPreText = new System.Windows.Forms.Label();
            this.lblPreEffect = new System.Windows.Forms.Label();

            this.cmbPostColor = new System.Windows.Forms.ComboBox();
            this.cmbPostEffect = new System.Windows.Forms.ComboBox();
            this.txtPostText = new System.Windows.Forms.TextBox();
            this.lblPostColor = new System.Windows.Forms.Label();
            this.lblPostText = new System.Windows.Forms.Label();
            this.lblPostEffect = new System.Windows.Forms.Label();

            // --- Form ---
            this.SuspendLayout();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Text = "SmartCAP Setup";
            this.ClientSize = new System.Drawing.Size(980, 560);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

            // --- cmbHubList ---
            this.cmbHubList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            this.cmbHubList.Name = "cmbHubList";
            this.cmbHubList.Left = 20;
            this.cmbHubList.Top = 20;
            this.cmbHubList.Width = 360;

            // --- btnScan ---
            this.btnScan.Name = "btnScan";
            this.btnScan.Text = "掃描 HUB";
            this.btnScan.Left = 390;
            this.btnScan.Top = 18;
            this.btnScan.Width = 100;
            this.btnScan.Click += new System.EventHandler(this.btnScan_Click);

            // --- btnConnect ---
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Text = "連線";
            this.btnConnect.Left = 500;
            this.btnConnect.Top = 18;
            this.btnConnect.Width = 90;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);

            // --- lstLog ---
            this.lstLog.Name = "lstLog";
            this.lstLog.Left = 20;
            this.lstLog.Top = 320;
            this.lstLog.Width = 930;
            this.lstLog.Height = 210;

            // --- Address 配對 ---
            this.lblAddress.AutoSize = true;
            this.lblAddress.Text = "配對位址：";
            this.lblAddress.Left = 20;
            this.lblAddress.Top = 70;

            this.txtAddress.Name = "txtAddress";
            this.txtAddress.Left = 110;
            this.txtAddress.Top = 66;
            this.txtAddress.Width = 120;

            this.btnAssign.Name = "btnAssign";
            this.btnAssign.Text = "綁定位址";
            this.btnAssign.Left = 240;
            this.btnAssign.Top = 64;
            this.btnAssign.Width = 100;
            this.btnAssign.Click += new System.EventHandler(this.btnAssign_Click);

            // --- PRE 面板 ---
            this.lblPreColor.AutoSize = true;
            this.lblPreColor.Text = "PRE 顏色";
            this.lblPreColor.Left = 20;
            this.lblPreColor.Top = 120;

            this.cmbPreColor.Name = "cmbPreColor";
            this.cmbPreColor.Left = 110;
            this.cmbPreColor.Top = 116;
            this.cmbPreColor.Width = 140;

            this.lblPreText.AutoSize = true;
            this.lblPreText.Text = "PRE 文字";
            this.lblPreText.Left = 250;
            this.lblPreText.Top = 120;

            this.txtPreText.Name = "txtPreText";
            this.txtPreText.Left = 340;
            this.txtPreText.Top = 116;
            this.txtPreText.Width = 140;

            this.lblPreEffect.AutoSize = true;
            this.lblPreEffect.Text = "PRE 效果";
            this.lblPreEffect.Left = 480;
            this.lblPreEffect.Top = 120;

            this.cmbPreEffect.Name = "cmbPreEffect";
            this.cmbPreEffect.Left = 570;
            this.cmbPreEffect.Top = 116;
            this.cmbPreEffect.Width = 140;

            this.btnPre.Name = "btnPre";
            this.btnPre.Text = "套用 PRE 外觀";
            this.btnPre.Left = 750;
            this.btnPre.Top = 114;
            this.btnPre.Width = 180;
            this.btnPre.Click += new System.EventHandler(this.btnPre_Click);

            // --- POST 面板 ---
            this.lblPostColor.AutoSize = true;
            this.lblPostColor.Text = "POST 顏色";
            this.lblPostColor.Left = 20;
            this.lblPostColor.Top = 160;

            this.cmbPostColor.Name = "cmbPostColor";
            this.cmbPostColor.Left = 110;
            this.cmbPostColor.Top = 156;
            this.cmbPostColor.Width = 140;

            this.lblPostText.AutoSize = true;
            this.lblPostText.Text = "POST 文字";
            this.lblPostText.Left = 250;
            this.lblPostText.Top = 160;

            this.txtPostText.Name = "txtPostText";
            this.txtPostText.Left = 340;
            this.txtPostText.Top = 156;
            this.txtPostText.Width = 140;

            this.lblPostEffect.AutoSize = true;
            this.lblPostEffect.Text = "POST 效果";
            this.lblPostEffect.Left = 480;
            this.lblPostEffect.Top = 160;

            this.cmbPostEffect.Name = "cmbPostEffect";
            this.cmbPostEffect.Left = 570;
            this.cmbPostEffect.Top = 156;
            this.cmbPostEffect.Width = 140;

            this.btnPost.Name = "btnPost";
            this.btnPost.Text = "套用 POST 外觀";
            this.btnPost.Left = 750;
            this.btnPost.Top = 154;
            this.btnPost.Width = 180;
            this.btnPost.Click += new System.EventHandler(this.btnPost_Click);

            // --- 輪詢示範 ---
            this.btnPolling.Name = "btnPolling";
            this.btnPolling.Text = "設定輪詢（示範）";
            this.btnPolling.Left = 20;
            this.btnPolling.Top = 210;
            this.btnPolling.Width = 160;
            this.btnPolling.Click += new System.EventHandler(this.btnPolling_Click);

            // --- Add Controls ---
            this.Controls.Add(this.cmbHubList);
            this.Controls.Add(this.btnScan);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.lstLog);

            this.Controls.Add(this.lblAddress);
            this.Controls.Add(this.txtAddress);
            this.Controls.Add(this.btnAssign);

            this.Controls.Add(this.lblPreColor);
            this.Controls.Add(this.cmbPreColor);
            this.Controls.Add(this.lblPreText);
            this.Controls.Add(this.txtPreText);
            this.Controls.Add(this.lblPreEffect);
            this.Controls.Add(this.cmbPreEffect);
            this.Controls.Add(this.btnPre);

            this.Controls.Add(this.lblPostColor);
            this.Controls.Add(this.cmbPostColor);
            this.Controls.Add(this.lblPostText);
            this.Controls.Add(this.txtPostText);
            this.Controls.Add(this.lblPostEffect);
            this.Controls.Add(this.cmbPostEffect);
            this.Controls.Add(this.btnPost);

            this.Controls.Add(this.btnPolling);

            this.ResumeLayout(false);
        }

        #endregion
    }
}

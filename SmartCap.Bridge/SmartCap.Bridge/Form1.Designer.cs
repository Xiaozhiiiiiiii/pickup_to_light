// SmartCap.Bridge / Form1.Designer.cs
namespace SmartCap.Bridge
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
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
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblHubIp = new System.Windows.Forms.Label();
            this.txtHubIp = new System.Windows.Forms.TextBox();
            this.btnConnectHub = new System.Windows.Forms.Button();
            this.lblHubStatus = new System.Windows.Forms.Label();
            this.lblOrderUrl = new System.Windows.Forms.Label();
            this.txtOrderUrl = new System.Windows.Forms.TextBox();
            this.btnGetOrder = new System.Windows.Forms.Button();
            this.btnStart = new System.Windows.Forms.Button();
            this.lblMqttStatus = new System.Windows.Forms.Label();
            this.lstLog = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(12, 9);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(860, 28);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "SmartCAP Bridge — ERP Order Picking";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblHubIp
            // 
            this.lblHubIp.AutoSize = true;
            this.lblHubIp.Location = new System.Drawing.Point(16, 52);
            this.lblHubIp.Name = "lblHubIp";
            this.lblHubIp.Size = new System.Drawing.Size(52, 15);
            this.lblHubIp.TabIndex = 1;
            this.lblHubIp.Text = "HUB IP：";
            // 
            // txtHubIp
            // 
            this.txtHubIp.Location = new System.Drawing.Point(74, 48);
            this.txtHubIp.Name = "txtHubIp";
            this.txtHubIp.PlaceholderText = "e.g. 10.0.60.96";
            this.txtHubIp.Size = new System.Drawing.Size(180, 23);
            this.txtHubIp.TabIndex = 2;
            // 
            // btnConnectHub
            // 
            this.btnConnectHub.Location = new System.Drawing.Point(260, 47);
            this.btnConnectHub.Name = "btnConnectHub";
            this.btnConnectHub.Size = new System.Drawing.Size(110, 25);
            this.btnConnectHub.TabIndex = 3;
            this.btnConnectHub.Text = "Connect HUB";
            this.btnConnectHub.UseVisualStyleBackColor = true;
            this.btnConnectHub.Click += new System.EventHandler(this.btnConnectHub_Click);
            // 
            // lblHubStatus
            // 
            this.lblHubStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lblHubStatus.Location = new System.Drawing.Point(380, 51);
            this.lblHubStatus.Name = "lblHubStatus";
            this.lblHubStatus.Size = new System.Drawing.Size(492, 18);
            this.lblHubStatus.TabIndex = 4;
            this.lblHubStatus.Text = "HUB: Not connected";
            // 
            // lblOrderUrl
            // 
            this.lblOrderUrl.AutoSize = true;
            this.lblOrderUrl.Location = new System.Drawing.Point(16, 89);
            this.lblOrderUrl.Name = "lblOrderUrl";
            this.lblOrderUrl.Size = new System.Drawing.Size(69, 15);
            this.lblOrderUrl.TabIndex = 5;
            this.lblOrderUrl.Text = "Order URL：";
            // 
            // 
            // txtOrderUrl
            // 
            this.txtOrderUrl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtOrderUrl.Location = new System.Drawing.Point(100, 86);
            this.txtOrderUrl.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.txtOrderUrl.Name = "txtOrderUrl";
            this.txtOrderUrl.PlaceholderText = "http://<ERP>:5055/orders/current";
            this.txtOrderUrl.Size = new System.Drawing.Size(550, 25);
            this.txtOrderUrl.TabIndex = 6;

            // 
            // btnGetOrder
            // 
            this.btnGetOrder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnGetOrder.Location = new System.Drawing.Point(665, 85);
            this.btnGetOrder.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.btnGetOrder.Name = "btnGetOrder";
            this.btnGetOrder.Size = new System.Drawing.Size(110, 27);
            this.btnGetOrder.TabIndex = 7;
            this.btnGetOrder.Text = "Get Order";
            this.btnGetOrder.UseVisualStyleBackColor = true;
          
            // 
            // btnStart
            // 
            this.btnStart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnStart.Location = new System.Drawing.Point(785, 85);
            this.btnStart.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(110, 27);
            this.btnStart.TabIndex = 8;
            this.btnStart.Text = "Start Picking";
            this.btnStart.UseVisualStyleBackColor = true;

            //   this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // lblMqttStatus
            // 
            this.lblMqttStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lblMqttStatus.Location = new System.Drawing.Point(16, 122);
            this.lblMqttStatus.Name = "lblMqttStatus";
            this.lblMqttStatus.Size = new System.Drawing.Size(856, 18);
            this.lblMqttStatus.TabIndex = 9;
            this.lblMqttStatus.Text = "MQTT: (unused)";
            // 
            // lstLog
            // 
            this.lstLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lstLog.IntegralHeight = false;
            this.lstLog.HorizontalScrollbar = true;
            this.lstLog.ItemHeight = 15;
            this.lstLog.Location = new System.Drawing.Point(16, 151);
            this.lstLog.Name = "lstLog";
            this.lstLog.Size = new System.Drawing.Size(856, 358);
            this.lstLog.TabIndex = 10;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(884, 521);
            this.Controls.Add(this.lstLog);
            this.Controls.Add(this.lblMqttStatus);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.btnGetOrder);
            this.Controls.Add(this.txtOrderUrl);
            this.Controls.Add(this.lblOrderUrl);
            this.Controls.Add(this.lblHubStatus);
            this.Controls.Add(this.btnConnectHub);
            this.Controls.Add(this.txtHubIp);
            this.Controls.Add(this.lblHubIp);
            this.Controls.Add(this.lblTitle);
            this.MinimumSize = new System.Drawing.Size(720, 480);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "SmartCAP Bridge";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblHubIp;
        public System.Windows.Forms.TextBox txtHubIp;
        private System.Windows.Forms.Button btnConnectHub;
        public System.Windows.Forms.Label lblHubStatus;
        private System.Windows.Forms.Label lblOrderUrl;
        public System.Windows.Forms.TextBox txtOrderUrl;
        private System.Windows.Forms.Button btnGetOrder;
        private System.Windows.Forms.Button btnStart;
        public System.Windows.Forms.Label lblMqttStatus;
        public System.Windows.Forms.ListBox lstLog;
    }
}

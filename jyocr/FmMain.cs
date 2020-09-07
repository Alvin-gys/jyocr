﻿using System;
using System.Windows.Forms;
using jyocr.Unit;
using System.Drawing;
using System.Runtime.InteropServices;
using jyocr.Properties;

namespace jyocr
{
    public partial class FmMain : Form
    {

        FmCutPic cutter = null;

        #region 窗口四边透明阴影
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
            int nLeftRect, // x-coordinate of upper-left corner
            int nTopRect, // y-coordinate of upper-left corner
            int nRightRect, // x-coordinate of lower-right corner
            int nBottomRect, // y-coordinate of lower-right corner
            int nWidthEllipse, // height of ellipse
            int nHeightEllipse // width of ellipse
        );

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        public static extern int DwmIsCompositionEnabled(ref int pfEnabled);

        private bool m_aeroEnabled;                     // variables for box shadow
        private const int CS_DROPSHADOW = 0x00020000;
        private const int WM_NCPAINT = 0x0085;
        private const int WM_ACTIVATEAPP = 0x001C;

        public struct MARGINS                           // struct for box shadow
        {
            public int leftWidth;
            public int rightWidth;
            public int topHeight;
            public int bottomHeight;
        }

        private const int WM_NCHITTEST = 0x84;          // variables for dragging the form
        private const int HTCLIENT = 0x1;
        private const int HTCAPTION = 0x2;

        protected override CreateParams CreateParams
        {
            get
            {
                m_aeroEnabled = CheckAeroEnabled();
                CreateParams cp = base.CreateParams;
                if (!m_aeroEnabled)
                    cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        private bool CheckAeroEnabled()
        {
            if (Environment.OSVersion.Version.Major >= 6)
            {
                int enabled = 0;
                DwmIsCompositionEnabled(ref enabled);
                return (enabled == 1) ? true : false;
            }
            return false;
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_NCPAINT:                        // box shadow
                    if (m_aeroEnabled)
                    {
                        var v = 2;
                        DwmSetWindowAttribute(this.Handle, 2, ref v, 4);
                        MARGINS margins = new MARGINS()
                        {
                            bottomHeight = 1,
                            leftWidth = 1,
                            rightWidth = 1,
                            topHeight = 1
                        };
                        DwmExtendFrameIntoClientArea(this.Handle, ref margins);
                    }
                    break;
                case 0x0312:
                    if (m.WParam.ToInt32() == 200)
                    {
                        ButtonCutPic_Click(null, null);
                    }
                    break;
                default:
                    break;
            }
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)     // drag the form
                m.Result = (IntPtr)HTCAPTION;
        }
        #endregion

        public FmMain()
        {
            m_aeroEnabled = false;
            InitializeComponent();
        }

        #region 软件加载
        private void FormMain_Load(object sender, EventArgs e)
        {
            // RichTextBox 段落缩进
            RichTextBoxValue.SelectionIndent = 40;
            RichTextBoxValue.SelectionHangingIndent = -35;

            // RichTextBox 拖放事件绑定
            RichTextBoxValue.AllowDrop = true;
            RichTextBoxValue.DragEnter += new DragEventHandler(FormMain_DragEnter);
            RichTextBoxValue.DragDrop += new DragEventHandler(FormMain_DragDrop);

            // 读取 ini 配置
            IniHelper.IniLoad("Setting.ini");
            OCRHelper.ApiKey = IniHelper.GetValue("百度接口", "API Key");
            OCRHelper.SecretKey = IniHelper.GetValue("百度接口", "Secret Key");
            OCRHelper.AccessToken = IniHelper.GetValue("百度接口", "Access Token");
            string check = IniHelper.GetValue("百度接口", "使用高精度接口");
            OCRHelper.Accurate = check == "" ? false : bool.Parse(check);

            // 判断 token 是否过期
            OCRHelper.DateToken = IniHelper.GetValue("百度接口", "Date Token");
            TimeSpan day = OCRHelper.DateToken == "" ? TimeSpan.MaxValue : DateTime.Now - DateTime.Parse(OCRHelper.DateToken);
            if (day.Days >= 30 && OCRHelper.ApiKey != "" && OCRHelper.SecretKey != "")
            {
                try
                {
                    string token = OCRHelper.GetBaiduToken(OCRHelper.ApiKey, OCRHelper.SecretKey);
                    if (token.Contains("错误"))
                    {
                        MessageBox.Show(this, token, "错误");
                    }
                    else
                    {
                        IniHelper.SetValue("百度接口", "Access Token", token);
                        IniHelper.SetValue("百度接口", "Date Token", DateTime.Now.ToString("yyyy-MM-dd"));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "错误");
                }
            }

            // 注册热键
            string value = IniHelper.GetValue("热键", "截图识别");
            if (value != "" && value != "请按下快捷键")
            {
                HotKey.SetHotkey(Handle, "None", "F4", value, 200);
            }
        }
        #endregion


        private void FmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            // 卸载热键
            HotKey.UnregisterHotKey(Handle, 200);
        }


        #region 浏览文件按钮
        private void ButtonFile_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "图片文件 (*.jpg,*.jpeg,*.png,*.bmp)|*.jgp;*.jpeg;*.png;*.bmp;"; //设置多文件格式
            if (this.openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                ButtonPart.BackgroundImage = 自动分段ToolStripMenuItem.Image;
                RichTextBoxValue.Text = OCRHelper.BaiduBasic(openFileDialog1.FileName);
            }
        }
        #endregion

        #region 截图识别按钮
        private void ButtonCutPic_Click(object sender, EventArgs e)
        {
            this.Visible = false; // 截图时隐藏本窗体
            System.Threading.Thread.Sleep(200); // 延时，避免把本窗体也截下来
            ShowCutPic();  // 截图功能
            this.Visible = true;
            Application.DoEvents(); // DoEvents()将强制处理消息队列中的所有消息
            try
            {
                if (Clipboard.ContainsImage())
                {
                    ButtonPart.BackgroundImage = 自动分段ToolStripMenuItem.Image;
                    toolTip1.SetToolTip(ButtonPart, "自动分段");
                    Image img = Clipboard.GetImage();  // 获取剪切板图片
                    RichTextBoxValue.Text = OCRHelper.BaiduBasic("", img); // 识别剪切板图片的文字
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "错误");
            }
        }
        #endregion

        #region 截图功能
        protected void ShowCutPic()
        {
            Bitmap CatchBmp = new Bitmap(Screen.AllScreens[0].Bounds.Width, Screen.AllScreens[0].Bounds.Height);
            // 创建一个画板，让我们可以在画板上画图
            // 这个画板也就是和屏幕大小一样大的图片
            // 我们可以通过Graphics这个类在这个空白图片上画图
            Graphics g = Graphics.FromImage(CatchBmp);
            // 把屏幕图片拷贝到我们创建的空白图片 CatchBmp中
            g.CopyFromScreen(new Point(0, 0), new Point(0, 0), new Size(Screen.AllScreens[0].Bounds.Width, Screen.AllScreens[0].Bounds.Height));

            // 创建截图窗体
            cutter = new FmCutPic();
            cutter.Image = CatchBmp;
            cutter.Cursor = Cursors.Cross;
            cutter.ShowDialog();
        }
        #endregion

        #region 文件拖动结束
        private void FormMain_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                ButtonPart.BackgroundImage = 自动分段ToolStripMenuItem.Image;
                string path = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
                RichTextBoxValue.Text = OCRHelper.BaiduBasic(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "错误");
            }
        }
        #endregion

        #region 文件拖动到工作区时
        private void FormMain_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Link;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }
        #endregion

        #region 复制、清空按钮
        private void ButtonDelete_Click(object sender, EventArgs e)
        {
            RichTextBoxValue.Text = "";
        }

        private void ButtonCopy_Click(object sender, EventArgs e)
        {
            Clipboard.SetData(DataFormats.Text, RichTextBoxValue.Text);
        }
        #endregion

        #region 段落按钮菜单功能
        private void 自动分段ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonPart.BackgroundImage = 自动分段ToolStripMenuItem.Image;
            toolTip1.SetToolTip(ButtonPart, "自动分段");
            RichTextBoxValue.Text = OCRHelper.typeset_txt;
        }

        private void 段落拆分ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonPart.BackgroundImage = 段落拆分ToolStripMenuItem.Image;
            toolTip1.SetToolTip(ButtonPart, "段落拆分");
            RichTextBoxValue.Text = OCRHelper.split_txt;
        }

        private void 段落合并ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonPart.BackgroundImage = 段落合并ToolStripMenuItem.Image;
            toolTip1.SetToolTip(ButtonPart, "段落合并");
            RichTextBoxValue.Text = RichTextBoxValue.Text.Replace("\n", "").Replace("\r", "");
        }

        private void ButtonPart_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                MenuPart.Show((Button)sender, new Point(ButtonPart.Left - ButtonPart.Width - 30, ButtonPart.Top + ButtonPart.Height));
            }
        }
        #endregion

        private void RichTextBoxValue_TextChanged(object sender, EventArgs e)
        {
            LabelWordCount.Text = "字数：" + RichTextBoxValue.Text.Length.ToString();
        }

        #region 设置按钮
        private void ButtonSet_Click(object sender, EventArgs e)
        {
            Form frm = new FmSetting();
            frm.ShowDialog();

            // 卸载热键重新注册
            string value = IniHelper.GetValue("热键", "截图识别");
            if (value != "" && value != "请按下快捷键")
            {
                HotKey.UnregisterHotKey(Handle, 200);
                HotKey.SetHotkey(Handle, "None", "F4", value, 200);
            }
        }
        #endregion

        #region 置顶按钮
        private void ButtonTop_Click(object sender, EventArgs e)
        {
            if (this.TopMost)
            {
                this.TopMost = false;
                ButtonTop.BackgroundImage = Resources.取消置顶;
                toolTip1.SetToolTip(ButtonTop, "取消置顶");
            }
            else
            {
                this.TopMost = true;
                ButtonTop.BackgroundImage = Resources.置顶;
                toolTip1.SetToolTip(ButtonTop, "置顶");
            }
        }
        #endregion

        #region 语言选项按钮
        private void ButtonLang_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (OCRHelper.Accurate)
                {
                    MenuLangAccurate.Show((Button)sender, new Point(ButtonLang.Left - ButtonLang.Width - 10, ButtonLang.Top + ButtonLang.Height));
                }
                else
                {
                    MenuLangBasic.Show((Button)sender, new Point(ButtonLang.Left - ButtonLang.Width - 10, ButtonLang.Top + ButtonLang.Height));
                }
            }
        }

        private void 中英混合ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "中英混合";
            OCRHelper.Language = "CHN_ENG";
        }

        private void 英文ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "英文";
            OCRHelper.Language = "ENG";
        }

        private void 日语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "日语";
            OCRHelper.Language = "JAP";
        }

        private void 韩语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "韩语";
            OCRHelper.Language = "KOR";
        }

        private void 法语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "法语";
            OCRHelper.Language = "FRE";
        }

        private void 西班牙语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "西班牙语";
            OCRHelper.Language = "SPA";
        }

        private void 葡萄牙语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "葡萄牙语";
            OCRHelper.Language = "POR";
        }

        private void 德语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "德语";
            OCRHelper.Language = "GER";
        }

        private void 意大利语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "意大利语";
            OCRHelper.Language = "ITA";
        }

        private void 俄语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "俄语";
            OCRHelper.Language = "RUS";
        }

        private void 丹麦语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "丹麦语";
            OCRHelper.Language = "DAN";
        }

        private void 荷兰语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "荷兰语";
            OCRHelper.Language = "DUT";
        }

        private void 马来语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "马来语";
            OCRHelper.Language = "MAL";
        }

        private void 瑞典语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "瑞典语";
            OCRHelper.Language = "SWE";
        }

        private void 印尼语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "印尼语";
            OCRHelper.Language = "IND";
        }

        private void 波兰语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "波兰语";
            OCRHelper.Language = "POL";
        }

        private void 罗马尼亚语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "罗马尼亚";
            OCRHelper.Language = "ROM";
        }

        private void 土耳其语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "土耳其语";
            OCRHelper.Language = "TUR";
        }

        private void 希腊语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "希腊语";
            OCRHelper.Language = "GRE";
        }

        private void 匈牙利语ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "匈牙利语";
            OCRHelper.Language = "HUN";
        }

        private void 自动检测ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ButtonLang.Text = "自动检测";
            OCRHelper.Language = "auto_detect";
        }
        #endregion
    }
}

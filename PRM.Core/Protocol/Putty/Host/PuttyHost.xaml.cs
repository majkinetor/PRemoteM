﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using PRM.Core.Model;
using PRM.Core.Protocol.Putty.SSH;
using Shawn.Utils;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace PRM.Core.Protocol.Putty.Host
{
    [Obsolete]
    public partial class PuttyHost : ProtocolHostBase
    {
        [DllImport("User32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        public static extern int ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr SetFocus(HandleRef hWnd);


        private const int GWL_STYLE = (-16);
        private const int WM_CLOSE = 0x10;
        private const int WS_CAPTION = 0x00C00000;      // 	创建一个有标题框的窗口
        private const int WS_BORDER = 0x00800000;       // 	创建一个单边框的窗口
        private const int WS_THICKFRAME = 0x00040000;   // 创建一个具有可调边框的窗口
        private const int WS_VSCROLL = 0x00200000;      // 创建一个有垂直滚动条的窗口。


        private const int SW_HIDE = 0;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMAXIMIZED = 3;

        public const string PuttyExeName = "PuTTY_PRM.exe";

        private const int PuttyWindowMargin = 0;
        private Process _puttyProcess = null;
        private IntPtr _puttyHandle = IntPtr.Zero;
        private System.Windows.Forms.Panel _puttyMasterPanel = null;
        private TransparentPanel _topTransparentPanel = null;
        private Task _taksTopPanelFocus = null;
        private PuttyOptions _puttyOption = null;
        private readonly IPuttyConnectable _protocolPuttyBase = null;

        public PuttyHost(IPuttyConnectable iPuttyConnectable) : base(iPuttyConnectable.ProtocolServerBase, false)
        {
            _protocolPuttyBase = iPuttyConnectable;
            InitializeComponent();
        }

        ~PuttyHost()
        {
            ClosePutty();
        }

        public override void Conn()
        {
            Debug.Assert(ParentWindow != null);
            Debug.Assert(_protocolPuttyBase.ProtocolServerBase.Id > 0);


            // set putty bg color
            var options = SystemConfig.Instance.Theme.SelectedPuttyTheme;
            GridBg.Background = new SolidColorBrush(new Color()
            {
                A = 255,
                R = 0,
                G = 0,
                B = 0,
            });
            var bgOption = options?.First(x => x.Key == PuttyOptionKey.Colour2.ToString());
            if (bgOption != null
                && bgOption.Value.ToString().Split(',').Length == 3)
            {
                var color = bgOption.Value.ToString().Split(',');
                if (byte.TryParse(color[0], out var r)
                    && byte.TryParse(color[1], out var g)
                    && byte.TryParse(color[2], out var b))
                {
                    GridBg.Background = new SolidColorBrush(new Color()
                    {
                        A = 255,
                        R = r,
                        G = g,
                        B = b,
                    });
                }
            }


            _puttyOption = new PuttyOptions(_protocolPuttyBase.GetSessionName());
            SetPuttySessionConfig();

            _puttyHandle = IntPtr.Zero;
            //FormBorderStyle = FormBorderStyle.None;
            //WindowState = FormWindowState.Maximized;
            var tsk = new Task(InitPutty);
            tsk.Start();


            _puttyMasterPanel = new System.Windows.Forms.Panel
            {
                BackColor = System.Drawing.Color.Transparent,
                Dock = System.Windows.Forms.DockStyle.Fill,
                BorderStyle = BorderStyle.None
            };
            _puttyMasterPanel.SizeChanged += PuttyMasterPanelOnSizeChanged;
            FormsHost.Child = _puttyMasterPanel;
        }

        public override void ReConn()
        {
            ClosePutty();
            Conn();
        }

        public override void DisConn()
        {
            ClosePutty();
            base.DisConn();
        }

        private void PuttyMasterPanelOnSizeChanged(object sender, EventArgs e)
        {
            if (_puttyHandle != IntPtr.Zero)
            {
                MoveWindow(_puttyHandle, PuttyWindowMargin, PuttyWindowMargin, _puttyMasterPanel.Width - PuttyWindowMargin * 2,
                    _puttyMasterPanel.Height - PuttyWindowMargin * 2, true);
                _topTransparentPanel.Width = _puttyMasterPanel.Width;
                _topTransparentPanel.Height = _puttyMasterPanel.Height;
                MakeItFocus();
            }
        }

        private void InitPutty()
        {
            _puttyProcess = new Process();
            var ps = new ProcessStartInfo();
            //ps.FileName = Path.Combine(Environment.CurrentDirectory, PuttyExeName);

            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SystemConfig.AppName, "Putty");
            if (!Directory.Exists(appDateFolder))
                Directory.CreateDirectory(appDateFolder);
            ps.FileName = Path.Combine(appDateFolder, PuttyExeName);
            ps.WorkingDirectory = appDateFolder;
            if (!File.Exists(ps.FileName))
            {
                var putty = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/PRM.Core;component/PuTTY_PRM.exe")).Stream;
                using (var fileStream = File.Create(ps.FileName))
                {
                    putty.Seek(0, SeekOrigin.Begin);
                    putty.CopyTo(fileStream);
                }
                putty.Close();
            }
            // var arg = $"-ssh {port} {user} {pw} {server}";
            // var arg = $@" -load ""{PuttyOption.SessionName}"" {IP} -P {PORT} -l {user} -pw {pdw} -{ssh version}";
            ps.Arguments = _protocolPuttyBase.GetPuttyConnString();
            ps.WindowStyle = ProcessWindowStyle.Minimized;
            _puttyProcess.StartInfo = ps;
            _puttyProcess.Start();
            _puttyProcess.Exited += (sender, args) => _puttyProcess = null;
            _puttyProcess.Refresh();
            _puttyProcess.WaitForInputIdle();
            _puttyHandle = _puttyProcess.MainWindowHandle;

            Dispatcher.Invoke(() =>
            {
                ShowWindow(_puttyHandle, SW_SHOWMAXIMIZED);
                int lStyle = GetWindowLong(_puttyHandle, GWL_STYLE);
                lStyle &= ~WS_CAPTION; // no title
                lStyle &= ~WS_BORDER;  // no border
                lStyle &= ~WS_THICKFRAME;
                SetWindowLong(_puttyHandle, GWL_STYLE, lStyle);
                MoveWindow(_puttyHandle, PuttyWindowMargin, PuttyWindowMargin, _puttyMasterPanel.Width - PuttyWindowMargin * 2, _puttyMasterPanel.Height - PuttyWindowMargin * 2, true);
                DelPuttySessionConfig();

                _topTransparentPanel = new TransparentPanel
                {
                    Parent = _puttyMasterPanel,
                    PuttyHandle = _puttyHandle
                };
                _topTransparentPanel.BringToFront();
                SetParent(_puttyHandle, _puttyMasterPanel.Handle);
                MakeItFocus();
            });
        }

        public void ClosePutty()
        {
            DelPuttySessionConfig();
            try
            {
                if (_puttyProcess?.HasExited == false)
                {
                    _puttyProcess?.Kill();
                }
                _puttyProcess = null;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
        }


        private void SetPuttySessionConfig()
        {
            // set key
            if (_protocolPuttyBase is ProtocolServerSSH server
                && !string.IsNullOrEmpty(server.PrivateKey))
            {
                var ppk = server.PrivateKey;
                if (SystemConfig.Instance.DataSecurity.Rsa != null)
                    ppk = SystemConfig.Instance.DataSecurity.Rsa.DecodeOrNull(ppk) ?? ppk;
                Debug.Assert(ppk != null);
                _puttyOption.Set(PuttyOptionKey.PublicKeyFile, ppk);
            }

            // set color theme
            _puttyOption.Set(PuttyOptionKey.FontHeight, SystemConfig.Instance.Theme.PuttyFontSize);
            var options = SystemConfig.Instance.Theme.SelectedPuttyTheme;
            if (options != null)
                foreach (var option in options)
                {
                    try
                    {
                        if (Enum.TryParse(option.Key, out PuttyOptionKey key))
                        {
                            if (option.ValueKind == RegistryValueKind.DWord)
                                _puttyOption.Set(key, (int)(option.Value));
                            else
                                _puttyOption.Set(key, (string)option.Value);
                        }
                    }
                    catch (Exception)
                    {
                        SimpleLogHelper.Warning($"Putty theme error: can't set up key(value)=> {option.Key}({option.ValueKind})");
                    }
                }


            //_puttyOption.Set(PuttyRegOptionKey.Colour0, "255,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour1, "255,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour2, "51,51,51");
            //_puttyOption.Set(PuttyRegOptionKey.Colour3, "85,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour4, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour5, "0,255,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour6, "77,77,77");
            //_puttyOption.Set(PuttyRegOptionKey.Colour7, "85,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour8, "187,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour9, "255,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour10, "152,251,152");
            //_puttyOption.Set(PuttyRegOptionKey.Colour11, "85,255,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour12, "240,230,140");
            //_puttyOption.Set(PuttyRegOptionKey.Colour13, "255,255,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour14, "205,133,63");
            //_puttyOption.Set(PuttyRegOptionKey.Colour15, "135,206,235");
            //_puttyOption.Set(PuttyRegOptionKey.Colour16, "255,222,173");
            //_puttyOption.Set(PuttyRegOptionKey.Colour17, "255,85,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour18, "255,160,160");
            //_puttyOption.Set(PuttyRegOptionKey.Colour19, "255,215,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour20, "245,222,179");
            //_puttyOption.Set(PuttyRegOptionKey.Colour21, "255,255,255");


            //_puttyOption.Set(PuttyRegOptionKey.Colour0, "192,192,192");
            //_puttyOption.Set(PuttyRegOptionKey.Colour1, "255,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour2, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour3, "85,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour4, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour5, "0,255,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour6, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour7, "85,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour8, "255,0,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour9, "255,85,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour10,"0,255,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour11,"85,255,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour12,"187,187,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour13,"255,255,85");
            //_puttyOption.Set(PuttyRegOptionKey.Colour14,"0,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour15,"0,0,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour16,"0,0,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour17,"255,85,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour18,"0,187,187");
            //_puttyOption.Set(PuttyRegOptionKey.Colour19,"85,255,255");
            //_puttyOption.Set(PuttyRegOptionKey.Colour20,"187,187,187");
            //_puttyOption.Set(PuttyRegOptionKey.Colour21,"255,255,255");


            //_puttyOption.Set(PuttyRegOptionKey.UseSystemColours, 0);
            //_puttyOption.Set(PuttyRegOptionKey.TryPalette, 0);
            //_puttyOption.Set(PuttyRegOptionKey.ANSIColour, 1);
            //_puttyOption.Set(PuttyRegOptionKey.Xterm256Colour, 1);
            //_puttyOption.Set(PuttyRegOptionKey.BoldAsColour, 1);

            //_puttyOption.Set(PuttyRegOptionKey.Colour0, "211,215,207");
            //_puttyOption.Set(PuttyRegOptionKey.Colour1, "238,238,236");
            //_puttyOption.Set(PuttyRegOptionKey.Colour2, "46,52,54");
            //_puttyOption.Set(PuttyRegOptionKey.Colour3, "85,87,83");
            //_puttyOption.Set(PuttyRegOptionKey.Colour4, "0,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour5, "0,255,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour6, "46,52,54");
            //_puttyOption.Set(PuttyRegOptionKey.Colour7, "85,87,83");
            //_puttyOption.Set(PuttyRegOptionKey.Colour8, "204,0,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour9, "239,41,41");
            //_puttyOption.Set(PuttyRegOptionKey.Colour10,"78,154,6");
            //_puttyOption.Set(PuttyRegOptionKey.Colour11,"138,226,52");
            //_puttyOption.Set(PuttyRegOptionKey.Colour12,"196,160,0");
            //_puttyOption.Set(PuttyRegOptionKey.Colour13,"252,233,79");
            //_puttyOption.Set(PuttyRegOptionKey.Colour14,"52,101,164");
            //_puttyOption.Set(PuttyRegOptionKey.Colour15,"114,159,207");
            //_puttyOption.Set(PuttyRegOptionKey.Colour16,"117,80,123");
            //_puttyOption.Set(PuttyRegOptionKey.Colour17,"173,127,168");
            //_puttyOption.Set(PuttyRegOptionKey.Colour18,"6,152,154");
            //_puttyOption.Set(PuttyRegOptionKey.Colour19,"52,226,226");
            //_puttyOption.Set(PuttyRegOptionKey.Colour20,"211,215,207");
            //_puttyOption.Set(PuttyRegOptionKey.Colour21,"238,238,236");


            _puttyOption.SaveToPuttyRegistryTable();
        }

        private void DelPuttySessionConfig()
        {
            _puttyOption?.DelFromPuttyRegistryTable();
            _puttyOption = null;
        }

        public override void GoFullScreen()
        {
            //throw new NotSupportedException("putty session can not go to full-screen mode!");
        }

        public override bool IsConnected()
        {
            return true;
        }

        public override bool IsConnecting()
        {
            return false;
        }

        public override void MakeItFocus()
        {
            if (_topTransparentPanel != null)
            {
                lock (MakeItFocusLocker1)
                {
                    // hack technology
                    // when tab selection changed it call MakeItFocus(), but get false by _topTransparentPanel.Focus() since the panel was not print yet.
                    // then I have not choice but set a task to wait '_topTransparentPanel.Focus()' returns 'true'.

                    if (_taksTopPanelFocus?.Status != TaskStatus.Running)
                    {
                        _taksTopPanelFocus = new Task(() =>
                        {
                            lock (MakeItFocusLocker2)
                            {
                                bool flag = false;
                                int nCount = 50; // don't let it runs forever.
                                while (!flag && nCount > 0)
                                {
                                    --nCount;
                                    Dispatcher.Invoke(() =>
                                    {
                                        flag = _topTransparentPanel.Focus();
                                    });
                                    Thread.Sleep(10);
                                }
                            }
                        });
                        _taksTopPanelFocus.Start();
                    }
                }
            }
        }
    }

    public sealed class TransparentPanel : System.Windows.Forms.Panel
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public IntPtr PuttyHandle = IntPtr.Zero;
        public TransparentPanel()
        {
            BackColor = System.Drawing.Color.Transparent;
            Dock = System.Windows.Forms.DockStyle.Fill;
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                return cp;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Do not paint background.
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (PuttyHandle != IntPtr.Zero)
            {
                PostMessage(PuttyHandle, (uint)msg.Msg, msg.WParam, msg.LParam);
            }

            return true;
        }

        [System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
        protected override void WndProc(ref Message m)
        {
            // Ignore Ctrl+RightClick to prevent Putty context menu
            if (m.Msg == 516 && ((int)m.WParam & 0x0008) == 0x0008)
                return;

            switch (m.Msg)
            {
                case 33: //WM_MOUSEACTIVATE
                case 160: //WM_NCMOUSEMOVE
                case 512: //WM_MOUSEMOVE
                case 513: //WM_LBUTTONDOWN
                case 514: //WM_LBUTTONUP
                case 515: //WM_LBUTTONDBLCLK
                case 516: //WM_RBUTTONDOWN
                case 517: //WM_RBUTTONUP
                case 518: //WM_RBUTTONDBLCLK
                case 519: //WM_MBUTTONDOWN
                case 520: //WM_MBUTTONUP
                case 521: //WM_MBUTTONDBLCLK
                case 522: //WM_MOUSEWHEEL
                case 526: //WM_MOUSEHWHEEL
                case 672: //WM_NCMOUSEHOVER
                case 673: //WM_MOUSEHOVER
                case 674: //WM_NCMOUSELEAVE
                case 675: //WM_MOUSELEAVE
                    {
                        if (PuttyHandle != IntPtr.Zero)
                        {
                            PostMessage(PuttyHandle, (uint)m.Msg, m.WParam, m.LParam);
                        }
                    }
                    break;
                default:
                    base.WndProc(ref m);
                    break;
            }
        }
    }
}

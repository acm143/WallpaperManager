﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LanceTools.FormUtil;
using Microsoft.Win32;
using WallpaperManager.ApplicationData;
using WallpaperManager.Options;
using WallpaperManager.Tagging;

namespace WallpaperManager
{
    public partial class WallpaperManager : Form
    {
        // Essentials
        private const int SetDeskWallpaper = 20;
        private const int UpdateIniFile = 0x01;
        private const int SendWinIniChange = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        #region Wallpaper Fade
        // Fade Wallpaper | Not yet implemented
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SendMessageTimeout(
            IntPtr hWnd,      // handle to destination window
            uint Msg,       // message
            IntPtr wParam,  // first message parameter
            IntPtr lParam,   // second message parameter
            uint fuFlags,
            uint uTimeout,
            out IntPtr result

        );
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, IntPtr ZeroOnly);

        IntPtr result = IntPtr.Zero;
        //? The below should allow for wallpaper fading with the above code, test this out sometime
        //? SendMessageTimeout(FindWindow("Program", IntPtr.Zero), 0x52c, IntPtr.Zero, IntPtr.Zero, 0, 500, out result);
        #endregion

        private GlobalHotkey ghShiftAlt;

        public WallpaperManager()
        {
            InitializeComponent();

            Application.ApplicationExit += OnApplicationExit;
            this.Load += OnLoad;
            this.Resize += OnResize;

            InitializeImageSelector();
            InitializeImageInspector();

            PathData.Validate(); // ensures that all needed folders exist
            WallpaperData.Initialize(this, false); // loads in all necessary data

            InitializeTimer();
            InitializeKeys();
            InitializeNotifyIcon();
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            Debug.WriteLine("OnApplicationExit is incomplete");

            //WallpaperData.SaveDefaultData();

            // Set Default theme if enabled
            /*
            if (setDefaultThemeOnExit)
            {
                SetDefaultTheme();
            }
            */

            // Unregister keys
            /*
            foreach (GlobalHotKey key in hotkeys)
            {
                if (key.Unregister())
                {
                    //! You still need to implement the ToString method for GlobalHotKey!!!
                    MessageBox.Show("Hotkey " + key.ToString() + " failed to unregister!");
                }
            }
            */
        }

        private void OnLoad(object sender, EventArgs e)
        {
            Debug.WriteLine("OnLoad is incomplete");

            /*
            //? You should probably move this to InitializeKeys!
            //? Also check if this even needs to be placed in OnLoad as InitializeKeys() seems more appropriate
            foreach (GlobalHotKey key in hotkeys)
            {
                if (!key.Register())
                {
                    //! You still need to implement the ToString method for GlobalHotKey!!!
                    //? Consider adding an option for auto-loading the default theme if a hotkey fails to register
                    MessageBox.Show("Hotkey " + key.ToString() + " failed to register!");
                }
            }
            */
        }

        private void OnResize(object sender, EventArgs e)
        {
            // minimizes the program to the system tray, "hiding" it
            if (WindowState == FormWindowState.Minimized && minimizeToSystemTray)
            {
                Hide();
                notifyIcon1.Visible = true;
            }
        }

        #region Hotkeys
        private void InitializeKeys()
        {
            //? See if you actually need this, makes it to where key presses are detected across all forms, does this include while the app is closed too?
            //? If the above is true then this will need to have a setting in the options panel
            this.KeyPreview = true;

            // GlobalHotkey
            ghShiftAlt = new GlobalHotkey(VirtualKey.SHIFT + VirtualKey.ALT, Keys.None, this);
            //ghDivide = new GlobalHotkey(VirtualKey.NOMOD, Keys.Divide, this);
            //ghMultiply = new GlobalHotkey(VirtualKey.NOMOD, Keys.Multiply, this);
            //ghNumPad5 = new GlobalHotkey(VirtualKey.NOMOD, Keys.NumPad5, this);

            if (!ghShiftAlt.Register())
            {
                MessageBox.Show("Hotkey failed to register!");
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == VirtualKey.WM_HOTKEY_MSG_ID)
            {
                HandleHotKey();
            }

            base.WndProc(ref m);
        }

        private void HandleHotKey()
        {
            if (OptionsData.EnableGlobalHotkey)
            {
                WallpaperData.SaveData(PathData.ActiveWallpaperTheme);
                WallpaperData.LoadDefaultTheme();
            }
        }
        #endregion

        #region Button Events
        private void buttonNextWallpaper_Click(object sender, EventArgs e)
        {
            NextWallpaper();
        }

        private void buttonPreviousWallpaper_Click(object sender, EventArgs e)
        {
            PreviousWallpaper();
        }

        private void buttonOptions_Click(object sender, EventArgs e)
        {
            using (OptionsForm f = new OptionsForm())
            {
                f.ShowDialog();
            }
        }

        private TagForm activeTagForm;
        private void buttonTagSettings_Click(object sender, EventArgs e)
        {
            // Show() will dispose itself on close, ShowDialog() will not however | Show() will allow you to click on other controls while ShowDialog() will not
            if (activeTagForm == null || activeTagForm.IsDisposed)
            {
                activeTagForm = new TagForm();
                activeTagForm.Show();
            }
        }

        private void WallpaperManager_Click(object sender, EventArgs e)
        {
            labelTimeLeft.Focus(); // you can't focus onto the form but this acts like what you'd want
        }
        #endregion

        public void NextWallpaper()
        {
            NextWallpaper(false);
        }

        public void NextWallpaper(bool ignoreErrorMessages)
        {
            if (!WallpaperData.FileDataIsEmpty())
            {
                if (!WallpaperData.NoImagesActive() && WallpaperData.GetAllRankedImages().Length != 0)
                {
                    //TODO Find a way to prevent the first previous wallpaper from being filled with empty strings
                    ResetTimer();
                    string[] previousWallpapers = new string[PathData.ActiveWallpapers.Length];
                    PathData.ActiveWallpapers.CopyTo(previousWallpapers, 0);
                    PathData.PreviousWallpapers.Push(previousWallpapers);

                    PathData.RandomizeWallpapers();
                    SetWallpaper();
                }
                else
                {
                    if (!ignoreErrorMessages) MessageBox.Show("No active wallpapers were found");
                }
            }
            else
            {
                if (!ignoreErrorMessages) MessageBox.Show("Add some wallpapers first! Use the Add Folder button to add a collection of images that'll be used as potential wallpapers");
            }
        }

        public void PreviousWallpaper()
        {
            if (PathData.PreviousWallpapers.Count > 1) // the first wallpaper will be filled with empty strings
            {
                ResetTimer();
                PathData.PreviousWallpapers.Pop().CopyTo(PathData.ActiveWallpapers, 0);
                SetWallpaper();
            }
            else
            {
                MessageBox.Show("There are no more previous wallpapers");
            }
        }

        public void ResetWallpaperManager()
        {
            ClearImageSelector();
            ClearImageFolders();
        }
    }
}

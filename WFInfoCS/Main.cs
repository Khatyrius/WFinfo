﻿using System;
using System.Threading;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace WFInfoCS
{
    class Main
    {
        public static Main INSTANCE;
        public static string appPath { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\WFInfoCS";
        public static string buildVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public static Data dataBase;
        public static Window window;
        public static Overlay[] overlays;
        public static RelicsWindow relicWindow;
        public static EquipmentWindow equipmentWindow;
        public static Settings settingsWindow;

        public Main()
        {
            INSTANCE = this;
            overlays = new Overlay[4] { new Overlay(), new Overlay(), new Overlay(), new Overlay() };
            window = new Window();
            dataBase = new Data();
            relicWindow = new RelicsWindow();
            equipmentWindow = new EquipmentWindow();
            settingsWindow = new Settings();
            Task.Factory.StartNew(new Action(ThreadedDataLoad));
        }

        public static void ThreadedDataLoad()
        {
            dataBase.Update();
            RunOnUIThread(() => { MainWindow.INSTANCE.Market_Data.Content = "Market Data: " + dataBase.marketData["timestamp"].ToString().Substring(5, 11); });
            RunOnUIThread(() => { MainWindow.INSTANCE.Drop_Data.Content = "Drop Data: " + dataBase.equipmentData["timestamp"].ToString().Substring(5, 11); });
            RunOnUIThread(() => { MainWindow.INSTANCE.Wiki_Data.Content = "Wiki Data: " + dataBase.equipmentData["rqmts_timestamp"].ToString().Substring(5, 11); });
            InitializeOverlays();
            StatusUpdate("WFInfo Initialization Complete", 0);
        }

        public static void InitializeOverlays()
        {
            double scaling = 1.0;
            Point center = new Point(Screen.PrimaryScreen.Bounds.Width / 2, Screen.PrimaryScreen.Bounds.Height / 2);

            if (Screen.PrimaryScreen.Bounds.Width * 9 > Screen.PrimaryScreen.Bounds.Height * 16)  // image is less than 16:9 aspect
                scaling *= Screen.PrimaryScreen.Bounds.Height / 1080.0;
            else
                scaling *= Screen.PrimaryScreen.Bounds.Width / 1920.0; //image is higher than 16:9 aspect
            //Initialize Overlays
            RunOnUIThread(() =>
            {
                for (int i = 0; i < overlays.Length; i++)
                {
                    //Screen.PrimaryScreen.Bounds
                    Main.overlays[i].LoadTextData("Loading...", "N/A", "N/A", "N/A", true, "N/A");
                    Main.overlays[i].Resize((int)(240 * scaling));
                    Main.overlays[i].Display(center.X + (int)(250 * scaling) * (i - 2) + (int)(10 * scaling), center.Y, 1000);
                }
            });
        }

        public static void RunOnUIThread(Action act)
        {
            MainWindow.INSTANCE.Dispatcher.Invoke(act);
        }

        public static void AddLog(string argm)
        { //write to the debug file, includes version and UTCtime
            string path = appPath + @"\Debug";
            //Console.WriteLine(argm);
            Directory.CreateDirectory(path);
            using (StreamWriter sw = File.AppendText(path + @"\debug.txt"))
            {
                sw.WriteLineAsync("[" + DateTime.UtcNow + " " + buildVersion + "] \t" + argm);
            }
        }

        public static void StatusUpdate(string message, int serverity)
        {
            MainWindow.INSTANCE.Dispatcher.Invoke(() => { MainWindow.INSTANCE.ChangeStatus(message, serverity); });
        }

        public void OnKeyAction(Keys key)
        {
            if (KeyInterop.KeyFromVirtualKey((int)key) == Settings.activationKey)
            { //check if user pressed activation key
                if (Settings.debug && (Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                {
                    Main.AddLog("Loading screenshot from file");
                    Main.StatusUpdate("Offline testing with screenshot", 0);
                    LoadScreenshot();
                } else if (Settings.debug || OCR.verifyWarframe())
                    //if (Ocr.verifyFocus()) 
                    //   Removing because a player may focus on the app during selection if they're using the window style, or they have issues, or they only have one monitor and want to see status
                    //   There's a lot of reasons why the focus won't be too useful, IMO -- Kekasi
                    Task.Factory.StartNew(() => OCR.ProcessRewardScreen());
            }
        }

        private void LoadScreenshot()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                openFileDialog.Filter = "image files (*.png)|*.png|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 2;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            foreach (string file in openFileDialog.FileNames)
                            {
                                Console.WriteLine("Testing file: " + file.ToString());

                                //Get the path of specified file
                                Bitmap image = new Bitmap(file);
                                OCR.updateWindow(image);
                                OCR.ProcessRewardScreen(image);
                            }

                        }
                        catch (Exception)
                        {
                            StatusUpdate("Faild to load image", 1);
                        }
                    });
                } else
                {
                    StatusUpdate("Faild to load image", 1);
                }
            }
        }

        //getters, boring shit
        public static string BuildVersion { get => buildVersion; }
        public string AppPath { get => appPath; }

        public static int VersionToInteger(string vers)
        {
            int ret = 0;
            string[] versParts = Regex.Replace(vers, "[^0-9.]+", "").Split('.');
            if (versParts.Length == 3)
                for (int i = 0; i < versParts.Length; i++)
                {
                    if (versParts[i].Length == 0)
                        return -1;
                    ret += Convert.ToInt32(int.Parse(versParts[i]) * Math.Pow(100, 2 - i));
                }

            return ret;
        }

        // Glob
        public static System.Globalization.CultureInfo culture = new System.Globalization.CultureInfo("en");
    }

    public class Status
    {
        public string message;
        public int serverity;

        public Status(string msg, int ser)
        {
            message = msg;
            serverity = ser;
        }
    }

}

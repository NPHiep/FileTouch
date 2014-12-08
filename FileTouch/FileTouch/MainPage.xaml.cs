using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using FileTouch.Resources;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.Streams;
using System.IO;
using System.Diagnostics;
using Windows.Networking.Sockets;
using Windows.Storage.Provider;

namespace FileTouch
{
    public partial class MainPage : PhoneApplicationPage
    {
        private long BUFFER_LENGTH = 2048;
        public static float progress;
        

        // Constructor
        public MainPage()
        {
            InitializeComponent();
            DataContext = App.myFloat;
            // Sample code to localize the ApplicationBar
            //BuildLocalizedApplicationBar();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            App.isUpLoad = true;
            FileOpenPicker fileOpenPicker = new FileOpenPicker();
            fileOpenPicker.FileTypeFilter.Add("*");
            fileOpenPicker.PickSingleFileAndContinue();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var app = App.Current as App;
            if (app.FilePickerContinuationArgs != null)
            {
                if (app.FilePickerContinuationArgs.Files != null && app.FilePickerContinuationArgs.Files.Count > 0)
                {
                    StorageFile file = app.FilePickerContinuationArgs.Files[0];
                    app.FilePickerContinuationArgs = null;
                    SocketClient client = new SocketClient();
                    // Attempt to connect to the echo server
                   string result = client.Connect("168.63.253.120", 8888);
                  //  this.Dispatcher.BeginInvoke(() =>
                    //{
                        client.SendFile(file);
                    //});
                     
                }
            }
            if(app.FileSavePickerContinuationArgs != null)
            {
                StorageFile file = app.FileSavePickerContinuationArgs.File;
                if (file != null)
                {
                    // Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync.
                    CachedFileManager.DeferUpdates(file);
                    // write to file
                    SaveFile(file);
                }
                else
                {
                    fileName.Text = "Operation cancelled.";
                }
            }
            base.OnNavigatedTo(e);
        }
        private async void SaveFile(StorageFile file)
        {
         
            SocketClient client = new SocketClient();
            // Attempt to connect to the echo server
            string result = client.Connect("168.63.253.120", 8888);
           client.ReceiveFile( file, FileID.Text);
        }
     
        /// <summary>
        /// Handle the btnEcho_Click event by sending text to the echo server 
        /// and outputting the response
        /// </summary>
        private void Button_Clicks(object sender, RoutedEventArgs e)
        {
            App.isUpLoad = false;
            FileSavePicker savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            // Dropdown of file types the user can save the file as
            savePicker.FileTypeChoices.Add("Plain Text", new List<string>() { ".txt" });
            // Default file name if the user does not type one in or select a file to replace
            savePicker.SuggestedFileName = "My Download Document";

            savePicker.PickSaveFileAndContinue();
        }

    }

}





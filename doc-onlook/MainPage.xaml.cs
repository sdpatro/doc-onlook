using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Diagnostics;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Search;
using System.Threading.Tasks;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace doc_onlook
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public class OnlookFile
        {
            string Data { get; set; }
            string Type { get; set; }
        }

        
        public MainPage()
        {
            this.InitializeComponent();
            FillSampleFiles();
            FillCarousel();
        }

        // Fill the local files list:
        public async void FillLocalList()
        {
            List<StorageFile> items = await GetLocalFiles();
            LocalListView.ItemsSource = items;
        }

        // Fill a couple of 'Get Started' HTML files
        public async void FillSampleFiles()
        {
                        
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            string AssetFile = @"Assets\TestPage1.html";
            StorageFolder InstallationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            StorageFile file = await InstallationFolder.GetFileAsync(AssetFile);
            try
            {
                StorageFile AppDataFile = await ApplicationData.Current.LocalFolder.GetFileAsync("TestPage1.html");
            }
            catch(FileNotFoundException e)
            {
                await file.CopyAsync(localFolder);
            }

            AssetFile = @"Assets\TestPage2.html";
            file = await InstallationFolder.GetFileAsync(AssetFile);
            try
            {
                StorageFile AppDataFile = await ApplicationData.Current.LocalFolder.GetFileAsync("TestPage2.html");
            }
            catch (FileNotFoundException e)
            {
                await file.CopyAsync(localFolder);
            }

            FillLocalList();
        }

        public async Task<List<StorageFile>> GetLocalFiles()
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFileQueryResult queryResult =  localFolder.CreateFileQuery();
            IReadOnlyList<StorageFile> fileList = await queryResult.GetFilesAsync();
            List<StorageFile> localList = new List<StorageFile>();
            foreach (StorageFile file in fileList)
            {
                localList.Add(file);
            }
            return localList;
        }

        public void FillCarousel()
        {
            WebView TestView = (WebView)this.FindName("testview_1");
            TestView.Navigate(new Uri("ms-appx-web:///assets/Hello.html"));
        }

        async private void SaveToDeviceBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileSavePicker savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("Plain Text", new List<string>() { ".txt" });
                savePicker.SuggestedFileName = "New Document";

                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    CachedFileManager.DeferUpdates(file);
                    await FileIO.WriteTextAsync(file, file.Name);
                    FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                    if (status == FileUpdateStatus.Complete)
                    {
                        MessageDialog dialog = new MessageDialog("File Saved.");
                        await dialog.ShowAsync();
                    }
                    else
                    {
                        MessageDialog dialog = new MessageDialog("We couldn't save the file.");
                        await dialog.ShowAsync();
                    }
                }
                else
                {
                    MessageDialog dialog = new MessageDialog("Operation cancelled.");
                    await dialog.ShowAsync();
                }
            }
            catch(Exception exc)
            {
                MessageDialog dialog = new MessageDialog("Something occured: "+exc.ToString());
                await dialog.ShowAsync();
            }
            
        }

        private async void FileListItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            StackPanel fileItem = (StackPanel)sender;
            string fileName = ((TextBlock)fileItem.Children[0]).Text;
            string fileType = ((TextBlock)fileItem.Children[1]).Text;
            StorageFile localFile = await GetLocalFile(fileName, fileType);
            DisplayDoc(localFile);
        }

        public async void DisplayDoc(StorageFile localFile)
        {
            var read = await FileIO.ReadTextAsync(localFile);
            var CurrentFileBuffer = read;
            ((WebView)this.FindName("testview_1")).NavigateToString(CurrentFileBuffer);
        }

        public async Task<StorageFile> GetLocalFile(string fileName, string fileType)
        {
            try
            {
                StorageFile localFile = await ApplicationData.Current.LocalFolder.GetFileAsync(fileName+fileType);
                return localFile;
            }
            catch(Exception exc){
                Debug.WriteLine("Exception: " + exc.ToString());
                return null;
            }
            
        }

    }
}

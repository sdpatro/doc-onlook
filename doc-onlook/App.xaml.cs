using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.SpeechRecognition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace doc_onlook
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            
        }

        public void CheckBackgroundTasks()
        {
            var taskRegistered = false;
            var exampleTaskName = "ExampleBackgroundTask";

            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == exampleTaskName)
                {
                    taskRegistered = true;
                    Debug.WriteLine("BackgroundTask registered...");
                    break;
                }
            }
        }

        public void RegisterBackgroundTask()
        {
            var builder = new BackgroundTaskBuilder();

            builder.Name = "ExampleBackgroundTask";
            builder.TaskEntryPoint = "RuntimeComponent1.ExampleBackgroundTask";
            builder.SetTrigger(new SystemTrigger(SystemTriggerType.InternetAvailable, false));
        }

        private async void AttachVCD()
        {
            var storageFile = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///DocOnlookCommands.xml"));
            await Windows.ApplicationModel.VoiceCommands.VoiceCommandDefinitionManager.InstallCommandDefinitionsFromStorageFileAsync(storageFile);
            Debug.WriteLine("VCD attached");
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            AttachVCD();

#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }
            // Ensure the current window is active
            Window.Current.Activate();

            RegisterBackgroundTask();
            CheckBackgroundTasks();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }

        override protected void OnActivated(IActivatedEventArgs args)
        {
            if (args.Kind == ActivationKind.VoiceCommand)
            {
                VoiceCommandActivatedEventArgs commandArgs = args as VoiceCommandActivatedEventArgs;
                SpeechRecognitionResult speechRecognitionResult = commandArgs.Result;

                string voiceCommandName = speechRecognitionResult.RulePath[0];
                string textSpoken = speechRecognitionResult.Text;
                //IReadOnlyList<string> recognizedVoiceCommandPhrases;

                switch (voiceCommandName)
                {
                    case "showTheLatestFile": Debug.WriteLine("Show latest file");
                        break;
                }
        }
    }
    }
}

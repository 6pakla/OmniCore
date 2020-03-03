﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Android;
using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using OmniCore.Client.Droid;
using Plugin.BluetoothLE;
using Permission = Android.Content.PM.Permission;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Android.Content;
using OmniCore.Client.Droid.Services;
using OmniCore.Model.Enumerations;
using OmniCore.Model.Exceptions;
using OmniCore.Model.Interfaces.Client;
using OmniCore.Model.Interfaces.Common;
using OmniCore.Model.Utilities.Extensions;
using OmniCore.Services;
using Application = Xamarin.Forms.Application;
using Debug = System.Diagnostics.Debug;
using Android.Gms.Common;
using Firebase.Messaging;
using Firebase.Iid;
using Android.Util;

namespace OmniCore.Client.Droid
{
    [Activity(Label = "OmniCore", Icon = "@mipmap/ic_launcher", Theme = "@style/MainTheme", MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation,
        LaunchMode = LaunchMode.SingleTask, Exported = true, AlwaysRetainTaskState = false,
        Name = "OmniCore.MainActivity")]

    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity, ICoreClientContext
    {
        private ICoreContainer<IClientResolvable> ClientContainer;

        private IServiceConnection ServiceConnection => (IServiceConnection) ClientContainer.Get<ICoreClientConnection>();
        private bool ConnectRequested = false;
        private bool DisconnectRequested = false;

#if DEBUG
        private IDisposable ScreenLockDisposable = null;
#endif

        protected override void OnCreate(Bundle savedInstanceState)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            AppDomain.CurrentDomain.UnhandledException += (sender, args) => OnUnhandledException(args.ExceptionObject);
            TaskScheduler.UnobservedTaskException += (sender, args) => OnUnhandledException(args.Exception);
            AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) => OnUnhandledException(args.Exception);

            Xamarin.Forms.Forms.SetFlags("CollectionView_Experimental",
                "IndicatorView_Experimental", "CarouselView_Experimental");
            
            base.OnCreate(savedInstanceState);

            Xamarin.Forms.Forms.Init(this, savedInstanceState);

            //TODO: move to service
            if (!CheckPermissions().Wait())
            {
                this.FinishAffinity();
            }

            Rg.Plugins.Popup.Popup.Init(this, savedInstanceState);

            ClientContainer = Initializer.AndroidClientContainer(this)
                .WithXamarinForms();

            LoadXamarinApplication();

        }

        public override void OnBackPressed()
        {
            if (Rg.Plugins.Popup.Popup.SendBackPressed(base.OnBackPressed))
            {
                // Do something if there are some pages in the `PopupStack`
            }
            else
            {
                // Do something if there are not any pages in the `PopupStack`
            }
        }

        private void OnUnhandledException(object exceptionObject)
        {
            if (exceptionObject != null && exceptionObject is Exception e)
            {
                Debug.WriteLine(e.AsDebugFriendly());
            }
            else
            {
                Debug.WriteLine($"****** Unknown exception object {exceptionObject}");
            }
        }

        private ISubject<bool> PermissionResultSubject;
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            PermissionResultSubject.OnNext(grantResults.All(r => r == Permission.Granted));
            PermissionResultSubject.OnCompleted();
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private IObservable<bool> CheckPermissions()
        {
            var permissions = new List<string>()
            {
                Manifest.Permission.AccessCoarseLocation,
                Manifest.Permission.BluetoothPrivileged,
                Manifest.Permission.ReadExternalStorage,
                Manifest.Permission.WriteExternalStorage,
            };

            foreach (var permission in permissions.ToArray())
            {
                if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessCoarseLocation) ==
                    (int) Permission.Granted)
                    permissions.Remove(permission);
            }

            if (permissions.Count > 0)
            {
                PermissionResultSubject = new Subject<bool>();
                ActivityCompat.RequestPermissions(this, permissions.ToArray(), 34);
                return PermissionResultSubject.AsObservable();
            }
            return Observable.Return(true);
        }

        protected override void OnResume()
        {
#if DEBUG
            ScreenLockDisposable = ClientContainer.Get<ICoreClient>().DisplayKeepAwake();
#endif
            ConnectToAndroidService();
            base.OnResume();
        }

        protected override void OnPause()
        {
#if DEBUG
            ScreenLockDisposable?.Dispose();
            ScreenLockDisposable = null;
#endif
            base.OnPause();
        }

        protected override void OnStop()
        {
            base.OnStop();
            DisconnectFromAndroidService();
        }

        private void LoadXamarinApplication()
        {
            LoadApplication(ClientContainer.Get<XamarinApp>());
        }
        
        private void ConnectToAndroidService()
        {
            if (ConnectRequested)
                return;
            
            var intent = new Intent(this, typeof(Services.AndroidService));
            if (!BindService(intent, ServiceConnection, Bind.AutoCreate))
                throw new OmniCoreUserInterfaceException(FailureType.ServiceConnectionFailed);
            ConnectRequested = true;
            DisconnectRequested = false;
        }

        private void DisconnectFromAndroidService()
        {
            if (DisconnectRequested)
                return;
            
            base.UnbindService(ServiceConnection);
            ConnectRequested = false;
            DisconnectRequested = true;
        }
    }
}
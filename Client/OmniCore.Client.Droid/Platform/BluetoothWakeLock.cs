﻿using System;
using Android.App;
using Android.Content;
using Android.OS;

namespace OmniCore.Client.Droid
{
    public class BluetoothWakeLock : IDisposable
    {
        private readonly string Tag = "OmniCoreLocationLock";
        private PowerManager.WakeLock WakeLockInternal;

        public BluetoothWakeLock()
        {
            var pm = (PowerManager) Application.Context.GetSystemService(Context.PowerService);
            Tag += Guid.NewGuid().ToString();
            WakeLockInternal = pm.NewWakeLock(WakeLockFlags.Partial | WakeLockFlags.LocationModeNoChange, Tag);
            WakeLockInternal.Acquire();
        }

        public void Dispose()
        {
            WakeLockInternal?.Release();
            WakeLockInternal = null;
        }
    }
}
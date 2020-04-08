﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using OmniCore.Model.Enumerations;
using OmniCore.Model.Exceptions;
using OmniCore.Model.Interfaces.Common;
using OmniCore.Model.Interfaces.Services;
using OmniCore.Model.Interfaces.Services.Internal;
using OmniCore.Model.Utilities.Extensions;
using Plugin.BluetoothLE;

namespace OmniCore.Client.Platform
{
    public class BlePeripheralAdapter : IBlePeripheralAdapter
    {
        private readonly ICoreApplicationFunctions ApplicationFunctions;

        private readonly ICoreContainer<IServerResolvable> Container;
        private readonly IErosRadioProvider[] ErosRadioProviders;
        private readonly List<Guid> ErosRadioServiceUuids;
        private readonly ICoreLoggingFunctions Logging;
        private readonly ICoreNotificationFunctions NotificationFunctions;

        private readonly AsyncLock PeripheralConnectionLockProvider;
        private readonly BlePeripheralScanner Scanner;
        private readonly AsyncLock AdapterManagementLock;
        private readonly ConcurrentDictionary<Guid, IDevice> DeviceCache;
        private readonly ConcurrentDictionary<Guid, BlePeripheral> PeripheralCache;

        public BlePeripheralAdapter(ICoreContainer<IServerResolvable> container,
            ICoreApplicationFunctions applicationFunctions,
            ICoreNotificationFunctions notificationFunctions,
            ICoreLoggingFunctions loggingFunctions,
            IErosRadioProvider[] erosRadioProviders)
        {
            Container = container;
            ApplicationFunctions = applicationFunctions;
            NotificationFunctions = notificationFunctions;
            Logging = loggingFunctions;
            ErosRadioProviders = erosRadioProviders;

            AdapterManagementLock = new AsyncLock();
            PeripheralCache = new ConcurrentDictionary<Guid, BlePeripheral>();
            DeviceCache = new ConcurrentDictionary<Guid, IDevice>();

            CrossBleAdapter.AndroidConfiguration.ShouldInvokeOnMainThread = false;
            CrossBleAdapter.AndroidConfiguration.UseInternalSyncQueue = true;
            CrossBleAdapter.AndroidConfiguration.UseNewScanner = true;
            CrossBleAdapter.AndroidConfiguration.RefreshServices = false;

            ErosRadioServiceUuids = erosRadioProviders
                .Select(rp => rp.ServiceUuid).ToList();

            PeripheralConnectionLockProvider = new AsyncLock();
            Scanner = new BlePeripheralScanner(ErosRadioServiceUuids, loggingFunctions, applicationFunctions);
            WhenScanStarted = Scanner.WhenScanStateChanged.Where(s => s).Select(s => this);
            WhenScanFinished = Scanner.WhenScanStateChanged.Where(s => !s).Select(s => this);

            InternalObservable = CreateObservable();
        }

        public IObservable<IBlePeripheralAdapter> WhenScanStarted { get; }
        public IObservable<IBlePeripheralAdapter> WhenScanFinished { get; }

        public IObservable<IBlePeripheralAdapter> WhenAdapterDisabled()
        {
            return Observable.Create<IBlePeripheralAdapter>(observer =>
            {
                if (CrossBleAdapter.Current.Status == AdapterStatus.PoweredOff)
                {
                    Logging.Debug("BLE: Adapter disabled");
                    observer.OnNext(this);
                }

                return CrossBleAdapter.Current.WhenStatusChanged()
                    .Where(s => s == AdapterStatus.PoweredOff)
                    .Subscribe(_ =>
                    {
                        Logging.Debug("BLE: Adapter disabled");
                        observer.OnNext(this);
                    });
            });
        }

        public IObservable<IBlePeripheralAdapter> WhenAdapterEnabled()
        {
            return Observable.Create<IBlePeripheralAdapter>(observer =>
            {
                if (CrossBleAdapter.Current.Status == AdapterStatus.PoweredOn)
                {
                    Logging.Debug("BLE: Adapter enabled");
                    observer.OnNext(this);
                }

                return CrossBleAdapter.Current.WhenStatusChanged()
                    .Where(s => s == AdapterStatus.PoweredOn)
                    .Subscribe(_ =>
                    {
                        Logging.Debug("BLE: Adapter enabled");
                        observer.OnNext(this);
                    });
            });
        }

        public async Task TryEnsureAdapterEnabled(CancellationToken cancellationToken)
        {
            switch (CrossBleAdapter.Current.Status)
            {
                case AdapterStatus.PoweredOn:
                case AdapterStatus.Unsupported:
                case AdapterStatus.Unauthorized:
                    return;
                case AdapterStatus.PoweredOff:
                    if (CrossBleAdapter.Current.CanControlAdapterState())
                        if (await TryEnableAdapter(cancellationToken))
                            return;
                    throw new OmniCoreAdapterException(FailureType.AdapterNotEnabled);
            }
        }

        public async Task<bool> TryEnableAdapter(CancellationToken cancellationToken)
        {
            using var adapterManagementLock = await AdapterManagementLock.LockAsync(cancellationToken);
            if (CrossBleAdapter.Current.Status == AdapterStatus.PoweredOn) return true;

            Logging.Debug("BLE: Trying to enable adapter");
            CrossBleAdapter.Current.SetAdapterState(true);
            Logging.Debug("BLE: Waiting for adapter to get enabled");

            await CrossBleAdapter.Current.WhenStatusChanged()
                .Where(s => s == AdapterStatus.PoweredOn)
                .FirstAsync()
                .ToTask(cancellationToken);

            Logging.Debug("BLE: Adapter enabled successfully");
            return CrossBleAdapter.Current.Status == AdapterStatus.PoweredOn;
        }

        public async Task<IDisposable> PeripheralConnectionLock(CancellationToken cancellationToken)
        {
            IDisposable bluetoothLock = null;
            IDisposable lockDisposable = null;
            try
            {
                lockDisposable = await PeripheralConnectionLockProvider.LockAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await TryEnsureAdapterEnabled(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                Scanner.Pause();
                bluetoothLock = ApplicationFunctions.BluetoothKeepAwake();

                await Task.Delay(500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                bluetoothLock?.Dispose();
                Scanner.Resume();
                lockDisposable?.Dispose();
            }

            return Disposable.Create(async () =>
            {
                bluetoothLock?.Dispose();
                await Task.Delay(500);
                Scanner.Resume();
                lockDisposable?.Dispose();
            });
        }

        private IObservable<IBlePeripheral> InternalObservable;
        public IObservable<IBlePeripheral> FindErosRadioPeripherals() => InternalObservable;
        private IObservable<IBlePeripheral> CreateObservable()
        {
            return Observable.Create<IBlePeripheral>(async observer =>
                {
                    var cts = new CancellationTokenSource();
                    var cancellationToken = cts.Token;

                    var observedPeripheralUuids = new HashSet<Guid>();

                    IDisposable scanSubscription = null;

                    try
                    {
                        Logging.Debug("BLE: Request connected devices");
                        using (var ppc = await PeripheralConnectionLock(cts.Token))
                        {
                            var connectedDevices = await CrossBleAdapter.Current
                                .GetConnectedDevices().ToTask(cancellationToken);
                            Logging.Debug("BLE: Received connected devices");

                            foreach (var connectedDevice in connectedDevices)
                            {
                                var service = await connectedDevice.DiscoverServices()
                                    .FirstOrDefaultAsync(s => ErosRadioServiceUuids.Contains(s.Uuid));

                                if (service != null)
                                {
                                    DeviceCache[connectedDevice.Uuid] = connectedDevice;
                                    var peripheral = GetPeripheralInternal(connectedDevice.Uuid, service.Uuid);
                                    peripheral.UpdateSubscriptions(connectedDevice);

                                    Logging.Debug(
                                        $"BLE: {peripheral.PeripheralUuid.AsMacAddress()} Notifying connected peripheral as found");
                                    observedPeripheralUuids.Add(peripheral.PeripheralUuid);
                                    observer.OnNext(peripheral);
                                }

                                cancellationToken.ThrowIfCancellationRequested();
                            }

                            var searchStart = DateTimeOffset.UtcNow;
                            var connectedPeripheralUuids = connectedDevices.Select(c => c.Uuid);
                            foreach (var peripheralUuid in DeviceCache.Keys.ToList())
                            {
                                if (!connectedPeripheralUuids.Any(cuuid => cuuid == peripheralUuid))
                                    DeviceCache[peripheralUuid] = null;

                                var peripheral = GetPeripheralInternal(peripheralUuid, ErosRadioServiceUuids[0]);
                                peripheral.DiscoveryState = (PeripheralDiscoveryState.Searching, searchStart);
                            }

                            Logging.Debug("BLE: Connecting to scan observable");
                            scanSubscription = Scanner.Scan()
                                .Subscribe(scanResult =>
                                {
                                    DeviceCache[scanResult.Device.Uuid] = scanResult.Device;
                                    var peripheral = GetPeripheralInternal(scanResult.Device.Uuid,
                                        scanResult.AdvertisementData.ServiceUuids[0]);

                                    peripheral.UpdateSubscriptions(scanResult.Device);

                                    if (string.IsNullOrEmpty(peripheral.Name))
                                        peripheral.Name = scanResult.AdvertisementData.LocalName;

                                    if (!string.IsNullOrEmpty(scanResult.Device.Name))
                                        peripheral.Name = scanResult.Device.Name;

                                    peripheral.Rssi = (scanResult.Rssi, DateTimeOffset.UtcNow);
                                    peripheral.DiscoveryState = (PeripheralDiscoveryState.Discovered,
                                        DateTimeOffset.UtcNow);

                                    if (!observedPeripheralUuids.Contains(peripheral.PeripheralUuid))
                                    {
                                        Logging.Debug(
                                            $"BLE: {peripheral.PeripheralUuid.AsMacAddress()} Reporting found peripheral");
                                        observedPeripheralUuids.Add(peripheral.PeripheralUuid);
                                        observer.OnNext(peripheral);
                                    }
                                });
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.Debug($"BLE: Error during scan: \n {e.AsDebugFriendly()}");
                        cts.Cancel();
                        cts.Dispose();
                        scanSubscription?.Dispose();
                        var dateFinished = DateTimeOffset.UtcNow;
                        foreach (var peripheral in PeripheralCache.Values.ToList())
                            if (peripheral.DiscoveryState.State == PeripheralDiscoveryState.Searching)
                                peripheral.DiscoveryState = (PeripheralDiscoveryState.NotFound, dateFinished);
                        observer.OnError(e);
                    }

                    return Disposable.Create(() =>
                    {
                        Logging.Debug("BLE: Disconnecting from scan observable");
                        cts.Cancel();
                        cts.Dispose();
                        scanSubscription?.Dispose();
                        var dateFinished = DateTimeOffset.UtcNow;
                        foreach (var peripheral in PeripheralCache.Values.ToList())
                            if (peripheral.DiscoveryState.State == PeripheralDiscoveryState.Searching)
                                peripheral.DiscoveryState = (PeripheralDiscoveryState.NotFound, dateFinished);
                    });
                }
            );
        }

        public IBlePeripheral GetPeripheral(Guid peripheralUuid, Guid primaryServiceUuid)
        {
            return GetPeripheralInternal(peripheralUuid, primaryServiceUuid);
        }
        
        private BlePeripheral GetPeripheralInternal(Guid peripheralUuid, Guid primaryServiceUuid)
        {
            return PeripheralCache.GetOrAdd(peripheralUuid, _ =>
            {
                var p = (BlePeripheral) Container.Get<IBlePeripheral>();
                p.PeripheralUuid = peripheralUuid;
                p.PrimaryServiceUuid = primaryServiceUuid;
                return p;
            });
        }

        public IDevice GetNativeDeviceFromCache(Guid peripheralUuid)
        {
            return DeviceCache[peripheralUuid];
        }

        public async Task<IDevice> GetNativeDevice(Guid peripheralUuid, CancellationToken cancellationToken)
        {
            if (!DeviceCache.TryGetValue(peripheralUuid, out var nativeDevice) || nativeDevice == null)
                await FindErosRadioPeripherals()
                    .FirstAsync(p => p.PeripheralUuid == peripheralUuid).ToTask(cancellationToken);
            return DeviceCache[peripheralUuid];
        }
    }
}
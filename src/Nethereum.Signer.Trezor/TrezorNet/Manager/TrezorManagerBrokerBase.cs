// NOTE: Adapted from the Trezor.Net project (https://github.com/MelbourneDeveloper/Trezor.Net).
// This copy lives in Nethereum temporarily until upstream is upgraded.

﻿using Device.Net;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Trezor.Net.Manager
{
    //TODO: Add logging (Inject the logger factory)

    public abstract class TrezorManagerBrokerBase<T, TMessageType> where T : TrezorManagerBase<TMessageType>, IDisposable
    {
        protected ILoggerFactory LoggerFactory { get; }

        #region Fields
        private bool _disposed;
        private readonly DeviceListener _DeviceListener;
        private readonly SemaphoreSlim _Lock = new SemaphoreSlim(1, 1);
        private readonly TaskCompletionSource<T> _FirstTrezorTaskCompletionSource = new TaskCompletionSource<T>();
        #endregion

        #region Events
        /// <summary>
        /// Occurs after the TrezorManagerBroker notices that a device hasbeen connected, and initialized
        /// </summary>
        public event EventHandler<TrezorManagerConnectionEventArgs<TMessageType>> TrezorInitialized;

        /// <summary>
        /// Occurs after the TrezorManagerBroker notices that the device has been disconnected, but before the TrezorManager is disposed
        /// </summary>
        public event EventHandler<TrezorManagerConnectionEventArgs<TMessageType>> TrezorDisconnected;
        #endregion

        #region Public Properties
        public ReadOnlyCollection<T> TrezorManagers { get; private set; } = new ReadOnlyCollection<T>(new List<T>());
        public EnterPinArgs EnterPinArgs { get; }
        public EnterPinArgs EnterPassphraseArgs { get; }
        public ICoinUtility CoinUtility { get; }
        public int? PollInterval { get; }
        #endregion

        #region Constructor
        protected TrezorManagerBrokerBase(
            EnterPinArgs enterPinArgs,
            EnterPinArgs enterPassphraseArgs,
            int? pollInterval,
            IDeviceFactory deviceFactory,
            ICoinUtility coinUtility = null,
            ILoggerFactory loggerFactory = null)
        {
            EnterPinArgs = enterPinArgs;
            EnterPassphraseArgs = enterPassphraseArgs;
            CoinUtility = coinUtility ?? new DefaultCoinUtility();
            PollInterval = pollInterval;
            LoggerFactory = loggerFactory;


            _DeviceListener = new DeviceListener(deviceFactory, PollInterval, loggerFactory);
            _DeviceListener.DeviceDisconnected += DevicePoller_DeviceDisconnected;
            _DeviceListener.DeviceInitialized += DevicePoller_DeviceInitialized;
        }
        #endregion

        #region Protected Abstract Methods
        protected abstract T CreateTrezorManager(IDevice device);
        #endregion

        #region Event Handlers
        private async void DevicePoller_DeviceInitialized(object sender, DeviceEventArgs e)
        {
            try
            {
                await _Lock.WaitAsync().ConfigureAwait(false);

                var trezorManager = TrezorManagers.FirstOrDefault(t => ReferenceEquals(t.Device, e.Device));

                if (trezorManager != null) return;

                trezorManager = CreateTrezorManager(e.Device);

                var tempList = new List<T>(TrezorManagers)
                {
                    trezorManager
                };

                TrezorManagers = new ReadOnlyCollection<T>(tempList);

                await trezorManager.InitializeAsync().ConfigureAwait(false);

                if (_FirstTrezorTaskCompletionSource.Task.Status == TaskStatus.WaitingForActivation) _FirstTrezorTaskCompletionSource.SetResult(trezorManager);

                TrezorInitialized?.Invoke(this, new TrezorManagerConnectionEventArgs<TMessageType>(trezorManager));
            }
            finally
            {
                _ = _Lock.Release();
            }
        }

        private async void DevicePoller_DeviceDisconnected(object sender, DeviceEventArgs e)
        {
            try
            {
                await _Lock.WaitAsync().ConfigureAwait(false);

                var trezorManager = TrezorManagers.FirstOrDefault(t => ReferenceEquals(t.Device, e.Device));

                if (trezorManager == null) return;

                TrezorDisconnected?.Invoke(this, new TrezorManagerConnectionEventArgs<TMessageType>(trezorManager));

                trezorManager.Dispose();

                var tempList = new List<T>(TrezorManagers);

                _ = tempList.Remove(trezorManager);

                TrezorManagers = new ReadOnlyCollection<T>(tempList);
            }
            finally
            {
                _ = _Lock.Release();
            }
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Placeholder. This currently does nothing but you should call this to initialize listening
        /// </summary>
        public void Start()
        {
            if (_DeviceListener != null) return;

            _DeviceListener.Start();

            //TODO: Call Start on the DeviceListener when it is implemented...
        }

        public void Stop() => _DeviceListener?.Stop();

        /// <summary>
        /// Check to see if there are any devices connected
        /// </summary>
        public async Task CheckForDevicesAsync()
        {
            try
            {
                await _DeviceListener.CheckForDevicesAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Starts the device listener and waits for the first connected Trezor to be initialized
        /// </summary>
        /// <returns></returns>
        public async Task<T> WaitForFirstTrezorAsync()
        {
            if (_DeviceListener == null) Start();
            await _DeviceListener.CheckForDevicesAsync().ConfigureAwait(false);
            return await _FirstTrezorTaskCompletionSource.Task.ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _Lock.Dispose();
            _DeviceListener.Stop();
            _DeviceListener.Dispose();

            foreach (var trezorManager in TrezorManagers)
            {
                trezorManager.Dispose();
            }

            GC.SuppressFinalize(this);
        }
        #endregion
    }
}




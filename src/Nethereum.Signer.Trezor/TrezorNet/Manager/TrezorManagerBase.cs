// NOTE: Adapted from the Trezor.Net project (https://github.com/MelbourneDeveloper/Trezor.Net).
// This copy lives in Nethereum temporarily until upstream is upgraded.

﻿using Device.Net;
using Hardwarewallets.Net;
using Hardwarewallets.Net.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProtoBuf;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using hw.trezor.messages.management;

namespace Trezor.Net
{
    /// <summary>
    /// An interface for dealing with the Trezor that works across all platforms
    /// </summary>
    public abstract class TrezorManagerBase<TMessageType> : IDisposable, IAddressDeriver
    {

        #region Private Fields

        private const int FirstChunkStartIndex = 9;

        private readonly EnterPinArgs _EnterPassphraseCallback;
        private readonly EnterPinArgs _EnterPinCallback;
        private readonly SemaphoreSlim _Lock = new SemaphoreSlim(1, 1);
        private readonly string LogSection = "TrezorManagerBase";
        private int _InvalidChunksCounter;
        private object _LastWrittenMessage;
        private bool disposed;

        #endregion Private Fields

        #region Protected Constructors

        protected TrezorManagerBase(
            EnterPinArgs enterPinCallback,
            EnterPinArgs enterPassphraseCallback,
            IDevice device,
            ILogger<TrezorManagerBase<TMessageType>> logger = null,
            ICoinUtility coinUtility = null)
        {
            CoinUtility = coinUtility ?? DefaultCoinUtility.Instance;
            _EnterPinCallback = enterPinCallback;
            _EnterPassphraseCallback = enterPassphraseCallback;
            Device = device ?? throw new ArgumentNullException(nameof(device));
            Logger = (ILogger)logger ?? NullLogger.Instance;
        }

        #endregion Protected Constructors

        #region Public Properties

        public ICoinUtility CoinUtility { get; set; }
        public IDevice Device { get; private set; }
        public abstract bool IsInitialized { get; }

        #endregion Public Properties

        #region Protected Properties

        protected abstract string ContractNamespace { get; }
        protected abstract bool? IsOldFirmware { get; }
        protected ILogger Logger { get; }
        protected abstract Type MessageTypeType { get; }

        #endregion Protected Properties

        #region Public Methods

        public virtual void Dispose()
        {
            if (disposed) return;
            disposed = true;

            Device?.Dispose();

            GC.SuppressFinalize(this);

            _Lock.Dispose();
        }

        public abstract Task<string> GetAddressAsync(IAddressPath addressPath, bool isPublicKey, bool display);

        /// <summary>
        /// Initialize the Trezor. Should only be called once.
        /// </summary>
        public abstract Task InitializeAsync();

        /// <summary>
        /// Check to see if the Trezor is connected to the device
        /// </summary>
        public bool IsDeviceInitialized() => Device.IsInitialized;

        /// <summary>
        /// Send a message to the Trezor and receive the result
        /// </summary>
        /// <typeparam name="TReadMessage">The message type</typeparam>
        /// <typeparam name="TWriteMessage">The result type</typeparam>
        /// <param name="message">The message</param>
        /// <returns>The result</returns>
        public async Task<TReadMessage> SendMessageAsync<TReadMessage, TWriteMessage>(TWriteMessage message)
        {
            ValidateInitialization(message);

            LogDebug($"Preparing to send {typeof(TWriteMessage).Name} expecting {typeof(TReadMessage).Name}");

            await _Lock.WaitAsync().ConfigureAwait(false);
            LogDebug($"Lock acquired for {typeof(TWriteMessage).Name}");

            var retriedAfterFeatures = false;

            try
            {
                var response = await SendMessageAsync(message).ConfigureAwait(false);
                LogDebug($"Initial response type: {response.GetType().Name}");

                for (var i = 0; i < 10; i++)
                {
                    if (response is Features && typeof(TReadMessage) != typeof(Features))
                    {
                        LogDebug("Received Features payload while waiting for expected type; reinitializing.");
                        if (retriedAfterFeatures)
                        {
                            break;
                        }

                        retriedAfterFeatures = true;
                        LogDebug("Reinitializing device after Features response.");
                        await InitializeAsync().ConfigureAwait(false);
                        response = await SendMessageAsync(message).ConfigureAwait(false);
                        LogDebug($"Response after reinitialize: {response.GetType().Name}");
                        continue;
                    }

                    if (IsPinMatrixRequest(response))
                    {
                        LogDebug("PIN matrix requested by device.");
                        var pin = await _EnterPinCallback.Invoke().ConfigureAwait(false);
                        LogDebug("PIN obtained, acknowledging to device.");
                        response = await PinMatrixAckAsync(pin).ConfigureAwait(false);
                        LogDebug($"Response after PIN ack: {response.GetType().Name}");
                        if (response is TReadMessage readMessage)
                        {
                            LogDebug($"Returning expected response {typeof(TReadMessage).Name} after PIN entry.");
                            return readMessage;
                        }
                    }

                    if (IsPassphraseRequest(response))
                    {
                        LogDebug("Passphrase requested by device.");
                        var passPhrase = await _EnterPassphraseCallback.Invoke().ConfigureAwait(false);
                        LogDebug("Passphrase obtained, acknowledging to device.");
                        response = await PassphraseAckAsync(passPhrase).ConfigureAwait(false);
                        LogDebug($"Response after passphrase ack: {response.GetType().Name}");
                        if (response is TReadMessage readMessage)
                        {
                            LogDebug($"Returning expected response {typeof(TReadMessage).Name} after passphrase entry.");
                            return readMessage;
                        }
                    }

                    else if (IsButtonRequest(response))
                    {
                        LogDebug("Button confirmation requested by device.");
                        response = await ButtonAckAsync().ConfigureAwait(false);
                        LogDebug($"Response after button ack: {response.GetType().Name}");

                        if (response is TReadMessage readMessage)
                        {
                            LogDebug($"Returning expected response {typeof(TReadMessage).Name} after button confirmation.");
                            return readMessage;
                        }
                    }

                    else if (response is TReadMessage readMessage)
                    {
                        LogDebug($"Received expected response {typeof(TReadMessage).Name}.");
                        return readMessage;
                    }
                }

                LogWarning($"Unexpected response {response.GetType().Name} while waiting for {typeof(TReadMessage).Name}.");
                throw new ManagerException($"Returned response ({response.GetType().Name})  was of the wrong specified message type ({typeof(TReadMessage).Name}). The user did not accept the message, or pin was entered incorrectly too many times (Note: this library does not have an incorrect pin safety mechanism.)");
            }
            finally
            {
                LogDebug($"Releasing lock for {typeof(TWriteMessage).Name}");
                _ = _Lock.Release();
            }
        }

        #endregion Public Methods

        #region Protected Methods

        protected abstract Task<object> ButtonAckAsync();

        protected abstract void CheckForFailure(object returnMessage);

        protected abstract Type GetContractType(TMessageType messageType, string typeName);

        protected abstract object GetEnumValue(string messageTypeString);
        protected abstract bool IsButtonRequest(object response);
        protected abstract bool IsInitialize(object response);

        protected abstract bool IsPassphraseRequest(object response);

        protected abstract bool IsPinMatrixRequest(object response);
        protected abstract Task<object> PassphraseAckAsync(string passPhrase);

        protected abstract Task<object> PinMatrixAckAsync(string pin);

        /// <summary>
        /// Warning: This is not thread safe. It should only be used inside the generic version of this method or to call pin related stuff
        /// </summary>
        protected async Task<object> SendMessageAsync(object message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            LogDebug($"Sending raw message {message.GetType().Name}");

            await WriteAsync(message).ConfigureAwait(false);

            var retVal = await ReadAsync().ConfigureAwait(false);

            CheckForFailure(retVal);

            LogDebug($"Raw send received response {retVal.GetType().Name}");
            return retVal;
        }

        protected void ValidateInitialization(object message)
        {
            if (!IsInitialized && !IsInitialize(message))
            {
                throw new ManagerException($"The device has not been successfully initialised. Please call {nameof(InitializeAsync)}.");
            }
        }

        #endregion Protected Methods

        #region Private Methods

        private static byte[] Append(byte[] x, byte[] y)
        {
            var z = new byte[x.Length + y.Length];
            x.CopyTo(z, 0);
            y.CopyTo(z, x.Length);
            return z;
        }

        private static object Deserialize(Type type, byte[] data)
        {
            using (var writer = new MemoryStream(data))
            {
                return Serializer.NonGeneric.Deserialize(type, writer);
            }
        }

        /// <summary>
        /// Horribly inefficient array thing
        /// </summary>
        /// <returns></returns>
        private static byte[] GetRange(byte[] bytes, int startIndex, int length) => bytes.ToList().GetRange(startIndex, length).ToArray();

        private static byte[] Serialize(object msg)
        {
            using (var writer = new MemoryStream())
            {
                Serializer.NonGeneric.Serialize(writer, msg);
                return writer.ToArray();
            }
        }

        private object Deserialize(TMessageType messageType, byte[] data)
        {
            try
            {
                var messageTypeNamespace = ContractNamespace;

                if (IsOldFirmware.HasValue && IsOldFirmware.Value)
                {
                    //Look for the type in the backwards compatibility namespace
                    messageTypeNamespace = $"{messageTypeNamespace}.BackwardsCompatible";
                }

                var typeName = $"{messageTypeNamespace}.{messageType.ToString().Replace("MessageType", string.Empty)}";

                if (IsOldFirmware.HasValue && IsOldFirmware.Value)
                {
                    var type = Type.GetType(typeName);

                    //Fall back on the non-backwards compatible namespace if necessary
                    if (type == null) typeName = $"{ContractNamespace}.{messageType.ToString().Replace("MessageType", string.Empty)}";
                }

                var contractType = GetContractType(messageType, typeName);

                return Deserialize(contractType, data);
            }
            catch (Exception ex)
            {
                throw new ManagerException("InvalidProtocolBufferException", ex);
            }
        }

        private async Task<object> ReadAsync()
        {
            LogDebug("Waiting for device response...");
            //Read a chunk
            var initialRead = await Device.ReadAsync().ConfigureAwait(false);
            var readBuffer = initialRead.Data;
            LogDebug($"Initial chunk length: {readBuffer.Length}");

            //Check to see that this is a valid first chunk 
            var firstByteNot63 = readBuffer[0] != (byte)'?';
            var secondByteNot35 = readBuffer[1] != 35;
            var thirdByteNot35 = readBuffer[2] != 35;
            if (firstByteNot63 || secondByteNot35 || thirdByteNot35)
            {
                var message = $"An error occurred while attempting to read the message from the device. The last written message was a {_LastWrittenMessage?.GetType().Name}. In the first chunk of data ";

                if (firstByteNot63)
                {
                    message += "the first byte was not 63";
                }

                if (secondByteNot35)
                {
                    message += "the second byte was not 35";
                }

                if (thirdByteNot35)
                {
                    message += "the third byte was not 35";
                }

                throw new ReadException(message, readBuffer, _LastWrittenMessage);
            }

            //From Trezor-Android TrezorManager.messageRead
            var messageTypeInt = ((readBuffer[3] & 0xFF) << 8) + (readBuffer[4] & 0xFF);

            if (!Enum.IsDefined(MessageTypeType, messageTypeInt))
            {
                throw new ManagerException($"The number {messageTypeInt} is not a valid MessageType");
            }

            //Get the message type
            var messageTypeValueName = Enum.GetName(MessageTypeType, messageTypeInt);

            var messageType = (TMessageType)Enum.Parse(MessageTypeType, messageTypeValueName);

            //msgLength:= int(binary.BigEndian.Uint32(buf[i + 4 : i + 8]))
            //TODO: Is this correct?
            var remainingDataLength = ((readBuffer[5] & 0xFF) << 24)
                                      + ((readBuffer[6] & 0xFF) << 16)
                                      + ((readBuffer[7] & 0xFF) << 8)
                                      + (readBuffer[8] & 0xFF);

            var length = Math.Min(readBuffer.Length - FirstChunkStartIndex, remainingDataLength);

            //This is the first chunk so read from 9-64
            var allData = GetRange(readBuffer, FirstChunkStartIndex, length);

            remainingDataLength -= allData.Length;

            _InvalidChunksCounter = 0;

            while (remainingDataLength > 0)
            {
                LogDebug($"Remaining data length {remainingDataLength}; reading next chunk...");
                //Read a chunk
                var nextRead = await Device.ReadAsync().ConfigureAwait(false);
                readBuffer = nextRead.Data;
                LogDebug($"Chunk length: {readBuffer.Length}");

                //check that there was some data returned
                if (readBuffer.Length <= 0)
                {
                    LogWarning("Device returned empty chunk, continuing...");
                    continue;
                }

                //Check what's smaller, the buffer or the remaining data length
                length = Math.Min(readBuffer.Length, remainingDataLength);

                if (readBuffer[0] != (byte)'?')
                {
                    if (_InvalidChunksCounter++ > 5)
                    {
                        throw new ManagerException("messageRead: too many invalid chunks (2)");
                    }
                }

                allData = Append(allData, GetRange(readBuffer, 1, length - 1));

                //Decrement the length of the data to be read
                remainingDataLength -= length - 1;

                //Super hack! Fix this!
                if (remainingDataLength != 1)
                {
                    continue;
                }

                allData = Append(allData, GetRange(readBuffer, length, 1));
                remainingDataLength = 0;
            }

            var msg = Deserialize(messageType, allData);

            LogInformation($"Read: {msg}");

            return msg;
        }

        private async Task WriteAsync(object msg)
        {
            LogInformation($"Write: {msg}");

            var byteArray = Serialize(msg);

            //This confirms that the message data is correct
            // var testMessage = Deserialize(msg.GetType(), byteArray);

            var msgSize = byteArray.Length;
            var msgName = msg.GetType().Name;

            var messageTypeString = "MessageType" + msgName;

            var messageType = GetEnumValue(messageTypeString);

            var msgId = (int)messageType;
            var data = new ByteBuffer(msgSize + 1024); // 32768);
            data.Put((byte)'#');
            data.Put((byte)'#');
            data.Put((byte)((msgId >> 8) & 0xFF));
            data.Put((byte)(msgId & 0xFF));
            data.Put((byte)((msgSize >> 24) & 0xFF));
            data.Put((byte)((msgSize >> 16) & 0xFF));
            data.Put((byte)((msgSize >> 8) & 0xFF));
            data.Put((byte)(msgSize & 0xFF));
            data.Put(byteArray);

            while (data.Position % 63 > 0)
            {
                data.Put(0);
            }

            var chunks = data.Position / 63;

            var wholeArray = data.ToArray();

            for (var i = 0; i < chunks; i++)
            {
                var range = new byte[64];
                range[0] = (byte)'?';

                for (var x = 0; x < 63; x++)
                {
                    range[x + 1] = wholeArray[(i * 63) + x];
                }

                _ = await Device.WriteAsync(range).ConfigureAwait(false);
            }

            _LastWrittenMessage = msg;
        }

        private void LogDebug(string message)
        {
            Logger?.LogDebug("[{Section}] {Message}", LogSection, message);
        }

        private void LogInformation(string message)
        {
            Logger?.LogInformation("[{Section}] {Message}", LogSection, message);
        }

        private void LogWarning(string message)
        {
            Logger?.LogWarning("[{Section}] {Message}", LogSection, message);
        }

        #endregion Private Methods
    }
}

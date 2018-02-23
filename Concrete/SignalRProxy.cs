using Microsoft.AspNet.SignalR.Client;
using System.Threading.Tasks;
using System.Configuration;
using System;
using System.Timers;
using System.Collections.Generic;
using System.Net;
using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace API.Concrete
{
    public class QueueMessage
    {
        public string MessageType { get; set; }
        public string Payload { get; set; }
    }

    public class ObservationDeckSignalRProxy : IObservationDeckSignalRProxy, IDisposable
    {
        private readonly static string connectionUrl = ConfigurationManager.AppSettings["SignalRURL"];
        private static IHubProxy _hub;
        private static HubConnection _connection;
        private static bool _tryingToReconnect = false;
        private static bool _isDisposing = false;
        private static Timer _timer;
        private static readonly ConcurrentQueue<QueueMessage> _messageQueue;

        public bool IsConnected => _connection != null && _connection.State == ConnectionState.Connected;

        static ObservationDeckSignalRProxy()
        {
            _messageQueue = new ConcurrentQueue<QueueMessage>();
            InitializeConnection();
        }
       
        public async Task SendDeskEventAddedMessage(DeskClientEvent model)
        {
            await SendMessage("SendDeskEventAddedMessage", model);            
        }

        public async Task SendDeskEventUpdatedMessage(IEnumerable<DeskClientEvent> model)
        {
            await SendMessage("SendDeskEventUpdatedMessage", model);            
        }

        private async Task SendMessage<T>(string messageType, T payload)
        {
            if (IsConnected)
            {
                await _hub.Invoke(messageType, payload);
            }
            else
            {
                _messageQueue.Enqueue(new QueueMessage { MessageType = messageType, Payload = JsonConvert.SerializeObject(payload) });
            }
        }

        private static void InitializeConnection()
        {
            if (_connection != null)
            {
                _connection.Error -= ConnectionError;
                _connection.Reconnecting -= ConnectionReconnecting;
                _connection.Reconnected -= ConnectionReconnected;
                _connection.Closed -= ConnectionClosed;
                _connection.StateChanged -= ConnectionStateChanged;
            }
            else
            {
                _connection = new HubConnection(connectionUrl);
                _connection.Credentials = CredentialCache.DefaultCredentials;
                _connection.TraceLevel = TraceLevels.All;
                _connection.TraceWriter = Console.Out;

            }

            if (_hub == null)
                _hub = _connection.CreateHubProxy("ObservationDeckHub");

            if (_connection.State == ConnectionState.Connected | _connection.State == ConnectionState.Connecting |
                _connection.State == ConnectionState.Reconnecting)
                _connection.Stop();

            try
            {
                LogFactory.Logger(typeof(ObservationDeckSignalRProxy)).Info("SignalR Connection Starting.");
                _connection.Start();
            }
            catch (Exception ex)
            {
                LogFactory.Logger(typeof(ObservationDeckSignalRProxy)).WithException(ex).Error("Error Starting SignalR Connection");
                Reconnect();
            }            

            _connection.Error += ConnectionError;
            _connection.Reconnecting += ConnectionReconnecting;
            _connection.Reconnected += ConnectionReconnected;
            _connection.Closed += ConnectionClosed;
            _connection.StateChanged += ConnectionStateChanged;
        }

        private static void ConnectionStateChanged(StateChange state)
        {
            LogFactory.Logger(typeof(ObservationDeckSignalRProxy)).Debug("Signal R Connection state changed. Connection State {0}", _connection.State.ToString());
            if (state.NewState == ConnectionState.Connected)
            {
                if (!_messageQueue.IsEmpty)
                {
                    if (_messageQueue.TryDequeue(out var message))
                    {
                        switch (message.MessageType)
                        {
                            case "SendDeskEventUpdatedMessage":
                                _hub.Invoke(message.MessageType, JsonConvert.DeserializeObject<IEnumerable<DeskClientEvent>>(message.Payload));
                                break;
                            case "SendDeskEventAddedMessage":
                                _hub.Invoke(message.MessageType, JsonConvert.DeserializeObject<DeskClientEvent>(message.Payload));
                                break;
                        }
                    }

                }
            }
        }

        private static void ConnectionClosed()
        {
            Reconnect();
        }

        private static void Reconnect()
        {
            if (!_isDisposing && _tryingToReconnect)
            {
                _timer = new Timer(5000);

                _timer.AutoReset = false;
                _timer.Enabled = true;
                _timer.Elapsed += Timer_Elapsed;

            }
        }

        private static void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            _timer = null;
            InitializeConnection();
        }

        private static void ConnectionReconnected()
        {
            LogFactory.Logger(typeof(ObservationDeckSignalRProxy)).Debug("Signal R Reconnected.");
            _tryingToReconnect = false;
        }

        private static void ConnectionReconnecting()
        {
            LogFactory.Logger(typeof(ObservationDeckSignalRProxy)).Debug("Signal R Reconnecting.");
            _tryingToReconnect = true;           
        }

        private static void ConnectionError(Exception obj)
        {       
            _tryingToReconnect = false;
            LogFactory.Logger(typeof(ObservationDeckSignalRProxy)).WithException(obj).Error("Signal R connection error.");

            InitializeConnection();
        }

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_connection != null)
                {
                    _isDisposing = true;
                    _connection.Stop();
                    _connection.Dispose();
                }
            }

        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        ~ObservationDeckSignalRProxy()
        {
            Dispose(false);
        }
        #endregion
    }
}
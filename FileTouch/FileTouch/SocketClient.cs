using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Net;
using Windows.Networking.Sockets;
using Windows.Storage;
using System.IO;
using Windows.Storage.Provider;


namespace FileTouch
{

    class SocketClient
    {
        private long BUFFER_LENGTH = 2048;
        // Cached Socket object that will be used by each call for the lifetime of this class
        Socket _socket = null;

        // Signaling object used to notify when an asynchronous operation is completed
        static ManualResetEvent _clientDone = new ManualResetEvent(false);

        // Define a timeout in milliseconds for each asynchronous call. If a response is not received within this 
        // timeout period, the call is aborted.
        const int TIMEOUT_MILLISECONDS = 5000;

        // The maximum size of the data buffer to use with the asynchronous socket methods
        const int MAX_BUFFER_SIZE = 2048;
        /// <summary>
        /// Attempt a TCP socket connection to the given host over the given port
        /// </summary>
        /// <param name="hostName">The name of the host</param>
        /// <param name="portNumber">The port number to connect</param>
        /// <returns>A string representing the result of this connection attempt</returns>
        public string Connect(string hostName, int portNumber)
        {
            string result = string.Empty;

            // Create DnsEndPoint. The hostName and port are passed in to this method.
            DnsEndPoint hostEntry = new DnsEndPoint(hostName, portNumber);

            // Create a stream-based, TCP socket using the InterNetwork Address Family. 
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Create a SocketAsyncEventArgs object to be used in the connection request
            SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
            socketEventArg.RemoteEndPoint = hostEntry;

            // Inline event handler for the Completed event.
            // Note: This event handler was implemented inline in order to make this method self-contained.
            socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate(object s, SocketAsyncEventArgs e)
            {
                // Retrieve the result of this request
                result = e.SocketError.ToString();

                // Signal that the request is complete, unblocking the UI thread
                _clientDone.Set();
            });

            // Sets the state of the event to nonsignaled, causing threads to block
            _clientDone.Reset();

            // Make an asynchronous Connect request over the socket
            _socket.ConnectAsync(socketEventArg);

            // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
            // If no response comes back within this time then proceed
            _clientDone.WaitOne(TIMEOUT_MILLISECONDS);

            return result;
        }
        /// <summary>
        /// Send the given data to the server using the established connection
        /// </summary>
        /// <param name="data">The data to send to the server</param>
        /// <returns>The result of the Send request</returns>
        public string Send(string data)
        {
            string response = "Operation Timeout";

            // We are re-using the _socket object initialized in the Connect method
            if (_socket != null)
            {
                // Create SocketAsyncEventArgs context object
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();

                // Set properties on context object
                socketEventArg.RemoteEndPoint = _socket.RemoteEndPoint;
                socketEventArg.UserToken = null;

                // Inline event handler for the Completed event.
                // Note: This event handler was implemented inline in order 
                // to make this method self-contained.
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate(object s, SocketAsyncEventArgs e)
                {
                    response = e.SocketError.ToString();

                    // Unblock the UI thread
                    _clientDone.Set();
                });

                // Add the data to be sent into the buffer
                byte[] payload = Encoding.UTF8.GetBytes(data);
                socketEventArg.SetBuffer(payload, 0, payload.Length);

                // Sets the state of the event to nonsignaled, causing threads to block
                _clientDone.Reset();
                StreamSocket ss = new StreamSocket();


                // Make an asynchronous Send request over the socket
                _socket.SendAsync(socketEventArg);

                // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
                // If no response comes back within this time then proceed
                _clientDone.WaitOne(TIMEOUT_MILLISECONDS);
            }
            else
            {
                response = "Socket is not initialized";
            }

            return response;
        }
        public async void SendFile(StorageFile file)
        {
            int streamPosition = 0;
            string response = "Operation Timeout";
            //send command
            Send("S");
            //send file name
            Send(file.Name);
            var tmp = Receive();
            // We are re-using the _socket object initialized in the Connect method
            if (_socket != null)
            {
                // Create SocketAsyncEventArgs context object
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();

                // Set properties on context object
                socketEventArg.RemoteEndPoint = _socket.RemoteEndPoint;
                socketEventArg.UserToken = null;

                // Inline event handler for the Completed event.
                // Note: This event handler was implemented inline in order 
                // to make this method self-contained.
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate(object s, SocketAsyncEventArgs e)
                {
                    response = e.SocketError.ToString();

                    // Unblock the UI thread
                    _clientDone.Set();
                });

                // Add the data to be sent into the buffer
                Stream stream;

                stream = await file.OpenStreamForReadAsync();
                //send file size
                Send(stream.Length.ToString());
                tmp = Receive();
                App.myFloat.Message = "Sending...";
                using (stream)
                {
                    int len = 0;
                    stream.Position = streamPosition;

                    while (streamPosition < stream.Length)
                    {
                        long memAlloc = stream.Length - streamPosition < BUFFER_LENGTH ? stream.Length - streamPosition : BUFFER_LENGTH;
                        byte[] buffer = new byte[memAlloc];


                        App.myFloat.Value = (float)100 * (float)streamPosition / stream.Length;
                        len = stream.Read(buffer, 0, buffer.Length);
                        if (len > 0)
                        {
                            socketEventArg.SetBuffer(buffer, 0, buffer.Length);

                            // Sets the state of the event to nonsignaled, causing threads to block
                            _clientDone.Reset();
                            StreamSocket ss = new StreamSocket();

                            // Make an asynchronous Send request over the socket
                            _socket.SendAsync(socketEventArg);

                            // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
                            // If no response comes back within this time then proceed
                            _clientDone.WaitOne(TIMEOUT_MILLISECONDS);
                            streamPosition += len;
                        }

                    }
                    App.myFloat.Value = (float)100 * (float)streamPosition / stream.Length;
                    App.myFloat.Message = "This file id: " + Receive();
                    _socket.Close();

                    GC.Collect();
                }
            }
            else
            {
                response = "Socket is not initialized";
            }

        }

        /// <summary>
        /// Receive data from the server using the established socket connection
        /// </summary>
        /// <returns>The data received from the server</returns>
        public string Receive()
        {
            string response = "Operation Timeout";

            // We are receiving over an established socket connection
            if (_socket != null)
            {
                // Create SocketAsyncEventArgs context object
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
                socketEventArg.RemoteEndPoint = _socket.RemoteEndPoint;

                // Setup the buffer to receive the data
                socketEventArg.SetBuffer(new Byte[MAX_BUFFER_SIZE], 0, MAX_BUFFER_SIZE);

                // Inline event handler for the Completed event.
                // Note: This even handler was implemented inline in order to make 
                // this method self-contained.
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate(object s, SocketAsyncEventArgs e)
                {
                    if (e.SocketError == System.Net.Sockets.SocketError.Success)
                    {
                        // Retrieve the data from the buffer
                        response = Encoding.UTF8.GetString(e.Buffer, e.Offset, e.BytesTransferred);
                        response = response.Trim('\0');
                    }
                    else
                    {
                        response = e.SocketError.ToString();
                    }

                    _clientDone.Set();
                });

                // Sets the state of the event to nonsignaled, causing threads to block
                _clientDone.Reset();

                // Make an asynchronous Receive request over the socket
                _socket.ReceiveAsync(socketEventArg);

                // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
                // If no response comes back within this time then proceed
                _clientDone.WaitOne(TIMEOUT_MILLISECONDS);
            }
            else
            {
                response = "Socket is not initialized";
            }

            return response;
        }
        public byte[] ReceiveBytesArray()
        {
            byte[] response = null;// = "Operation Timeout";

            // We are receiving over an established socket connection
            if (_socket != null)
            {
                // Create SocketAsyncEventArgs context object
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
                socketEventArg.RemoteEndPoint = _socket.RemoteEndPoint;

                // Setup the buffer to receive the data
                socketEventArg.SetBuffer(new Byte[MAX_BUFFER_SIZE], 0, MAX_BUFFER_SIZE);

                // Inline event handler for the Completed event.
                // Note: This even handler was implemented inline in order to make 
                // this method self-contained.
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate(object s, SocketAsyncEventArgs e)
                {
                    if (e.SocketError == System.Net.Sockets.SocketError.Success)
                    {
                        // Retrieve the data from the buffer
                        e.Buffer.CopyTo(response, e.Offset);//, e.Offset, e.BytesTransferred);
                    }
                    else
                    {
                    }

                    _clientDone.Set();
                });

                // Sets the state of the event to nonsignaled, causing threads to block
                _clientDone.Reset();

                // Make an asynchronous Receive request over the socket
                _socket.ReceiveAsync(socketEventArg);

                // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
                // If no response comes back within this time then proceed
                _clientDone.WaitOne(TIMEOUT_MILLISECONDS);
            }
            else
            {
            }

            return response;
        }
        public async void ReceiveFile(StorageFile file, string fileID)
        {
            string recv;
            int fileid = 0;
            int streamPosition = 0;
            long filesize = 0;
            string response = "Operation Timeout";
            //send command
            Send("R");
            //send file id
            Send(fileID);
            recv = Receive();

            if (recv == "not found")
            {
                _socket.Close();
                return;
            };

            Send("OK");
            //get file size
            var stmp = Receive();
            filesize = long.Parse(stmp);
            recv += filesize.ToString();
            Send("OK");
            App.myFloat.Message = recv;
          //  _socket.Close();
          //  return;
            // We are re-using the _socket object initialized in the Connect method
            if (_socket != null)
            {
                // Create SocketAsyncEventArgs context object
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();

                // Set properties on context object
                socketEventArg.RemoteEndPoint = _socket.RemoteEndPoint;
                socketEventArg.UserToken = null;

                // Inline event handler for the Completed event.
                // Note: This event handler was implemented inline in order 
                // to make this method self-contained.
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate(object s, SocketAsyncEventArgs e)
                {
                    response = e.SocketError.ToString();

                    // Unblock the UI thread
                    _clientDone.Set();
                });

                // Add the data to be sent into the buffer
                Stream stream = null;
                
                 // Let Windows know that we're finished changing the file so the other app can update the remote version of the file.
                // Completing updates may require Windows to ask for user input.
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                if (status == FileUpdateStatus.Complete)
                {
                    App.myFloat.Message = "File " + file.Name + " was saved.";
                }
                else
                {
                    App.myFloat.Message = "File " + file.Name + " couldn't be saved.";
                }
                
                using (stream)
                {
                    int len = 0;
                    stream.Position = streamPosition;
                    var mystream = await file.OpenStreamForWriteAsync();
                    
                    while (filesize > 0)
                    {
                        App.myFloat.Value = (float)100 * (float)streamPosition / stream.Length;
                        var tmp = ReceiveBytesArray();
                        filesize -= tmp.Length;
                        if (tmp.Length > 0)
                            await FileIO.WriteBytesAsync(file, tmp);

                    }
                    App.myFloat.Value = (float)100 * (float)streamPosition / stream.Length;

                    App.myFloat.Message = "This file id: " + Receive();
                    _socket.Close();

                    GC.Collect();
                }
            }
            else
            {
                response = "Socket is not initialized";
            }

            return;
        }
        /// <summary>
        /// Closes the Socket connection and releases all associated resources
        /// </summary>
        public void Close()
        {
            if (_socket != null)
            {
                _socket.Close();
            }
        }

    }
}

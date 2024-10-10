// ***********************************************************************
// Assembly         : EchoBot.Services
// Author           : JasonTheDeveloper
// Created          : 09-07-2020
//
// Last Modified By : bcage29
// Last Modified On : 02-28-2022
// ***********************************************************************
// <copyright file="BotMediaStream.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>
// <summary>The bot media stream.</summary>
// ***********************************************************************-

using Microsoft.Graph.Communications.Calls;
using Microsoft.Graph.Communications.Calls.Media;
using Microsoft.Graph.Communications.Common;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Skype.Bots.Media;
using Microsoft.Skype.Internal.Media.Services.Common;
using EchoBot.Services.Media;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using EchoBot.Services.ServiceSetup;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using IOFile = System.IO.File;
using System.Net;
using Microsoft.Extensions.Configuration;
using Sprache;
using System.Web.Http.Results;


namespace EchoBot.Services.Bot
{
    /// <summary>
    /// Class responsible for streaming audio and video.
    /// </summary>
    public class BotMediaStream : ObjectRootDisposable
    {
        private AppSettings _settings;

        /// <summary>
        /// The participants
        /// </summary>
        internal List<IParticipant> participants;

        /// <summary>
        /// The audio socket
        /// </summary>
        private readonly IAudioSocket _audioSocket;
        /// <summary>
        /// The media stream
        /// </summary>
        private readonly ILogger _logger;
        private AudioVideoFramePlayer audioVideoFramePlayer;
        private readonly TaskCompletionSource<bool> audioSendStatusActive;
        private readonly TaskCompletionSource<bool> startVideoPlayerCompleted;
        private AudioVideoFramePlayerSettings audioVideoFramePlayerSettings;
        private List<AudioMediaBuffer> audioMediaBuffers = new List<AudioMediaBuffer>();
        private int shutdown;
        private Dictionary<string, CognitiveServicesService> _languageServiceDict;
        private string myCallId;
        private int numberParticipants;
        private Dictionary<string, string> myParticipatsWithId;

        private string tenantId;
        private string threadId;
        public string myOrganizerId;
        public string myOrganizerName;
        private IConfidentialClientApplication app;
        private GraphServiceClient graphClient;
        private string timeStart = "";
        private int initMyMeet = 0;
        private string myMainLenguage = "en-US";
        private int[] myLenguagesId = new int[] { 0, 0 };///0: English, 1: Spanish
        private int counterParticipantTokens = 0;
        private int counterErrorsOnAudioMediaReceived  = 0;
        private int controlInformationLabelParticipantsNotFound = 0;
        private int controlErrorParticipantsNotFound = 0;
        /// <summary>
        /// Initializes a new instance of the <see cref="BotMediaStream" /> class.
        /// </summary>
        /// <param name="mediaSession">The media session.</param>
        /// <param name="callId">The call identity</param>
        /// <param name="graphLogger">The Graph logger.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="settings">Azure settings</param>
        /// <exception cref="InvalidOperationException">A mediaSession needs to have at least an audioSocket</exception>
        public BotMediaStream(
            ILocalMediaSession mediaSession,
            string callId,
            IGraphLogger graphLogger,
            ILogger logger,
            AppSettings settings,
            string tenantId="",
            string threadId=""
        )
            : base(graphLogger)
        {
            ArgumentVerifier.ThrowOnNullArgument(mediaSession, nameof(mediaSession));
            ArgumentVerifier.ThrowOnNullArgument(logger, nameof(logger));
            ArgumentVerifier.ThrowOnNullArgument(settings, nameof(settings));
            myCallId = callId;
            Console.WriteLine($"my call Id in the BotMediaStream is:{callId}");
            numberParticipants = 0;
            myParticipatsWithId = new Dictionary<string, string>();
            this.tenantId = tenantId;
            this.threadId = threadId;

            _settings = settings;

            this.participants = new List<IParticipant>();

            this.audioSendStatusActive = new TaskCompletionSource<bool>();
            this.startVideoPlayerCompleted = new TaskCompletionSource<bool>();

            // Subscribe to the audio media.
            this._audioSocket = mediaSession.AudioSocket;
            if (this._audioSocket == null)
            {
                throw new InvalidOperationException("A mediaSession needs to have at least an audioSocket");
            }

            this._audioSocket.AudioSendStatusChanged += OnAudioSendStatusChanged;

            _logger = logger;

            this._audioSocket.AudioMediaReceived += this.OnAudioMediaReceived;

            var ignoreTask = this.StartAudioVideoFramePlayerAsync().ForgetAndLogExceptionAsync(this.GraphLogger, "Failed to start the player");

            if (_settings.UseCognitiveServices)
            {
                _logger.LogInformation($" **** in boMediaStream settings={_settings} logger={_logger}  callId={callId}");
                _languageServiceDict = new Dictionary<string, CognitiveServicesService>();

// await botMediaStream.InitializeAsync();
            }
            DateTime utcNow = DateTime.UtcNow; timeStart = utcNow.ToString();
            GlobalVariables.writeFileControl(2,"", myCallId, tenantId);
        }
        public async Task InitializeAsync()
        {
            //await _defLanguageService.TextToSpeech("Hello team, we start the meeting");
        }

        ~BotMediaStream()
        {
            Console.WriteLine("create destructor of object BotMediaStream");
        }

        /// <summary>
        /// Gets the participants.
        /// </summary>
        /// <returns>List&lt;IParticipant&gt;.</returns>
        public List<IParticipant> GetParticipants()
        {
            return participants;
        }

        /// <summary>
        /// Shut down.
        /// </summary>
        /// <returns><see cref="Task" />.</returns>
        public async Task ShutdownAsync()
        {
            if (Interlocked.CompareExchange(ref this.shutdown, 1, 1) == 1)
            {
                return;
            }

            await this.startVideoPlayerCompleted.Task.ConfigureAwait(false);

            // unsubscribe
            if (this._audioSocket != null)
            {
                this._audioSocket.AudioSendStatusChanged -= this.OnAudioSendStatusChanged;
            }

            // shutting down the players
            if (this.audioVideoFramePlayer != null)
            {
                await this.audioVideoFramePlayer.ShutdownAsync().ConfigureAwait(false);
            }

            // make sure all the audio and video buffers are disposed, it can happen that,
            // the buffers were not enqueued but the call was disposed if the caller hangs up quickly
            foreach (var audioMediaBuffer in this.audioMediaBuffers)
            {
                audioMediaBuffer.Dispose();
            }

            _logger.LogInformation($"disposed {this.audioMediaBuffers.Count} audioMediaBUffers.");

            this.audioMediaBuffers.Clear();
        }
        
        /// <summary>
        /// Initialize AV frame player.
        /// </summary>
        /// <returns>Task denoting creation of the player with initial frames enqueued.</returns>
        private async Task StartAudioVideoFramePlayerAsync()
        {
            try
            {
                await Task.WhenAll(this.audioSendStatusActive.Task).ConfigureAwait(false);

                _logger.LogInformation("Send status active for audio and video Creating the audio video player");
                this.audioVideoFramePlayerSettings =
                    new AudioVideoFramePlayerSettings(new AudioSettings(20), new VideoSettings(), 1000);
                this.audioVideoFramePlayer = new AudioVideoFramePlayer(
                    (AudioSocket)_audioSocket,
                    null,
                    this.audioVideoFramePlayerSettings);

                _logger.LogInformation("created the audio video player");
            }
            catch (Exception ex)
            {
                GlobalVariables.writeFileControl(4, "File BotMediaStream.cs, function StartAudioVideoFramePlayerAsync: " + ex.Message, myCallId);
                _logger.LogError(ex, "Failed to create the audioVideoFramePlayer with exception");
            }
            finally
            {
                this.startVideoPlayerCompleted.TrySetResult(true);
            }
        }

        /// <summary>
        /// Callback for informational updates from the media plaform about audio status changes.
        /// Once the status becomes active, audio can be loopbacked.
        /// </summary>
        /// <param name="sender">The audio socket.</param>
        /// <param name="e">Event arguments.</param>
        private void OnAudioSendStatusChanged(object sender, AudioSendStatusChangedEventArgs e)
        {
            _logger.LogTrace($"[AudioSendStatusChangedEventArgs(MediaSendStatus={e.MediaSendStatus})]");
           // _logger.LogTrace($"[Amy new audio={e.Buffer})]");

            if (e.MediaSendStatus == MediaSendStatus.Active)
            {
                this.audioSendStatusActive.TrySetResult(true);
            }
        }

        public void sendSummary(string callId, string tenantId = "619e851e-8e70-43fc-a9a4-d609af5db018", string threadId = "")
        {
            try
            {
                DateTime utcNow = DateTime.UtcNow;
                string timeEnd = utcNow.ToString();
                Console.WriteLine("La hora actual en UTC es 2: " + utcNow.ToString("HH:mm:ss"));

                string filePath = @"C:\archivo_" + callId + ".txt";

                if (System.IO.Directory.Exists(@"C:\API"))
                {
                    filePath = @"C:\API\call_" + callId + ".txt";
                }


                string userEmailCurrent = ""; 

                try
                {
                    bool found = GlobalVariables.MyGlobalUserEmail.TryGetValue(callId, out userEmailCurrent);
                    if (!found)
                    {
                        userEmailCurrent = "defaultEmail@example.com"; 
                    }
                } catch (Exception ex)
                {
                    GlobalVariables.writeFileControl(4, "File BotMediaStream.cs, function sendSummary found email: " + ex.Message, callId);
                    Console.WriteLine($"Se produjo una excepci�n: {ex.Message}");
                    userEmailCurrent = "error"; 
                }
                Console.WriteLine(" ------ my Email is: ");
                Console.WriteLine(userEmailCurrent);
                Console.WriteLine(" ------ my callId is: ");
                Console.WriteLine(callId);
                //string filePath = @"F:\API\Transcripts\call_" + callId + ".txt";
                while (!GlobalVariables.MyGlobalDictionary[callId].IsEmpty)
                {
                    if (GlobalVariables.MyGlobalDictionary[callId].TryDequeue(out string result))
                    {
                        if (!System.IO.File.Exists(filePath))
                        {
                            System.IO.File.WriteAllText(filePath, result + Environment.NewLine);
                        }
                        else
                        {
                            System.IO.File.AppendAllText(filePath, result + Environment.NewLine);
                        }
                    }
                }
                //string fileContent = IOFile.ReadAllText(filePath);
                string fileContent = "";

                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        fileContent = IOFile.ReadAllText(filePath);
                        break;
                    }
                    catch (Exception ex)
                    {

                        if (controlErrorParticipantsNotFound++ % 100 == 0)
                            GlobalVariables.writeFileControl(4, "File BotMediaStream.cs, function sendSummary in filel content error for: " + ex.Message, callId);
                        Console.WriteLine("Error reading file, attempt " + (i + 1) + ": " + ex.Message);
                        if (i < 2) // Wait only if it's not the last attempt
                        {
                            System.Threading.Thread.Sleep(5000); // Wait for 5 seconds before retrying
                        }
                    }
                }
                string transcription = fileContent.Replace(Environment.NewLine, @"\n");

                var currentLanguage = "en-US";
                if (GlobalVariables.MyGlobalLanguage.ContainsKey(myCallId))
                {
                    currentLanguage = GlobalVariables.MyGlobalLanguage[myCallId];
                }
                var url = "https://echobot-azure-function-python.azurewebsites.net/api/orchestrators/generate_summary";
                //var url = "https://new-messages-webhook-dev.azurewebsites.net/api/obtainSummary";
                //var url = "https://8918-20-55-90-54.ngrok-free.app/api/obtainSummary";
                var datos = new { callId, tenantId, transcription, threadId, myOrganizerId, myOrganizerName, timeStart, timeEnd, currentLanguage, userEmailCurrent};

                using (var httpClient = new HttpClient())
                {
                    var content = new StringContent(JsonConvert.SerializeObject(datos), Encoding.UTF8, "application/json");
                    var response = httpClient.PostAsync(url, content).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                }
                GlobalVariables.writeFileControl(3, "", callId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred in; sendSummary: " + ex.Message);
                if(controlErrorParticipantsNotFound++ % 100 == 0)
                    GlobalVariables.writeFileControl(4, "File BotMediaStream.cs, function sendSummary: " + ex.Message, callId);
            }
        }

        /**
        * Function updateCurrentParticipants
        *
        * What this function does is update our ansParticipant map.
        * The key of this map is a number, the participant's microphone ID,
        * and its value is the participant's name.
        * If this is wrong, it won't recognize the participant.
        * This function is called by default in 3 occasions:
        * 1: when a new participant is added to the call, this is in the CallHandler.cs class.
        * 2: when a participant is removed (leaves) from the call, this is in the CallHandler.css class.
        * 3: in this same class, in the OnAudioMediaReceived function, it is called here because it detects audio, 
        * but it doesn't detect which participant that audio belongs to, so the participant list is updated again to detect 
        * that participant.
        */
        public async Task updateCurrentParticipants(bool useEndPoint = false)
        {
            try
            {
                Dictionary<string, string> ansParticipant = new Dictionary<string, string>();
                //Console.WriteLine("this line update the current participant in bot Media Stream");
                //Console.WriteLine("before to input for participants");
                if (this.participants.Count == 0)
                {
                    Console.WriteLine("this break because the numberof partidcipant is 0");
                    sendSummary(myCallId, tenantId, threadId);
                }
                var myNamesParticipantsEndPoint = new Dictionary<string, string>();
                var builder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);


                IConfigurationRoot configuration = builder.Build();
                //Console.WriteLine("**##**##**##" + configuration.GetSection("AzureSettings").GetValue<string>("AadAppId"));
               // Console.WriteLine("**##**##**##" + configuration.GetSection("AzureSettings").GetValue<string>("AadAppSecret"));

                foreach (IParticipant myParticipant in this.participants)
                {
                    Console.WriteLine("Participant ID Bot: " + myParticipant.Id);
                    //int newData = int.Parse(myParticipant.Id);
                    //Console.WriteLine("Participant ID2: " + newData);
                    Console.WriteLine("Participant Name: Bot" + myParticipant.Resource.Info?.Identity?.User?.DisplayName);
                    //Console.WriteLine("Participant Name: Bot" + myParticipant.Resource.Info?.Identity?.Guest?.DisplayName);
                    Console.WriteLine("Participant Is In Lobby: Bot" + myParticipant.Resource.IsInLobby);
                    Console.WriteLine("Participant Is Muted: Bot" + myParticipant.Resource.IsMuted);

                    foreach (var mediaStream in myParticipant.Resource.MediaStreams)
                    {
                        Console.WriteLine("Participant is sourceId: " + mediaStream.SourceId);

                        Console.WriteLine("my data mediaStream in the cicle is: " + mediaStream.SourceId);
                        if (!ansParticipant.ContainsKey(mediaStream.SourceId))
                        {
                            ansParticipant[mediaStream.SourceId] = myParticipant.Resource.Info?.Identity?.User?.DisplayName;
                            if (ansParticipant[mediaStream.SourceId] == null && useEndPoint)
                            {
                                myNamesParticipantsEndPoint = await RepeatedTask();
                                if (myNamesParticipantsEndPoint.ContainsKey(mediaStream.SourceId))
                                {
                                    ansParticipant[mediaStream.SourceId] = myNamesParticipantsEndPoint[mediaStream.SourceId];
                                }
                                else
                                {
                                    System.Threading.Thread.Sleep(10000);
                                    myNamesParticipantsEndPoint = await RepeatedTask();
                                    if (myNamesParticipantsEndPoint.ContainsKey(mediaStream.SourceId))
                                    {
                                        ansParticipant[mediaStream.SourceId] = myNamesParticipantsEndPoint[mediaStream.SourceId];
                                    }
                                    else
                                    {
                                        ansParticipant[mediaStream.SourceId] = "Guest";
                                    }
                                }
                            }
                        }
                        if (!_languageServiceDict.ContainsKey(mediaStream.SourceId))
                        {
                            Console.WriteLine("add in funtion update participant, the language" + GlobalVariables.MyGlobalLanguage[myCallId]);
                            var currentLanguage = "en-US";
                            if (GlobalVariables.MyGlobalLanguage.ContainsKey(myCallId))
                            {
                                currentLanguage = GlobalVariables.MyGlobalLanguage[myCallId];
                            }
                            CognitiveServicesService _myLanguageService = new CognitiveServicesService(_settings, _logger, myCallId, ansParticipant[mediaStream.SourceId], currentLanguage);
                            _myLanguageService.SendMediaBuffer += this.OnSendMediaBuffer;
                            _languageServiceDict.Add(mediaStream.SourceId, _myLanguageService);
                        }
                        break;
                    }

                }
                //Console.WriteLine("after to input for participants");
                //_languageService.setMyParticipantsWithId(ansParticipant);
            }
            catch(Exception ex)
            {
                GlobalVariables.writeFileControl(4, "File BotMediaStream.cs, function updateCurrentParticipants: " + ex.Message, myCallId);
                Console.WriteLine("error in updateCurrentParticipants: " + ex);
            }

        }


        /// <summary>
        /// Receive audio from subscribed participant.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The audio media received arguments.</param>
        private async void OnAudioMediaReceived(object sender, AudioMediaReceivedEventArgs e)
        {
         

            if(e.Buffer.UnmixedAudioBuffers != null) {
                
                foreach (UnmixedAudioBuffer _my_buffer in e.Buffer.UnmixedAudioBuffers)
                try
                {
                    //Console.WriteLine(" my buffer is: _my_buffer");
                        var participant = string.Join("\r\n", _my_buffer.ActiveSpeakerId);
                        if(counterParticipantTokens++%1000 == 0)
                            Console.WriteLine("*participant is: "+participant+" __"+counterParticipantTokens);
                        if (_languageServiceDict.ContainsKey(participant))
                    {
                        await _languageServiceDict[participant].AppendAudioBuffer(_my_buffer, e.Buffer);
                           // Console.WriteLine("its my case 1");
                    }
                    else
                    {
                           // Console.WriteLine("its my case 2");
                            await updateCurrentParticipants(true);
                                   // send audio buffer back on the audio socket
                                   // the particpant talking will hear themselves
                                   var length = e.Buffer.Length;
                        if (length > 0)
                        {
                            var buffer = new byte[length];
                            Marshal.Copy(_my_buffer.Data, buffer, 0, (int)length);

                            var currentTick = DateTime.Now.Ticks;
                            this.audioMediaBuffers = Util.Utilities.CreateAudioMediaBuffers(buffer, currentTick, _logger);
                            await this.audioVideoFramePlayer.EnqueueBuffersAsync(this.audioMediaBuffers, new List<VideoMediaBuffer>());
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.GraphLogger.Error(ex);
                    if(counterErrorsOnAudioMediaReceived++%1000 == 0) 
                        GlobalVariables.writeFileControl(4, "File BotMediaStream.cs, function OnAudioMediaReceived A: " + ex.Message, myCallId);
                    _logger.LogError(ex, "OnAudioMediaReceived error");
                }
                finally
                {
                    //e.Buffer.Dispose();
                }
            }
            
            if(e.Buffer.UnmixedAudioBuffers == null) 
            try
            {
                    var participant = string.Join("\r\n", e.Buffer.ActiveSpeakers);

                 

                        foreach (string key in _languageServiceDict.Keys)
                        {
                            await _languageServiceDict[key].AppendAudioBuffer(e.Buffer);
                        }
                    

                    e.Buffer.Dispose();
                     
             /*   else
                {
                    // send audio buffer back on the audio socket
                    // the particpant talking will hear themselves
                    var length = e.Buffer.Length;
                    if (length > 0)
                    {
                        var buffer = new byte[length];
                        Marshal.Copy(e.Buffer.Data, buffer, 0, (int)length);

                        var currentTick = DateTime.Now.Ticks;
                        this.audioMediaBuffers = Util.Utilities.CreateAudioMediaBuffers(buffer, currentTick, _logger);
                        await this.audioVideoFramePlayer.EnqueueBuffersAsync(this.audioMediaBuffers, new List<VideoMediaBuffer>());
                    }
                }*/
            }
            catch (Exception ex)
            {
                this.GraphLogger.Error(ex);
                if(counterErrorsOnAudioMediaReceived++%1000 == 0)   
                    GlobalVariables.writeFileControl(4, "File BotMediaStream.cs, function OnAudioMediaReceived B: " + ex.Message, myCallId);
                    _logger.LogError(ex, "OnAudioMediaReceived error");
            }
            finally
            {
               // e.Buffer.Dispose();
            }
            e.Buffer.Dispose();

        }

        private void OnSendMediaBuffer(object sender, MediaStreamEventArgs e)
        {

            this.audioMediaBuffers = e.AudioMediaBuffers;
            var result = Task.Run(async () => await this.audioVideoFramePlayer.EnqueueBuffersAsync(this.audioMediaBuffers, new List<VideoMediaBuffer>())).GetAwaiter();
        }
        ///Request obatin token

        private static readonly HttpClient client = new HttpClient();
        private async Task<Dictionary<string,string>> RepeatedTask()
        {
            Dictionary<string, string> displayNameDictionary = new Dictionary<string, string>();

            try
            {

                var now = DateTime.Now;
                Console.WriteLine($"############### 12345678987654321 ################## in time {now:hh:mm:ss}");

                var builder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);


                IConfigurationRoot configuration = builder.Build();
                string tenantId = this.tenantId;
                
                
                string clientId = configuration.GetSection("AzureSettings").GetValue<string>("AadAppId");
                //string clientId = GlobalVariables.keyAppId;
                string clientSe = configuration.GetSection("AzureSettings").GetValue<string>("AadAppSecret");
                //string clientSe = GlobalVariables.keyAppSecret;

                string callId = myCallId;
                Console.WriteLine($"############### callId ################## in time {callId}");

                var token = await GetToken(tenantId, clientId, clientSe);
                if (token != null)
                {
                    var participants = await GetParticipantsEndPoint(callId, token);
                    if (participants != null)
                    {
                        var participantsJson = JObject.Parse(participants);
                        var participantsArray = participantsJson["value"] as JArray;
                        Console.WriteLine($"Participants count: {participantsArray.Count}");

                        foreach (var participant in participantsArray)
                        {
                            Console.WriteLine($"Participant ID: {participant["id"]}");

                            if (participant["info"]?["identity"]?["user"] is JObject user)
                            {
                                Console.WriteLine("YES 4 field USER");

                                if (user["displayName"] != null)
                                {
                                    Console.WriteLine("YES 5 field DISPLAY NAME" + user["displayName"]);

                                    // Obteniendo el ID de la fuente de audio.
                                    var audioSourceId = "";
                                    if (participant["mediaStreams"] is JArray mediaStreams)
                                    {
                                        foreach (var stream in mediaStreams)
                                        {
                                            if (stream is JObject dataSource)
                                            {
                                                Console.WriteLine("My data Source is:" + dataSource["sourceId"]);
                                                audioSourceId = dataSource["sourceId"].ToString();
                                            }
                                            break;
                                        }
                                        // Si el ID de la fuente de audio es v�lido, agregarlo al diccionario.
                                        if (!string.IsNullOrEmpty(audioSourceId))
                                        {
                                            displayNameDictionary[audioSourceId] = user["displayName"].ToString();
                                            GlobalVariables.writeFileControl(5, "File BotMediaStream.cs, function RepeatedTask add participant A: audioSourceId: " + audioSourceId + ", user[\"displayName\"].ToString(): "+ user["displayName"].ToString(), myCallId);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("NO 5 field DISPLAY NAME");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("NO 4 field USER or USER is null");
                            }
                        }

                        Console.WriteLine($"YES: Participants were obtained successfully.");
                    }
                    else
                    {
                        Console.WriteLine($"NO: Participants could not be obtained.");
                    }
                }
                else
                {
                    Console.WriteLine($"NO: Token could not be obtained.");
                }
                return displayNameDictionary;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }


        // my request http
        private static readonly HttpClient clientEndPoint = new HttpClient();
        public async Task<string> GetToken(string tenantId, string clientId, string clientSecret)
        {
            var url = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
            var values = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "scope", "https://graph.microsoft.com/.default" },
                { "client_secret", clientSecret },
                { "grant_type", "client_credentials" }
            };

            var content = new FormUrlEncodedContent(values);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                var response = await clientEndPoint.PostAsync(url, content, cts.Token);

                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseString);

                return jsonResponse["access_token"].ToString();
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Request time out.");
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Request error: {e.Message}");
            }

            return null;
        }

        public async Task<string> GetParticipantsEndPoint(string callId, string token)
        {
            var url = $"https://graph.microsoft.com/v1.0/communications/calls/{callId}/participants";
            clientEndPoint.DefaultRequestHeaders.Clear();
            clientEndPoint.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                var response = await clientEndPoint.GetAsync(url, cts.Token);

                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();

                return responseString;
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Request time out.");
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Request error: {e.Message}");
            }

            return null;
        }

    }
}

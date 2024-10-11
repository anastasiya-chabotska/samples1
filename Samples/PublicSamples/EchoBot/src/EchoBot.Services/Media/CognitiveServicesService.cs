using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Skype.Bots.Media;
using EchoBot.Services.ServiceSetup;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Emit;
using static System.Net.Mime.MediaTypeNames;
using System.Linq;
using Newtonsoft.Json;
using System.Net.Http;

using System.Text;
using System.Collections.Concurrent;

namespace EchoBot.Services.Media
{
    /// <summary>
    /// Class CognitiveServicesService.
    /// </summary>
    public class CognitiveServicesService
    {
        /// <summary>
        /// The is the indicator if the media stream is running
        /// </summary>
        private bool _isRunning = false;
        /// <summary>
        /// The is draining indicator
        /// </summary>
        protected bool _isDraining;
        
        /// <summary>
        /// The logger
        /// </summary>
        private readonly ILogger _logger;
        private readonly PushAudioInputStream _audioInputStream = AudioInputStream.CreatePushStream(AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));
        private readonly AudioOutputStream _audioOutputStream = AudioOutputStream.CreatePullStream();

        private readonly SpeechConfig _speechConfig;
        private SpeechRecognizer _recognizer;
        private readonly SpeechSynthesizer _synthesizer;

        private String myCurrentCallId;
        private string mainSpeaker;
        private bool startMeet = true;

        private string currentLenguage = "";
        private string firstLenguage;
        /// <summary>
        /// Initializes a new instance of the <see cref="CognitiveServicesService" /> class.
        public CognitiveServicesService(AppSettings settings, ILogger logger, String newCallId, string speakerName, string language, string myfirstLenguage = "")
        {
            GlobalVariables.WriteGeneralLog("--- Info: create new cognitive services serice 1: " + newCallId, "Info");

            _logger = logger;
            myCurrentCallId = newCallId;
            mainSpeaker = speakerName;
            firstLenguage = myfirstLenguage;

            //_speechConfig = SpeechConfig.FromSubscription(settings.SpeechConfigKey, settings.SpeechConfigRegion);
            _speechConfig = SpeechConfig.FromSubscription("9e07f79a331d48fd871220dd8a1e2f89", "eastus");
            _speechConfig.SpeechSynthesisLanguage = language;//settings.BotLanguage;
            _speechConfig.SpeechRecognitionLanguage = language;// settings.BotLanguage;

            var audioConfig = AudioConfig.FromStreamOutput(_audioOutputStream);
            _synthesizer = new SpeechSynthesizer(_speechConfig, audioConfig);
            Console.WriteLine("constructor echobot.services/media/congnitiveServicesService step 1 ");
        }
/*        public void setMyParticipantsWithId(Dictionary<string, string> newMyParticipantsWithId) 
        {
            Console.WriteLine("before  data of participantsId");
            foreach (KeyValuePair<string, string> participant in myParticipantsWithId) Console.WriteLine("Clave: {0}, Valor: {1}", participant.Key, participant.Value);
            Console.WriteLine("after data of participantsId");
            foreach (KeyValuePair<string, string> participant in newMyParticipantsWithId) Console.WriteLine("Clave: {0}, Valor: {1}", participant.Key, participant.Value);
            this.myParticipantsWithId = newMyParticipantsWithId;
        }
*/        /// <summary>
        /// Appends the audio buffer.
        /// </summary>
        /// <param name="audioBuffer"></param>
        public async Task AppendAudioBuffer(AudioMediaBuffer audioBuffer,bool initMeet = true)
        {
            if (!_isRunning)
            {
                startMeet = initMeet;
                _logger.LogInformation($"append mixed");
                Start();
                
                await ProcessSpeech(audioBuffer);
            }

            try
            {
                // audio for a 1:1 call
                var bufferLength = audioBuffer.Length;
                if (bufferLength > 0)
                {
                    //_logger.LogInformation($"input in rurring 1vs1 if Task: ");
                    var buffer = new byte[bufferLength];
                    Marshal.Copy(audioBuffer.Data, buffer, 0, (int)bufferLength);

                    _audioInputStream.Write(buffer);
                }
           
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception happend writing to input stream");
            }
        }




        public async Task AppendAudioBuffer(UnmixedAudioBuffer audioBuffer, AudioMediaBuffer originalBuffer, bool initMeet = false)
        {
            if (!_isRunning)
            {
                startMeet = initMeet;
                _logger.LogInformation($"append unmixed");
                Start();
            
                await ProcessSpeech(originalBuffer);
            }

            try
            {
                //_logger.LogInformation($"input in rurring 1vs 1 Task: ");
                // audio for a 1:1 call
                var bufferLength = audioBuffer.Length;
                if (bufferLength > 0)
                {
                    //_logger.LogInformation($"input in rurring 1vs1 if Task: ");
                    var buffer = new byte[bufferLength];
                    Marshal.Copy(audioBuffer.Data, buffer, 0, (int)bufferLength);

                    _audioInputStream.Write(buffer);
                }
             
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception happend writing to input stream");
            }
        }


        protected virtual void OnSendMediaBufferEventArgs(object sender, MediaStreamEventArgs e)
        {
            if (SendMediaBuffer != null)
            {
                SendMediaBuffer(this, e);
            }
        }

        public event EventHandler<MediaStreamEventArgs> SendMediaBuffer;

        /// <summary>
        /// Ends this instance.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ShutDownAsync()
        {
            if (!_isRunning)
            {
                return;
            }

            if (_isRunning)
            {
                await _recognizer.StopContinuousRecognitionAsync();
                _recognizer.Dispose();
                _audioInputStream.Dispose();
                _audioOutputStream.Dispose();
                _synthesizer.Dispose();

                _isRunning = false;
            }
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        private void Start()
        {
            if (!_isRunning)
            {
                _isRunning = true;
            }
        }

        /// <summary>
        /// Processes this instance.
        /// </summary>
        private async Task ProcessSpeech(AudioMediaBuffer audioBuffer)
        {
            _logger.LogInformation($"entra aqui funcion Task");
            var autoDetectSourceLanguageConfig =
                AutoDetectSourceLanguageConfig.FromLanguages(
                    new string[] { "en-US", "de-DE", "es-ES" });
            try
            {
                var stopRecognition = new TaskCompletionSource<int>();

                using (var audioInput = AudioConfig.FromStreamInput(_audioInputStream))
                {
                    if (_recognizer == null)
                    {
                        _logger.LogInformation("init recognizer");
                        _recognizer = new SpeechRecognizer(_speechConfig, audioInput);  // to activate a single language
                        //_recognizer = new SpeechRecognizer(_speechConfig, autoDetectSourceLanguageConfig, audioInput);  // to activate autodetect language
                        /*if (firstLenguage == "")
                        {
                            _recognizer = new SpeechRecognizer(_speechConfig, autoDetectSourceLanguageConfig, audioInput);
                        }
                        else
                        {
                            _recognizer = new SpeechRecognizer(_speechConfig, audioInput);
                        }*/
                    }
                }
              
                _recognizer.Recognizing += (s, e) =>
                {
                    _logger.LogInformation($"1 RECOGNIZING: Text={e.Result.Text} + {mainSpeaker} ");
                };

                _recognizer.Recognized += async (s, e) =>
                {

                    if (e.Result.Reason == ResultReason.RecognizedSpeech)
                    {
                        if (string.IsNullOrEmpty(e.Result.Text))
                            return;
                        var autoDetectSourceLanguageResult = AutoDetectSourceLanguageResult.FromResult(e.Result);
                        _logger.LogInformation($"2 RECOGNIZED: Text={e.Result.Text} +={mainSpeaker}");

                        //{autoDetectSourceLanguageResult.Language}

                        string text = mainSpeaker + ": " + e.Result.Text;
                        //GlobalVariables.WriteGeneralLog("--- Info: transcription: " + text, "Info");
                        //GlobalVariables.MyGlobalQueue.Enqueue(text);
                        if (GlobalVariables.MyGlobalDictionary.ContainsKey(myCurrentCallId))
                        {
                            GlobalVariables.MyGlobalDictionary[myCurrentCallId].Enqueue(text);
                        }
                        else
                        {
                            ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
                            queue.Enqueue(text);
                            GlobalVariables.MyGlobalDictionary.TryAdd(myCurrentCallId, queue);
                        }
                        /*
                        _logger.LogInformation($"2 RECOGNIZED: Text={e.Result.Text} +={mainSpeaker}");
    
                        string text = mainSpeaker + ": " + e.Result.Text;
                      

                        string path = @"F:\API\Transcripts\call_"+ myCurrentCallId + ".txt";
                        if (!File.Exists(path))
                        {
                            // If the file does not exist, create it and add the text
                            File.WriteAllText(path, text + Environment.NewLine);
                        }
                        else
                        {
                            // If the file exists, add the text to the end
                            File.AppendAllText(path, text + Environment.NewLine);
                        }
                        // We recognized the speech
                        // Now do Speech to Text
                        //await TextToSpeech(e.Result.Text);
                        */
                    }
                    else if (e.Result.Reason == ResultReason.NoMatch)
                    {
                        _logger.LogInformation($"NOMATCH: Speech could not be recognized.");
                    }
                };

                _recognizer.Canceled += (s, e) =>
                {
                    _logger.LogInformation($"CANCELED: Reason={e.Reason}");

                    if (e.Reason == CancellationReason.Error)
                    {
                        _logger.LogInformation($"CANCELED: ErrorCode={e.ErrorCode}");
                        _logger.LogInformation($"CANCELED: ErrorDetails={e.ErrorDetails}");
                        _logger.LogInformation($"CANCELED: Did you update the subscription info?");
                    }

                    stopRecognition.TrySetResult(0);
                };

                _recognizer.SessionStarted += async (s, e) =>
                {
                    _logger.LogInformation("\nSession started event.");
                    if(startMeet)
                    {
                       // await TextToSpeech("Hello team, we start the meeting");
                    }
                };

                _recognizer.SessionStopped += (s, e) =>
                {
                    _logger.LogInformation("\nSession stopped event.");
                    _logger.LogInformation("\nStop recognition.");
                    stopRecognition.TrySetResult(0);
                };

                // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
                await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                // Waits for completion.
                // Use Task.WaitAny to keep the task rooted.
                Task.WaitAny(new[] { stopRecognition.Task });

                // Stops recognition.
                await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException ex)
            {
                GlobalVariables.writeFileControl(4, "File BotMediaStream.cs, function ProcessSpeech A: " + ex.Message, myCurrentCallId);
                _logger.LogError(ex, "The queue processing task object has been disposed.");
            }
            catch (Exception ex)
            {
                // Catch all other exceptions and log
                GlobalVariables.writeFileControl(4, "File BotMediaStream.cs, function ProcessSpeech B: " + ex.Message, myCurrentCallId);
                _logger.LogError(ex, "Caught Exception");
            }

            _isDraining = false;
        }


/*        private async Task ProcessSpeech(UnmixedAudioBuffer audioBuffer)
        {
            _logger.LogInformation($"entra aqui funcion Task");
            try
            {
                var stopRecognition = new TaskCompletionSource<int>();

                using (var audioInput = AudioConfig.FromStreamInput(_audioInputStream))
                {
                    if (_recognizer == null)
                    {
                        _logger.LogInformation("init recognizer");
                        _recognizer = new SpeechRecognizer(_speechConfig, audioInput);
                    }
                }
                String temporalString = (audioBuffer.ActiveSpeakerId != null) ? string.Join("\r\n", audioBuffer.ActiveSpeakerId) : "";
                _recognizer.Recognizing += (s, e) =>
                {
                    _logger.LogInformation($"1 RECOGNIZING: Text={e.Result.Text} + {temporalString} {myCurrentCallId}");
                };

                _recognizer.Recognized += async (s, e) =>
                {
                    _logger.LogInformation($" data e : Text={e}");

                    if (e.Result.Reason == ResultReason.RecognizedSpeech)
                    {
                        if (string.IsNullOrEmpty(e.Result.Text))
                            return;


                        _logger.LogInformation($"2 RECOGNIZED: Text={e.Result.Text} +={temporalString}");
                        string text = "";
                        if (myParticipantsWithId.ContainsKey(myCurrentSpeaker))
                        {
                            text = myParticipantsWithId[myCurrentSpeaker] + ": " + e.Result.Text;
                        }
                        else
                        {
                            Console.WriteLine("************* ERROR MAP ***************");
                            Console.WriteLine($"a key does not exist in the dictionary .{myCurrentSpeaker}., the previous speaker is .{previousSpeaker}.");
                            text = ": " + e.Result.Text;
                        }

                        string path = @"C:\archivo_" + myCurrentCallId + ".txt";
                        if (!File.Exists(path))
                        {
                            // If the file does not exist, create it and add the text
                            File.WriteAllText(path, text + Environment.NewLine);
                        }
                        else
                        {
                            // If the file exists, add the text to the end
                            File.AppendAllText(path, text + Environment.NewLine);
                        }
                        // We recognized the speech
                        // Now do Speech to Text
                        //await TextToSpeech(e.Result.Text);

                    }
                    else if (e.Result.Reason == ResultReason.NoMatch)
                    {
                        _logger.LogInformation($"NOMATCH: Speech could not be recognized.");
                    }
                };

                _recognizer.Canceled += (s, e) =>
                {
                    _logger.LogInformation($"CANCELED: Reason={e.Reason}");

                    if (e.Reason == CancellationReason.Error)
                    {
                        _logger.LogInformation($"CANCELED: ErrorCode={e.ErrorCode}");
                        _logger.LogInformation($"CANCELED: ErrorDetails={e.ErrorDetails}");
                        _logger.LogInformation($"CANCELED: Did you update the subscription info?");
                    }

                    stopRecognition.TrySetResult(0);
                };

                _recognizer.SessionStarted += async (s, e) =>
                {
                    _logger.LogInformation("\nSession started event.");
                    await TextToSpeech("Hello");
                };

                _recognizer.SessionStopped += (s, e) =>
                {
                    _logger.LogInformation("\nSession stopped event.");
                    _logger.LogInformation("\nStop recognition.");
                    stopRecognition.TrySetResult(0);
                };

                // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
                await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                // Waits for completion.
                // Use Task.WaitAny to keep the task rooted.
                Task.WaitAny(new[] { stopRecognition.Task });

                // Stops recognition.
                await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "The queue processing task object has been disposed.");
            }
            catch (Exception ex)
            {
                // Catch all other exceptions and log
                _logger.LogError(ex, "Caught Exception");
            }

            _isDraining = false;
        }
*/

        public async Task TextToSpeech(string text)
        {
            // convert the text to speech
            SpeechSynthesisResult result = await _synthesizer.SpeakTextAsync(text);
            // take the stream of the result
            // create 20ms media buffers of the stream
            // and send to the AudioSocket in the BotMediaStream
            using (var stream = AudioDataStream.FromResult(result))
            {
                var currentTick = DateTime.Now.Ticks;
                MediaStreamEventArgs args = new MediaStreamEventArgs
                {
                    AudioMediaBuffers = Util.Utilities.CreateAudioMediaBuffers(stream, currentTick, _logger)
                };
                OnSendMediaBufferEventArgs(this, args);
            }
        }
    }
}

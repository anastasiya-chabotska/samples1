// ***********************************************************************
// Assembly         : EchoBot.Services
// Author           : JasonTheDeveloper
// Created          : 09-07-2020
//
// Last Modified By : bcage29
// Last Modified On : 02-28-2022
// ***********************************************************************
// <copyright file="CallHandler.cs" company="Microsoft">
//     Copyright �  2020
// </copyright>
// <summary></summary>
// ***********************************************************************>

using Microsoft.Graph;
using Microsoft.Graph.Communications.Calls;
using Microsoft.Graph.Communications.Calls.Media;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Graph.Communications.Resources;
using EchoBot.Services.ServiceSetup;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using System.Threading;
using Microsoft.Skype.Internal.Bots.Media;
using System.Runtime.InteropServices;
using System.Net.Http;       // request http
using System.Net;            // request http
using Newtonsoft.Json.Linq; ///request http
using Newtonsoft.Json;
using System.Linq;

namespace EchoBot.Services.Bot
{
    /// <summary>
    /// Call Handler Logic.
    /// </summary>
    public class CallHandler : HeartbeatHandler
    {
        /// <summary>
        /// Gets the call.
        /// </summary>
        /// <value>The call.</value>
        public ICall Call { get; }

        /// <summary>
        /// Gets the bot media stream.
        /// </summary>
        /// <value>The bot media stream.</value>
        public BotMediaStream BotMediaStream { get; private set; }
        private Thread thread;
        /// <summary>
        /// Initializes a new instance of the <see cref="CallHandler" /> class.
        /// </summary>
        /// <param name="statefulCall">The stateful call.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="logger"></param>
        public CallHandler(
            ICall statefulCall,
            AppSettings settings,
            ILogger logger
        )
            : base(TimeSpan.FromMinutes(10), statefulCall?.GraphLogger)
        {
            this.Call = statefulCall;
            this.Call.OnUpdated += this.CallOnUpdated;
            Console.WriteLine("before to start the meet");
            Console.WriteLine("My call id is:", this.Call.Id);
            this.Call.Participants.OnUpdated += this.ParticipantsOnUpdated;
            this.BotMediaStream = new BotMediaStream(this.Call.GetLocalMediaSession(), this.Call.Id, this.GraphLogger, logger, settings, this.Call.Resource.TenantId, this.Call.Resource.ChatInfo.ThreadId);

            //Task.Run(() => RepeatedTask());
            //thread = new Thread(new ThreadStart(this.RepeatedTask));
            //thread.Start();
        }

        /// <inheritdoc/>
        protected override Task HeartbeatAsync(ElapsedEventArgs args)
        {
            return this.Call.KeepAliveAsync();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            Console.WriteLine("write a output CallHanlder Dispose");
            Console.WriteLine($"my call is: ({this.Call.Resource.CallbackUri})");
            Console.WriteLine($"my call is CallChainId: ({this.Call.Resource.CallChainId})");
            Console.WriteLine($"my call is ThreadId: ({this.Call.Resource.ChatInfo.ThreadId})");
            Console.WriteLine($"my call is MyParticipantId: ({this.Call.Resource.MyParticipantId})");
            Console.WriteLine($"my call is TenantId: ({this.Call.Resource.TenantId})");
            this.BotMediaStream.sendSummary(this.Call.Id, this.Call.Resource.TenantId, this.Call.Resource.ChatInfo.ThreadId);
            base.Dispose(disposing);
            this.Call.OnUpdated -= this.CallOnUpdated;
            this.Call.Participants.OnUpdated -= this.ParticipantsOnUpdated;

            this.BotMediaStream?.ShutdownAsync().ForgetAndLogExceptionAsync(this.GraphLogger);
        }

        /// <summary>
        /// Event fired when the call has been updated.
        /// </summary>
        /// <param name="sender">The call.</param>
        /// <param name="e">The event args containing call changes.</param>
        private async void CallOnUpdated(ICall sender, ResourceEventArgs<Call> e)
        {
            GraphLogger.Info($"Call status updated to {e.NewResource.State} - {e.NewResource.ResultInfo?.Message}");
            Console.WriteLine("end my meet 1");
            if (e.OldResource.State != e.NewResource.State && e.NewResource.State == CallState.Established)
            {
                // Call is established...
            }

            if ((e.OldResource.State == CallState.Established) && (e.NewResource.State == CallState.Terminated))
            {
                if (BotMediaStream != null)
                {
                    //await BotMediaStream.StopMedia();
                    await this.BotMediaStream?.ShutdownAsync().ForgetAndLogExceptionAsync(this.GraphLogger);
                }
            }
        }

        /// <summary>
        /// Creates the participant update json.
        /// </summary>
        /// <param name="participantId">The participant identifier.</param>
        /// <param name="participantDisplayName">Display name of the participant.</param>
        /// <returns>System.String.</returns>
        private string createParticipantUpdateJson(string participantId, string participantDisplayName = "")
        {
            if (participantDisplayName.Length==0)
                return "{" + String.Format($"\"Id\": \"{participantId}\"") + "}";
            else
                return "{" + String.Format($"\"Id\": \"{participantId}\", \"DisplayName\": \"{participantDisplayName}\"") + "}";
        }

        /// <summary>
        /// Updates the participant.
        /// </summary>
        /// <param name="participants">The participants.</param>
        /// <param name="participant">The participant.</param>
        /// <param name="added">if set to <c>true</c> [added].</param>
        /// <param name="participantDisplayName">Display name of the participant.</param>
        /// <returns>System.String.</returns>
        private string updateParticipant(List<IParticipant> participants, IParticipant participant, bool added, string participantDisplayName = "")
        {
            if (added)
                participants.Add(participant);
            else
                participants.Remove(participant);
            GraphLogger.Info($"Call updateParticipant {participant} is {participantDisplayName}");
            return createParticipantUpdateJson(participant.Id, participantDisplayName);
        }

        /// <summary>
        /// Updates the participants.
        /// </summary>
        /// <param name="eventArgs">The event arguments.</param>
        /// <param name="added">if set to <c>true</c> [added].</param>
        private void updateParticipants(ICollection<IParticipant> eventArgs, bool added = true)
        {
            if(eventArgs.Count == 0)
            {
                Console.WriteLine("end my meet 2");
            }
            foreach (var participant in eventArgs)
            {
                var json = string.Empty;

                var participantDetails = participant.Resource.Info.Identity.User;
                string participantDetailsJson = JsonConvert.SerializeObject(participantDetails);
                Console.WriteLine("****************** qwerty" + participantDetails);
                Console.WriteLine(participantDetailsJson);
                if (participantDetails != null)
                {
                    json = updateParticipant(this.BotMediaStream.participants, participant, added, participantDetails.DisplayName);
                    Task.Run(() =>
                    {
                        try
                        {
                            this.BotMediaStream.updateCurrentParticipants();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("error in file CallHanlder.cs: " + ex);
                        }
                    });

                    Console.WriteLine("Participant *** 1"+ participantDetails.DisplayName);
                    foreach (IParticipant myParticipant in this.BotMediaStream.participants)
                    {
                        Console.WriteLine("Participant ID: " + myParticipant.Id);
                        //int newData = int.Parse(myParticipant.Id);
                        //Console.WriteLine("Participant ID2: " + newData);
                        Console.WriteLine("Participant Name: " + myParticipant.Resource.Info?.Identity?.User?.DisplayName);
                        Console.WriteLine("Participant Is In Lobby: " + myParticipant.Resource.IsInLobby);
                        Console.WriteLine("Participant Is Muted: " + myParticipant.Resource.IsMuted);
                        GlobalVariables.writeFileControl(5, "File CallHandler.cs, function updateParticipants add/remove participant A: myParticipant.Resource.Info?.Identity?.User?.DisplayName: " + myParticipant.Resource.Info?.Identity?.User?.DisplayName, this.Call?.Id);

                        if (participant.Resource.MediaStreams != null)
                        {
                            Console.WriteLine("Participant Media Streams:");

                            foreach (MediaStream mediaStream in participant.Resource.MediaStreams)
                            {
                               // Console.WriteLine("\tMedia Stream ID: " + mediaStream.Id);
                                Console.WriteLine("\tMedia Stream Type: " + mediaStream.MediaType);
                                Console.WriteLine("\tMedia Stream Direction: " + mediaStream.Direction);
                            }
                        }
                        Console.WriteLine();
                    }

                }
                else if (participant.Resource.Info.Identity.AdditionalData?.Count > 0)
                {
                    if (CheckParticipantIsUsable(participant))
                    {
                        json = updateParticipant(this.BotMediaStream.participants, participant, added);
                        Task.Run(() =>
                        {
                            try
                            {
                                this.BotMediaStream.updateCurrentParticipants();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("error in file CallHanlder.cs: " + ex);
                            }
                        }); Console.WriteLine("Participant *** 2");
                        foreach (IParticipant myParticipant in this.BotMediaStream.participants)
                        {
                            Console.WriteLine("Participant ID: " + myParticipant.Id);
                            Console.WriteLine("Participant Name: " + myParticipant.Resource.Info?.Identity?.User?.DisplayName);
                            Console.WriteLine("Participant Is In Lobby: " + myParticipant.Resource.IsInLobby);
                            Console.WriteLine("Participant Is Muted: " + myParticipant.Resource.IsMuted);

                            Console.WriteLine();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Event fired when the participants collection has been updated.
        /// </summary>
        /// <param name="sender">Participants collection.</param>
        /// <param name="args">Event args containing added and removed participants.</param>
        public void ParticipantsOnUpdated(IParticipantCollection sender, CollectionEventArgs<IParticipant> args)
        {
            updateParticipants(args.AddedResources);
            updateParticipants(args.RemovedResources, false);
        }

        /// <summary>
        /// Checks the participant is usable.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        private bool CheckParticipantIsUsable(IParticipant p)
        {
            foreach (var i in p.Resource.Info.Identity.AdditionalData)
                if (i.Key != "applicationInstance" && i.Value is Identity)
                    return true;

            return false;
        }
       
    }
}

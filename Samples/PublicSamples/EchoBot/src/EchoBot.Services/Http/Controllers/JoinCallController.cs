// ***********************************************************************
// Assembly         : EchoBot.Services
// Author           : JasonTheDeveloper
// Created          : 09-07-2020
//
// Last Modified By : bcage29
// Last Modified On : 02-28-2022
// ***********************************************************************
// <copyright file="JoinCallController.cs" company="Microsoft">
//     Copyright ©  2020
// </copyright>
// <summary></summary>
// ***********************************************************************
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Communications.Core.Serialization;
using EchoBot.Model.Constants;
using EchoBot.Model.Models;
using EchoBot.Services.Contract;
using EchoBot.Services.ServiceSetup;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Extensions.Logging;
using EchoBot.Services.Bot;
using Microsoft.Graph.Communications.Common.Telemetry;


namespace EchoBot.Services.Http.Controllers
{
    /// <summary>
    /// JoinCallController is a third-party controller (non-Bot Framework) that can be called in CVI scenario to trigger the bot to join a call.
    /// </summary>
    public class JoinCallController : ApiController
    {
        /// <summary>
        /// The bot service
        /// </summary>
        private readonly IBotService _botService;
        private readonly IBotService _botService2;
        /// <summary>
        /// The settings
        /// </summary>
        private readonly AppSettings _settings;
        /// <summary>
        /// the logger
        /// </summary>
        public ILogger _logger { get; set; }


        /// <summary>
        /// Initializes a new instance of the <see cref="JoinCallController" /> class.

        /// </summary>
        public JoinCallController()
        {
            _botService = AppHost.AppHostInstance.Resolve<IBotService>();
            _settings = AppHost.AppHostInstance.Resolve<IOptions<AppSettings>>().Value;
            _logger = AppHost.AppHostInstance.Resolve<ILogger<JoinCallController>>();
            /*_botService2 = AppHost.AppHostInstance.Resolve<IBotService>();
            _botService2.SetAlternativeCredentials("881fc4a1-e9ac-4a1d-969c-cf8342d43b80", "JUx8Q~LF4EQL4.5-yVA0JiHgH5mNzw.QsJQ9la1k");

            if (Object.ReferenceEquals(_botService, _botService2))
            {
                GlobalVariables.WriteGeneralLog("Warning: _botService and _botService2 are the same instance. This could lead to shared credential issues.", "Warning");
            }
            else
            {
                GlobalVariables.WriteGeneralLog("Info: _botService and _botService2 are independent instances with separate memory references.", "Info");
            }*/

            var graphLogger = AppHost.AppHostInstance.Resolve<IGraphLogger>();
            var settings = AppHost.AppHostInstance.Resolve<IOptions<AppSettings>>();
            var azureSettings = AppHost.AppHostInstance.Resolve<IAzureSettings>();
            var logger = AppHost.AppHostInstance.Resolve<ILogger<BotService>>();

            _botService2 = new BotService(
                graphLogger,
                logger,
                settings,
                azureSettings,
                "881fc4a1-e9ac-4a1d-969c-cf8342d43b80", // alternativeAppId
                "JUx8Q~LF4EQL4.5-yVA0JiHgH5mNzw.QsJQ9la1k"  // alternativeAppSecret
            );

            _settings = settings.Value;

            if (_botService != null)
            {
                GlobalVariables.WriteGeneralLog("Info: _botService fue creado exitosamente usando el segundo constructor con credenciales alternativas.", "Info");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JoinCallController" /> class.

        /// </summary>
        /// <param name="botService">The bot service.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="logger">The logger.</param>
        public JoinCallController(IBotService botService, AppSettings settings, ILogger<JoinCallController> logger)
        {
            _logger = logger;
            _botService = botService;
            _settings = settings;
        }

        /// <summary>
        /// The join call async.
        /// </summary>
        /// <param name="joinCallBody">The join call body.</param>
        /// <returns>The <see cref="HttpResponseMessage" />.</returns>
        [HttpPost]
        [Route(HttpRouteConstants.JoinCall)]
        public async Task<HttpResponseMessage> JoinCallAsync([FromBody] JoinCallBody joinCallBody)
        {
            try
            {
                _logger.LogInformation("JOIN CALL");
                GlobalVariables.temporaryLanguage = "en-US";
                var body = await this.Request.Content.ReadAsStringAsync();
                IBotService botServiceToUse = joinCallBody.newVersion ? _botService2 : _botService;
                var call = await botServiceToUse.JoinCallAsync(joinCallBody).ConfigureAwait(false);
                //var call = await _botService.JoinCallAsync(joinCallBody).ConfigureAwait(false);
                _logger.LogInformation($"Info Call is: {call}");
                GlobalVariables.writeFileControl(1, "", call.Id);
                GlobalVariables.MyGlobalLanguage.AddOrUpdate(call.Id, GlobalVariables.temporaryLanguage, (key, oldValue) => GlobalVariables.temporaryLanguage);
                var userEmail = joinCallBody.userEmail;
                Console.WriteLine("****************** USER EMAIL 2  ******");
                Console.WriteLine(userEmail);
                Console.WriteLine("call id") ;
                Console.WriteLine(call.Id);
                GlobalVariables.MyGlobalUserEmail.AddOrUpdate(call.Id, userEmail, (key, oldValue) => userEmail);
                var values = new JoinUrlResponse()
                {
                    CallId = call.Id,
                    ScenarioId = call.ScenarioId,
                    Port = _settings.BotInstanceExternalPort.ToString()
                };

                var serializer = new CommsSerializer(pretty: true);
                var json = serializer.SerializeObject(values);
                var response = this.Request.CreateResponse(HttpStatusCode.OK);
                response.Content = new StringContent(json, Encoding.UTF8, "application/json");
                _logger.LogInformation($"input in logger task join call async = {values.CallId}");
                return response;
            }
            catch (ServiceException e)
            {
                HttpResponseMessage response = (int)e.StatusCode >= 300
                    ? this.Request.CreateResponse(e.StatusCode)
                    : this.Request.CreateResponse(HttpStatusCode.InternalServerError);

                if (e.ResponseHeaders != null)
                {
                    foreach (var responseHeader in e.ResponseHeaders)
                    {
                        response.Headers.TryAddWithoutValidation(responseHeader.Key, responseHeader.Value);
                    }
                }

                response.Content = new StringContent(e.ToString());
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Received HTTP {this.Request.Method}, {this.Request.RequestUri}");
                HttpResponseMessage response = this.Request.CreateResponse(HttpStatusCode.InternalServerError);
                response.Content = new StringContent(e.Message);
                return response;
            }
        }
        [HttpPost]
        [Route(HttpRouteConstants.JoinCallSpanish)]
        public async Task<HttpResponseMessage> JoinCallAsyncSpanish([FromBody] JoinCallBody joinCallBody)
        {
            try
            {
                _logger.LogInformation("JOIN CALL SPANISH");
                GlobalVariables.temporaryLanguage = "es-ES";
                var body = await this.Request.Content.ReadAsStringAsync();
                var call = await _botService.JoinCallAsync(joinCallBody).ConfigureAwait(false);
                _logger.LogInformation($"Info Call is: {call}");
                GlobalVariables.writeFileControl(1, "", call.Id);
                GlobalVariables.MyGlobalLanguage.AddOrUpdate(call.Id, GlobalVariables.temporaryLanguage, (key, oldValue) => GlobalVariables.temporaryLanguage);
                var values = new JoinUrlResponse()
                {
                    CallId = call.Id,
                    ScenarioId = call.ScenarioId,
                    Port = _settings.BotInstanceExternalPort.ToString()
                };

                var serializer = new CommsSerializer(pretty: true);
                var json = serializer.SerializeObject(values);
                var response = this.Request.CreateResponse(HttpStatusCode.OK);
                response.Content = new StringContent(json, Encoding.UTF8, "application/json");
                _logger.LogInformation($"input in logger task join call async = {values.CallId}");
                return response;
            }
            catch (ServiceException e)
            {
                HttpResponseMessage response = (int)e.StatusCode >= 300
                    ? this.Request.CreateResponse(e.StatusCode)
                    : this.Request.CreateResponse(HttpStatusCode.InternalServerError);

                if (e.ResponseHeaders != null)
                {
                    foreach (var responseHeader in e.ResponseHeaders)
                    {
                        response.Headers.TryAddWithoutValidation(responseHeader.Key, responseHeader.Value);
                    }
                }

                response.Content = new StringContent(e.ToString());
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Received HTTP {this.Request.Method}, {this.Request.RequestUri}");
                HttpResponseMessage response = this.Request.CreateResponse(HttpStatusCode.InternalServerError);
                response.Content = new StringContent(e.Message);
                return response;
            }
        }

        [HttpPost]
        [Route(HttpRouteConstants.LeaveTheCall)]
        public async Task<HttpResponseMessage> LeaveCallAsync([FromUri] string callId)
        {
            try
            {
                _logger.LogInformation("LEAVE CALL");
                GlobalVariables.writeFileControl(2, "LEAVE CALL", "test_info");
                // Assuming EndCallByCallLegIdAsync is a method in _botService
                await _botService.EndCallByCallLegIdAsync(callId).ConfigureAwait(false);

                _logger.LogInformation($"Call with ID {callId} has been ended successfully.");

                var response = this.Request.CreateResponse(HttpStatusCode.OK);
                response.Content = new StringContent($"Call with ID {callId} has been left successfully.", Encoding.UTF8, "application/json");
                GlobalVariables.writeFileControl(2, "LEAVE CALL 2", "test_info");
                return response;
            }
            catch (ServiceException e)
            {
                HttpResponseMessage response = (int)e.StatusCode >= 300
                    ? this.Request.CreateResponse(e.StatusCode)
                    : this.Request.CreateResponse(HttpStatusCode.InternalServerError);

                if (e.ResponseHeaders != null)
                {
                    foreach (var responseHeader in e.ResponseHeaders)
                    {
                        response.Headers.TryAddWithoutValidation(responseHeader.Key, responseHeader.Value);
                    }
                }

                response.Content = new StringContent(e.ToString());
                GlobalVariables.writeFileControl(1, "ERROR LEAVE"+e.ToString(), "test_info");
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Received HTTP {this.Request.Method}, {this.Request.RequestUri}");
                HttpResponseMessage response = this.Request.CreateResponse(HttpStatusCode.InternalServerError);
                response.Content = new StringContent(e.Message);
                GlobalVariables.writeFileControl(1, "ERROR LEAVE 2" + new StringContent(e.Message), "test_info");
                return response;
            }
        }


    }
}

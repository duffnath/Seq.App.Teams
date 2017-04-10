﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Seq.Apps;
using Seq.Apps.LogEvents;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Seq.App.Teams
{
    [SeqApp("Teams",
    Description = "Sends log events to Teams.")]
    public class TeamsReactor : Reactor, ISubscribeTo<LogEventData>
    {
        
        private static IDictionary<LogEventLevel, string> _levelColorMap = new Dictionary<LogEventLevel, string>
        {
            {LogEventLevel.Verbose, "gray"},
            {LogEventLevel.Debug, "gray"},
            {LogEventLevel.Information, "green"},
            {LogEventLevel.Warning, "yellow"},
            {LogEventLevel.Error, "red"},
            {LogEventLevel.Fatal, "red"},
        };

        [SeqAppSetting(
        DisplayName = "Seq Base URL",
        HelpText = "Used for generating perma links to events in Teams messages.",
        IsOptional = true)]
        public string BaseUrl { get; set; }
        
        [SeqAppSetting(
        DisplayName = "Teams WebHook URL",
        HelpText = "Used to send message to Teams")]
        public string TeamsBaseUrl { get; set; }

        [SeqAppSetting(
        DisplayName = "Trace all Message ",
        HelpText = "Used to show all messages to trace",
        IsOptional = true)]
        public bool TraceMessage { get; set; }

        [SeqAppSetting(
        HelpText = "Background color for message. One of \"yellow\", \"red\", \"green\", \"purple\", \"gray\", or \"random\". (default: auto based on message level)",
        IsOptional = true)]
        public string Color { get; set; }
        

        public void On(Event<LogEventData> evt)
        {

            try
            {
                if (TraceMessage)
                {
                    Log
                        .ForContext("Uri", new Uri(TeamsBaseUrl))
                        .Information("Start Processing {Message}", evt.Data.RenderedMessage);
                }

                TeamsCard body = BuildBody(evt);

                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(TeamsBaseUrl);

                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                  
                    var response = client.PostAsync(
                        "", 
                        new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
                        ).Result;

                    if (!response.IsSuccessStatusCode)
                    {
                        Log
                            .ForContext("Uri", response.RequestMessage.RequestUri)
                            .Error("Could not send Teams message, server replied {StatusCode} {StatusMessage}: {Message}", Convert.ToInt32(response.StatusCode), response.StatusCode, response.Content.ReadAsStringAsync().Result);
                    }
                    else
                    {
                        if (TraceMessage)
                        {
                            string reponseResult = response.Content.ReadAsStringAsync().Result;
                            Log
                                .ForContext("Uri", response.RequestMessage.RequestUri)
                                .Information("Server replied {StatusCode} {StatusMessage}: {Message}", Convert.ToInt32(response.StatusCode), response.StatusCode, reponseResult);
                        }
                    }
                }
            }
            catch (Exception ex)
            {

                Log
                    .ForContext("Uri", new Uri(TeamsBaseUrl))
                    .Error(ex, "An error occured while constructing request.");
            }
        }

        private TeamsCard BuildBody(Event<LogEventData> evt)
        {

            string link = $"{BaseUrl}/#/events?filter=@Id%20%3D%3D%20%22{evt.Id}%22&show=expanded";

            var msg = new StringBuilder($"** {evt.Data.Level.ToString()} :** <a href={link}> link </a> {evt.Data.RenderedMessage} ");
            if (msg.Length > 1000)
            {
                msg.Length = 1000;
            }

            

            TeamsPotentialAction action = new TeamsPotentialAction()
            {
                Type = "ViewAction",
                Name = "Click here to open in Seq",
                Target = new string[] { link }
            };
            

            var color = Color;
            if (string.IsNullOrWhiteSpace(color))
            {
                color = _levelColorMap[evt.Data.Level];
            }

            TeamsCard body = new TeamsCard()
            {
                Title = "<span style='color:"+color+"'>" + evt.Data.Level.ToString()+"</span>",
                ThemeColor = color,
                Text = msg.ToString(),
                PotentialAction = new TeamsPotentialAction[] { action }
            };

            return body;
        }
    }
}

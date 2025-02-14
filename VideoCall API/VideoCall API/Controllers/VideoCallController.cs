﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Twilio.Jwt.AccessToken;
using Twilio;
using Twilio.Rest.Video.V1.Room;
using Twilio.Types;
using Twilio.Base;
using System.Net;
using System.Text;
using VideoCall_API.Models;
using Microsoft.Extensions.Configuration;

namespace VideoCall_API.Controllers
{
    [Route("[controller]/[action]")]
    [ApiController]
    public class VideoCallController : ControllerBase
    {
        private readonly ILogger<VideoCallController> _logger;
        private readonly IConfiguration _config;

        public VideoCallController(ILogger<VideoCallController> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }
        
        /// <summary>
        /// Generate token for user
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        [HttpGet]
        public object GenerateToken(string groupName, string userName)
        {
            try
            {
                string twilioAccountSid = _config.GetValue<string>("TwilioAPISetting:twilioAccountSid");
                string authToken = _config.GetValue<string>("TwilioAPISetting:authToken");
                string twilioApiKey = _config.GetValue<string>("TwilioAPISetting:twilioApiKey");
                string twilioApiSecret = _config.GetValue<string>("TwilioAPISetting:twilioApiSecret");
                string serviceSid = _config.GetValue<string>("TwilioAPISetting:serviceSid");

                TwilioClient.Init(twilioAccountSid, authToken);

                /*
                 * First using group name we search room with same name exists or not.
                 * if not exists then we create one room with group name
                 */

                Twilio.Rest.Video.V1.RoomResource room;
                try
                {
                    room = Twilio.Rest.Video.V1.RoomResource.Fetch(pathSid: groupName);
                }
                catch (Exception ex)
                {
                    try
                    {
                        room = Twilio.Rest.Video.V1.RoomResource.Create(uniqueName: groupName);
                    }
                    catch (Exception e)
                    {
                        return null;
                    }

                }

                Twilio.Rest.Conversations.V1.Service.ConversationResource conversation;

                /*
                 * After that we check in room that we have conversation service available or not using service key
                 * and room id. 
                 * If conversation service not available in room then we add in room.
                */
                try
                {
                    conversation = Twilio.Rest.Conversations.V1.Service.ConversationResource.Fetch(
                        pathChatServiceSid: serviceSid,
                        pathSid: room.Sid
                    );
                }
                catch (Exception ex)
                {
                    try
                    {
                        conversation = Twilio.Rest.Conversations.V1.Service.ConversationResource.Create(
                            pathChatServiceSid: serviceSid,
                            uniqueName: room.Sid
                        );
                    }
                    catch (Exception e)
                    {
                        return null;
                    }

                }

                /*
                 * After that we add participant in conversation using service key,conversation key and user name
                */

                try
                {
                    var participant = Twilio.Rest.Conversations.V1.Service.Conversation.ParticipantResource.Create(
                        pathChatServiceSid: serviceSid,
                        pathConversationSid: conversation.Sid,
                        identity: userName
                    );
                }
                catch (Exception e)
                {

                }

                /*
                 * Here we create one access token to join video call.
                 * We have add grant that which service we want to use. 
                 * Ex we add video and chat grant.
                */

                var video = new VideoGrant();
                video.Room = groupName;

                var grant = new ChatGrant
                {
                    ServiceSid = serviceSid
                };

                var grants = new HashSet<IGrant>
                {
                    video,
                    { grant }
                };

                var token = new Token(
                    twilioAccountSid,
                    twilioApiKey,
                    twilioApiSecret,
                    identity: userName,
                    grants: grants);

                return token.ToJwt();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Start and stop recording using roomId
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="isRecording"></param>
        /// <returns></returns>
        [HttpGet]
        public object RecordingVideo(string roomId, bool isRecording)
        {
            try
            {
                string twilioAccountSid = _config.GetValue<string>("TwilioAPISetting:twilioAccountSid");
                string authToken = _config.GetValue<string>("TwilioAPISetting:authToken");
                TwilioClient.Init(twilioAccountSid, authToken);
                if (isRecording)
                {
                    ///For stop recording
                    var recordingRules = RecordingRulesResource.Update(
                        rules: new List<RecordingRule>(){
                            new RecordingRule(Twilio.Types.RecordingRule.TypeEnum.Exclude, true, null, null, null),
                        },
                        pathRoomSid: roomId
                    );

                    return recordingRules;
                }
                else
                {
                    ///For start recording
                    var recordingRules = RecordingRulesResource.Update(
                        rules: new List<RecordingRule>(){
                            new RecordingRule(Twilio.Types.RecordingRule.TypeEnum.Include, true, null, null, null),
                        },
                        pathRoomSid: roomId
                    );

                    return recordingRules;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// get video recording list roomSid
        /// </summary>
        /// <param name="roomSid"></param>
        /// <returns></returns>
        [HttpGet]
        public object getVideoRecordingList(string roomSid)
        {
            try
            {
                string twilioAccountSid = _config.GetValue<string>("TwilioAPISetting:twilioAccountSid");
                string authToken = _config.GetValue<string>("TwilioAPISetting:authToken");

                TwilioClient.Init(twilioAccountSid, authToken);
                List<VideoRecordingModel> recordings = new List<VideoRecordingModel>();
                var roomRecordings = RoomRecordingResource.Read(
                        pathRoomSid: roomSid,
                        status: RoomRecordingResource.StatusEnum.Completed
                    ).GetEnumerator();

                while (roomRecordings.MoveNext())
                {
                    recordings.Add(new VideoRecordingModel()
                    {
                        DateCreated = roomRecordings.Current.DateCreated,
                        Duration = roomRecordings.Current.Duration,
                        Type = roomRecordings.Current.Type == RoomRecordingResource.TypeEnum.Audio ? "audio" : "video",
                        RoomSid = roomRecordings.Current.RoomSid,
                        Sid = roomRecordings.Current.Sid,
                        ContainerFormat = roomRecordings.Current.ContainerFormat == RoomRecordingResource.FormatEnum.Mka ? "mka" : "mkv",
                    });
                }
                recordings = recordings.GroupBy(p => p.DateCreated).Select(p => p.FirstOrDefault()).ToList();
                return recordings;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Download video recording using recordingSid
        /// </summary>
        /// <param name="recordingSid"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        [HttpGet]
        public object DownloadVideoRecording(string recordingSid, string type)
        {
            try
            {
                string twilioApiKey = _config.GetValue<string>("TwilioAPISetting:twilioApiKey");
                string twilioApiSecret = _config.GetValue<string>("TwilioAPISetting:twilioApiSecret");
                string twilioAccountSid = _config.GetValue<string>("TwilioAPISetting:twilioAccountSid");
                string authToken = _config.GetValue<string>("TwilioAPISetting:authToken");

                TwilioClient.Init(twilioAccountSid, authToken);
                string uri = $"https://video.twilio.com/v1/Recordings/{recordingSid}/Media";

                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(twilioApiKey + ":" + twilioApiSecret)));
                request.Headers.Add("Accept", "application/json");
                request.ContentType = "application/json";
                request.Method = "GET";
                request.AllowAutoRedirect = true;

                var response = (HttpWebResponse)request.GetResponse();
                return response.ResponseUri.AbsoluteUri;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Get room participant list using roomId
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        [HttpGet]
        public object ParticipantList(string roomId)
        {
            try
            {
                string twilioAccountSid = _config.GetValue<string>("TwilioAPISetting:twilioAccountSid");
                string authToken = _config.GetValue<string>("TwilioAPISetting:authToken");
                TwilioClient.Init(twilioAccountSid, authToken);
                ResourceSet<ParticipantResource> participants = ParticipantResource.Read(
                   roomId,
                   ParticipantResource.StatusEnum.Connected);

                return participants;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Remove participant for room using roomId and participantId
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="participantId"></param>
        /// <returns></returns>
        [HttpGet]
        public object RemoveParticipant(string roomId, string participantId)
        {
            try
            {
                string twilioAccountSid = _config.GetValue<string>("TwilioAPISetting:twilioAccountSid");
                string authToken = _config.GetValue<string>("TwilioAPISetting:authToken");
                TwilioClient.Init(twilioAccountSid, authToken);
                ParticipantResource participant = ParticipantResource.Update(
                        roomId,
                        participantId,
                        ParticipantResource.StatusEnum.Disconnected);

                return 1;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}

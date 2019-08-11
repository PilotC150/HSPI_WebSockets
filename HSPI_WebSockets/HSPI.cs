using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using System.Web;
using HomeSeerAPI;
using Hspi;
using Newtonsoft.Json;
using Scheduler;

namespace HSPI_WebSockets
{
    // ReSharper disable once InconsistentNaming
    public class HSPI : HspiBase2
    {
        public const string PLUGIN_NAME = "Websockets Notifications";
        private string _port = "8080";

        public HSPI()
        {
            
        }

        public override int AccessLevel()
        {
            return 1;
        }

        protected string IniFilename
        {
            get { return PLUGIN_NAME + ".ini"; }
        }

        public override IPlugInAPI.strInterfaceStatus InterfaceStatus()
        {
            // TODO: Check if the web socket port is set.
            return new IPlugInAPI.strInterfaceStatus { intStatus = IPlugInAPI.enumInterfaceStatus.OK };
        }
        
        public override void HSEvent(Enums.HSEvent eventType, object[] parameters)
        {
            // TODO: This is where the bulk of the work happens

            //if (!IsAnyWebHookConfigured())
            //{
            //    Program.WriteLog(LogType.Debug,
            //        "Ignoring event " + eventType + " because no webhook endpoint is configured.");
            //    return;
            //}

            Dictionary<string, object> dict = new Dictionary<string, object> {
                {"eventType", eventType.ToString()}
            };

            try
            {
                int devRef;

                switch (eventType)
                {
                    case Enums.HSEvent.VALUE_SET:
                    case Enums.HSEvent.VALUE_CHANGE:
                        devRef = (int)parameters[4];
                        dict.Add("address", (string)parameters[1]);
                        dict.Add("newValue", ((double)parameters[2]).ToString(CultureInfo.InvariantCulture));
                        dict.Add("oldValue", ((double)parameters[3]).ToString(CultureInfo.InvariantCulture));
                        dict.Add("ref", devRef);
                        break;

                    case Enums.HSEvent.STRING_CHANGE:
                        devRef = (int)parameters[3];
                        dict.Add("address", (string)parameters[1]);
                        dict.Add("newValue", (string)parameters[2]);
                        dict.Add("ref", devRef);
                        break;

                    default:
                        HS.WriteLog("Warn", "Unknown event type " + eventType);
                        return;
                }
                
                string json = JsonConvert.SerializeObject(dict);
                HS.WriteLog("Verbose", json);

                WebSocketServer.Broadcast(json);

            }
            catch (Exception ex)
            {
                HS.WriteLog("Error", ex.ToString());
            }
        }

        public override string InitIO(string port)
        {
            // debug
            HS.WriteLog(Name, "Entering InitIO");

            // initialise everything here, return a blank string only if successful, or an error message

            var _port = HS.GetINISetting("Config", "Port", "8080", IniFilename);
            

            // Configure events for when values change
            Callback.RegisterEventCB(Enums.HSEvent.VALUE_SET, PLUGIN_NAME, "");
            Callback.RegisterEventCB(Enums.HSEvent.VALUE_CHANGE, PLUGIN_NAME, "");
            Callback.RegisterEventCB(Enums.HSEvent.STRING_CHANGE, PLUGIN_NAME, "");

            HS.RegisterPage("WebSocketNotificationConfig", Name, "");
            
            WebPageDesc configLink = new WebPageDesc
            {
                plugInName = Name,
                plugInInstance = "",
                link = "WebSocketNotificationConfig",
                linktext = "Config",
                order = 1,
                page_title = "WebSockets Notifications Settings"
            };
            Callback.RegisterConfigLink(configLink);
            Callback.RegisterLink(configLink);

            WebSocketServer.Start("http://localhost:" + _port +"/");

            // debug
            HS.WriteLog(Name, "Completed InitIO");
            return "";
        }

        public override void ShutdownIO()
        {
            // debug
            HS.WriteLog(Name, "Entering ShutdownIO");

            // shut everything down here
            WebSocketServer.Stop();

            // let our console wrapper know we are finished
            Shutdown = true;

            // debug
            HS.WriteLog(Name, "Completed ShutdownIO");
        }

        public override string GetPagePlugin(string page, string user, int userRights, string queryString)
        {
            HS.WriteLog("Debug", "Requested page name " + page + " by user " + user + " with rights " + userRights);
            if (page != "WebSocketNotificationConfig")
            {
                return "Unknown page " + page;
            }

            PageBuilderAndMenu.clsPageBuilder pageBuilder = new PageBuilderAndMenu.clsPageBuilder(page);

            if ((userRights & 2) != 2)
            {
                // User is not an admin
                pageBuilder.reset();
                pageBuilder.AddHeader(HS.GetPageHeader(page, "WebHook Notifications Settings", "", "", false, true));
                pageBuilder.AddBody("<p><strong>Access Denied:</strong> You are not an administrative user.</p>");
                pageBuilder.AddFooter(HS.GetPageFooter());
                pageBuilder.suppressDefaultFooter = true;

                return pageBuilder.BuildPage();
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(
                PageBuilderAndMenu.clsPageBuilder.FormStart("wsn_config_form", "wsn_config_form", "post"));

            stringBuilder.Append(
                "<table width=\"1000px\" cellspacing=\"0\"><tr><td class=\"tableheader\" colspan=\"3\">Settings</td></tr>");
            
            stringBuilder.Append(
                "<tr><td class=\"tablecell\" style=\"width:200px\" align=\"left\">WebSocket Port:</td>");
            stringBuilder.Append("<td class=\"tablecell\">");

            clsJQuery.jqTextBox textBox =
                new clsJQuery.jqTextBox("WebSocketPort", "text", _port, page, 100, true);
            stringBuilder.Append(textBox.Build());
            stringBuilder.Append("</td></tr>");

            stringBuilder.Append("</table>");

            clsJQuery.jqButton doneBtn = new clsJQuery.jqButton("DoneBtn", "Done", page, false);
            doneBtn.url = "/";
            stringBuilder.Append("<br />");
            stringBuilder.Append(doneBtn.Build());
            stringBuilder.Append("<br /><br />");

            pageBuilder.reset();
            pageBuilder.AddHeader(HS.GetPageHeader(page, "WebSocket Notifications Settings", "", "", false, true));
            pageBuilder.AddBody(stringBuilder.ToString());
            pageBuilder.AddFooter(HS.GetPageFooter());
            pageBuilder.suppressDefaultFooter = true;

            return pageBuilder.BuildPage();
        }

        public override string PostBackProc(string page, string data, string user, int userRights)
        {
            HS.WriteLog("Debug", "PostBackProc page name " + page + " by user " + user + " with rights " + userRights);
            if (page != "WebSocketNotificationConfig")
            {
                return "Unknown page " + page;
            }

            if ((userRights & 2) != 2)
            {
                // User is not an admin
                return "Access denied: you are not an administrative user.";
            }

            try
            {
                NameValueCollection postData = HttpUtility.ParseQueryString(data);

                string port = postData.Get("WebSocketPort");

                HS.SaveINISetting("Config", "Port", port, IniFilename);
            }
            catch (Exception ex)
            {
                HS.WriteLog("Warn", ex.ToString());
            }

            return "";
        }

        public override int Capabilities()
        {
            return (int)Enums.eCapabilities.CA_IO;
        }

        protected override string GetName()
        {
            return PLUGIN_NAME;
        }
    }
}
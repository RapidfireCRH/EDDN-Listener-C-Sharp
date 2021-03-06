﻿using NetMQ.Sockets;
using NetMQ;
using System;
using System.Text;
using Ionic.Zlib;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.IO;

namespace ConsoleApp
{
    class Program
    {
        public struct dbschema
        {
            public string schema;
            public DateTime gatewayTimestamp;
            public string softwareName;
            public string softwareVersion;
            public string uploaderID;
            public messageschema message_data;
            public string message;
            public string raw;
        }
        public struct messageschema
        {
            public DateTime timestamp;
            public eventtype _event;
            public starschema star;
        }
        public enum eventtype { Docked, FSDJump, Scan, Location };
        public struct starschema
        {
            public string name;
            public double x;
            public double y;
            public double z;
        }
        static ulong num = 0;
        static bool debugbool = false;
        static void Main(string[] args)
        {
            var utf8 = new UTF8Encoding();
            try
            {
                using (var client = new SubscriberSocket())
                {
                    client.Options.ReceiveHighWatermark = 1000;
                    client.Connect("tcp://eddn.edcd.io:9500");
                    client.SubscribeToAnyTopic();
                    while (true)
                    {
                        var bytes = client.ReceiveFrameBytes();
                        var uncompressed = ZlibStream.UncompressBuffer(bytes);

                        var result = utf8.GetString(uncompressed);

                        addmessage(result);
                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                Console.Read();
            }
        }

        public static void addmessage(string message)
        {
            debug("adding message " + message);
            dbschema next = new dbschema();

            debug("parsing message");
            dynamic stuff = JObject.Parse(message);

            debug("writing schema");
            next.schema = message.Substring(message.IndexOf("{\"$schemaRef\": \"") + "{\"$schemaRef\": \"".Length, message.IndexOf("\", \"header\":") - message.IndexOf("{\"$schemaRef\": \"") - "{\"$schemaRef\": \"".Length);
            debug("returning unwanted schemas");
            if (!(next.schema.Contains("journal")) || next.schema.Contains("test"))
                return;

            debug("Determining what type of Journal event");
            if (message.Contains("\"event\": \"Docked\""))
                next.message_data._event = eventtype.Docked;
            else if (message.Contains("\"event\": \"FSDJump\""))
                next.message_data._event = eventtype.FSDJump;
            else if (message.Contains("\"event\": \"Scan\""))
                next.message_data._event = eventtype.Scan;
            else if (message.Contains("\"event\": \"Location\""))
                next.message_data._event = eventtype.Location;

            debug("parsing timestamp");
            next.gatewayTimestamp = DateTime.Parse((string)stuff.header.gatewayTimestamp, new CultureInfo("EN-US", false));

            debug("parsing softwarename");
            next.softwareName = stuff.header.softwareName;

            debug("parsing software version");
            next.softwareVersion = stuff.header.softwareVersion;

            debug("parsing uploader id");
            next.uploaderID = stuff.header.uploaderID;

            debug("parsing message");
            next.message = message.Substring(message.IndexOf("\"message\": ") + "\"message\": ".Length, message.Length - message.IndexOf("\"message\": ") - "\"message\": ".Length - 1);

            debug("parsing timestamp");
            next.message_data.timestamp = DateTime.Parse((string)stuff.message.timestamp, new CultureInfo("EN-US", false));

            debug("parsing starsystem");
            next.message_data.star.name = stuff.message.StarSystem;

            debug("parsing x coord");
            try { next.message_data.star.x = stuff.message.StarPos[0]; } catch { next.message_data.star.x = 0; }

            debug("parsing y coord");
            try { next.message_data.star.y = stuff.message.StarPos[1]; } catch { next.message_data.star.y = 0; }

            debug("parsing z coord");
            try { next.message_data.star.z = stuff.message.StarPos[2]; } catch { next.message_data.star.z = 0; }

            debug("writing message");
            next.raw = message;

            debug("writing json");
            if (!File.Exists(next.gatewayTimestamp.Year + "_" + next.gatewayTimestamp.Month + "_" + next.gatewayTimestamp.Day + "_EDDN Dump.json"))
            {
                File.WriteAllText(next.gatewayTimestamp.Year + "_" + next.gatewayTimestamp.Month + "_" + next.gatewayTimestamp.Day + "_EDDN Dump.json", "");
                num = 0;
            }
            File.AppendAllLines(next.gatewayTimestamp.Year + "_" + next.gatewayTimestamp.Month + "_" + next.gatewayTimestamp.Day + "_EDDN Dump.json", new string[] { next.raw });
            debug("printing " + ++num + " | " + next.gatewayTimestamp.ToLongTimeString() + " | " + addspaces(next.uploaderID) + " | " + addspaces(next.message_data.star.name, 28) + " [ " + next.message_data.star.x + ", " + next.message_data.star.y + ", " + next.message_data.star.z + " ]");
            Console.WriteLine(num + " | " + next.gatewayTimestamp.ToLongTimeString() + " | " + addspaces(next.uploaderID) + " | " + addspaces(next.message_data.star.name, 28) + " [ " + next.message_data.star.x + ", " + next.message_data.star.y + ", " + next.message_data.star.z + " ]");
        }
        /// <summary>
        /// Adds number of spaces to make text numofchar long
        /// </summary>
        /// <param name="text">text to add spaces</param>
        /// <param name="numofchar">goal text length</param>
        /// <returns>text with appended spaces to numofchar long</returns>
        static string addspaces(string text, int numofchar = 32)
        {
            if (text == null)
                text = "";
            string ret = text;
            while (ret.Length < numofchar)
            {
                ret = ret + " ";
            }
            return ret;
        }
        /// <summary>
        /// Needed to clean up debug messages. Processes messages to different locations
        /// </summary>
        /// <param name="message">message to process</param>
        static void debug(string message)
        {
            if (!debugbool)
                return;
            //Console.WriteLine(message);
            File.AppendAllText("debug.log", DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + " | " + message + Environment.NewLine);
        }
    }
}

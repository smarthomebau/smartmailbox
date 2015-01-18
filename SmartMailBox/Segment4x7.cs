using System;
using System.Collections.Generic;
using System.Text;
using Tinkerforge;

namespace SmartMailBox
{
    static class Segment4x7
    {
        private static string HOST = "localhost";
        private static int PORT = 4223;
        private static string UID = "iV3"; // Change to your UID!

        public static void WriteSegments(byte[] content)
        {
            IPConnection ipcon = new IPConnection(); // Create IP connection
            BrickletSegmentDisplay4x7 sd4x7 = new BrickletSegmentDisplay4x7(UID, ipcon); // Create device object

            ipcon.Connect(HOST, PORT); // Connect to brickd
            // Don't use device before ipcon is connected

            // Write content
            byte[] segments = { content[0], content[1], content[2], content[3] };
            sd4x7.SetSegments(segments, 0, false);

            ipcon.Disconnect();
        }

    }
}

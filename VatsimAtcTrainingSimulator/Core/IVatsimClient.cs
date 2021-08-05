﻿using System;
using System.Threading.Tasks;

namespace VatsimAtcTrainingSimulator.Core
{
    interface IVatsimClient
    {
        VatsimConnectionHandler ConnHandler { get; }

        Action<string> Logger { get; set; }

        string Callsign { get; }

        Task<bool> Connect(string hostname, int port, string callsign, string cid, string password, string fullname, bool vatsim);

        Task Disconnect();
    }
}
